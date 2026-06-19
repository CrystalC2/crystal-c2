using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Client.Scripts.RailgunScript;

// ── Opcode constants (must match agent.c) ────────────────────────────────────
internal static class Op
{
    public const byte Halt     = 0x00;
    public const byte PushInt  = 0x01;
    public const byte PushStr  = 0x02;
    public const byte PushWStr = 0x03;
    public const byte PushOut  = 0x04;
    public const byte Load     = 0x05;
    public const byte Store    = 0x06;
    public const byte LoadAddr = 0x07;
    public const byte Call     = 0x08;
    public const byte Pop      = 0x09;
    public const byte Jmp      = 0x0A;
    public const byte Jz       = 0x0B;
    public const byte Jnz      = 0x0C;
    public const byte CmpEq    = 0x0D;
    public const byte CmpNeq   = 0x0E;
    public const byte CmpLt    = 0x0F;
    public const byte CmpGt    = 0x10;
    public const byte CmpLe    = 0x11;
    public const byte CmpGe    = 0x12;
    public const byte ArgInt   = 0x13;
    public const byte ArgStr   = 0x14;
    public const byte ArgWStr  = 0x15;
    public const byte ArgBytes = 0x16;
    public const byte Deref    = 0x17;
    public const byte Or       = 0x18;
}

internal sealed class Compiler(Encoding ansi, string? baseDir = null)
{
    private readonly List<byte>               _code      = [];
    private readonly List<string>             _dlls      = [];   // ordered; index = dll_idx
    private readonly Dictionary<string, int>  _dllMap    = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string>             _imports   = [];   // bare-name imports in order
    private readonly Dictionary<string, byte> _vars      = [];
    private readonly Dictionary<string, ulong> _constants = new(StringComparer.Ordinal);
    private byte _nextVar;

    // ── Public entry point ────────────────────────────────────────────────────

    public byte[] Compile(List<Stmt> program)
    {
        // Pass 1: resolve includes and defines (compile-time constants),
        // then register imports so the beacon's 0xFF search is populated.
        foreach (var s in program)
        {
            switch (s)
            {
                case IncludeStmt inc: LoadInclude(inc.Path); break;
                case DefineStmt  def: _constants[def.Name] = def.Value; break;
                case ImportStmt  imp:
                    _imports.Add(imp.Dll);
                    GetOrAddDll(imp.Dll);
                    break;
            }
        }

        // Pass 2: emit code, skipping directive statements.
        foreach (var s in program)
            if (s is not ImportStmt and not DefineStmt and not IncludeStmt)
                EmitStmt(s);

        _code.Add(Op.Halt);
        return BuildPayload();
    }

    private void LoadInclude(string path)
    {
        string fullPath;
        if (Path.IsPathRooted(path))
            fullPath = path;
        else if (baseDir is not null && File.Exists(Path.Combine(baseDir, path)))
            fullPath = Path.Combine(baseDir, path);
        else
            fullPath = Path.Combine(AppContext.BaseDirectory, path);

        string source;
        try { source = File.ReadAllText(fullPath); }
        catch (Exception ex) { throw new Exception($"include '{path}': {ex.Message}"); }

        var tokens = new Lexer(source).Tokenize();
        var stmts  = new Parser(tokens).ParseProgram();
        var dir    = Path.GetDirectoryName(fullPath) ?? "";

        foreach (var s in stmts)
        {
            switch (s)
            {
                case DefineStmt  def: _constants[def.Name] = def.Value; break;
                case IncludeStmt nested:
                    var np = Path.IsPathRooted(nested.Path) ? nested.Path
                             : Path.Combine(dir, nested.Path);
                    LoadInclude(np);
                    break;
            }
        }
    }

    // ── Payload header ────────────────────────────────────────────────────────

    private byte[] BuildPayload()
    {
        using var ms = new MemoryStream();

        ms.WriteByte((byte)_dlls.Count);
        foreach (var dll in _dlls)
        {
            var b = ansi.GetBytes(dll);
            ms.WriteByte((byte)(b.Length + 1));
            ms.Write(b);
            ms.WriteByte(0);
        }

        ms.WriteByte(_nextVar);

        var code = _code.ToArray();
        ms.WriteByte((byte)(code.Length & 0xFF));
        ms.WriteByte((byte)(code.Length >> 8));
        ms.Write(code);

        return ms.ToArray();
    }

    // ── Statement emitters ────────────────────────────────────────────────────

    private void EmitStmt(Stmt stmt)
    {
        switch (stmt)
        {
            case AssignStmt a:
                EmitExpr(a.Rhs);
                Emit(Op.Store, GetOrAddVar(a.Var));
                break;

            case ExprStmt e:
                EmitExpr(e.Expr);
                _code.Add(Op.Pop);
                break;

            case IfStmt ifs:
                EmitIf(ifs);
                break;

            case WhileStmt ws:
                EmitWhile(ws);
                break;
        }
    }

    private void EmitIf(IfStmt ifs)
    {
        EmitCond(ifs.Cond);
        _code.Add(Op.Jz);
        var pJz = ReserveJump();

        foreach (var s in ifs.Then) EmitStmt(s);

        if (ifs.Else is not null)
        {
            _code.Add(Op.Jmp);
            var pJmp = ReserveJump();
            PatchJump(pJz, _code.Count);
            foreach (var s in ifs.Else) EmitStmt(s);
            PatchJump(pJmp, _code.Count);
        }
        else
        {
            PatchJump(pJz, _code.Count);
        }
    }

    private void EmitWhile(WhileStmt ws)
    {
        var loopTop = _code.Count;
        EmitCond(ws.Cond);
        _code.Add(Op.Jz);
        var pJz = ReserveJump();
        foreach (var s in ws.Body) EmitStmt(s);
        _code.Add(Op.Jmp);
        var pJmp = ReserveJump();
        PatchJump(pJmp, loopTop);
        PatchJump(pJz, _code.Count);
    }

    // Conditions: emit the expr; if it's a BinExpr with a relational op we
    // already handled the CMP opcode in EmitExpr, so no extra work needed.
    private void EmitCond(Expr cond) => EmitExpr(cond);

    // ── Expression emitters ───────────────────────────────────────────────────

    private void EmitExpr(Expr expr)
    {
        switch (expr)
        {
            case IntLit i:
                _code.Add(Op.PushInt);
                _code.AddRange(BitConverter.GetBytes(i.Value)); // 8 bytes LE
                break;

            case StrLit { Wide: false } s:
                var sb = ansi.GetBytes(s.Value);
                _code.Add(Op.PushStr);
                EmitU16((ushort)sb.Length);
                _code.AddRange(sb);
                break;

            case StrLit { Wide: true } s:
                var wb = Encoding.Unicode.GetBytes(s.Value);
                _code.Add(Op.PushWStr);
                EmitU16((ushort)wb.Length);
                _code.AddRange(wb);
                break;

            case VarExpr v:
                Emit(Op.Load, GetOrAddVar(v.Name));
                break;

            case AddrExpr a:
                Emit(Op.LoadAddr, GetOrAddVar(a.Name));
                break;

            case DerefExpr d:
                Emit(Op.Deref, GetOrAddVar(d.Name));
                break;

            case ConstExpr c:
                if (!_constants.TryGetValue(c.Name, out var constVal))
                    throw new Exception($"Undefined constant '{c.Name}'");
                _code.Add(Op.PushInt);
                _code.AddRange(BitConverter.GetBytes(constVal));
                break;

            case BinExpr b:
                EmitExpr(b.Left);
                EmitExpr(b.Right);
                _code.Add(b.Op switch
                {
                    TokenKind.EqEq => Op.CmpEq,
                    TokenKind.Neq  => Op.CmpNeq,
                    TokenKind.Lt   => Op.CmpLt,
                    TokenKind.Gt   => Op.CmpGt,
                    TokenKind.Le   => Op.CmpLe,
                    TokenKind.Ge   => Op.CmpGe,
                    TokenKind.Pipe => Op.Or,
                    _ => throw new Exception($"Unknown binary op {b.Op}")
                });
                break;

            case CallExpr c:
                EmitCall(c);
                break;
        }
    }

    private void EmitCall(CallExpr call)
    {
        if (call.Dll is null)
        {
            // buf(size) pushes an out-buffer
            if (call.Func.Equals("buf", StringComparison.OrdinalIgnoreCase))
            {
                if (call.Args.Count != 1 || call.Args[0] is not IntLit sz)
                    throw new Exception("buf() requires exactly one integer size argument");
                _code.Add(Op.PushOut);
                EmitU16((ushort)sz.Value);
                return;
            }

            // arg_int/arg_str/arg_wstr consume from the runtime args buffer
            if (call.Func.Equals("arg_int", StringComparison.OrdinalIgnoreCase))
            {
                if (call.Args.Count != 0) throw new Exception("arg_int() takes no arguments");
                _code.Add(Op.ArgInt);
                return;
            }
            if (call.Func.Equals("arg_str", StringComparison.OrdinalIgnoreCase))
            {
                if (call.Args.Count != 0) throw new Exception("arg_str() takes no arguments");
                _code.Add(Op.ArgStr);
                return;
            }
            if (call.Func.Equals("arg_wstr", StringComparison.OrdinalIgnoreCase))
            {
                if (call.Args.Count != 0) throw new Exception("arg_wstr() takes no arguments");
                _code.Add(Op.ArgWStr);
                return;
            }
            if (call.Func.Equals("arg_bytes", StringComparison.OrdinalIgnoreCase))
            {
                if (call.Args.Count != 0) throw new Exception("arg_bytes() takes no arguments");
                _code.Add(Op.ArgBytes);
                return;
            }
        }

        // Push args left-to-right; beacon reads them the same way
        var argTypes = new List<byte>();
        foreach (var arg in call.Args)
        {
            EmitExpr(arg);
            argTypes.Add(CallArgType(arg));
        }

        _code.Add(Op.Call);

        // dll_idx: 0xFF = search all imports in declaration order
        if (call.Dll is not null)
            _code.Add((byte)GetOrAddDll(call.Dll));
        else if (_imports.Count > 0)
            _code.Add(0xFF);
        else
            throw new Exception($"No DLL specified for '{call.Func}' and no imports declared");

        var funcBytes = ansi.GetBytes(call.Func);
        if (funcBytes.Length > 255) throw new Exception($"Function name too long: {call.Func}");
        _code.Add((byte)funcBytes.Length);
        _code.AddRange(funcBytes);
        _code.Add((byte)call.Args.Count);
        _code.AddRange(argTypes);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Returns arg type for CALL instruction.
    // 'v' = value (no cleanup), 's' = heap string (free after), 'o' = out-buf (dump+free)
    private static byte CallArgType(Expr arg) => arg switch
    {
        StrLit                                                      => (byte)'s',
        CallExpr { Dll: null } c when
            c.Func.Equals("buf", StringComparison.OrdinalIgnoreCase) => (byte)'o',
        _                                                           => (byte)'v',
    };

    private byte GetOrAddVar(string name)
    {
        if (!_vars.TryGetValue(name, out var idx))
        {
            if (_nextVar == 64) throw new Exception("Too many variables (max 64)");
            idx = _nextVar++;
            _vars[name] = idx;
        }
        return idx;
    }

    private int GetOrAddDll(string dll)
    {
        if (!_dllMap.TryGetValue(dll, out var idx))
        {
            if (_dlls.Count == 16) throw new Exception("Too many DLLs (max 16)");
            idx = _dlls.Count;
            _dlls.Add(dll);
            _dllMap[dll] = idx;
        }
        return idx;
    }

    private void Emit(byte op, byte operand) { _code.Add(op); _code.Add(operand); }
    private void EmitU16(ushort v) { _code.Add((byte)(v & 0xFF)); _code.Add((byte)(v >> 8)); }

    private int ReserveJump() { var p = _code.Count; _code.Add(0); _code.Add(0); return p; }

    private void PatchJump(int patchPos, int target)
    {
        var offset = (short)(target - (patchPos + 2));
        _code[patchPos]     = (byte)(offset & 0xFF);
        _code[patchPos + 1] = (byte)((offset >> 8) & 0xFF);
    }
}