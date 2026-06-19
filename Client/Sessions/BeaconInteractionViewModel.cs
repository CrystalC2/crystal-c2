using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Client.Scripts;
using Client.Services;
using CrystalC2.Tasks;
using ReactiveUI;

namespace Client.Sessions;

internal sealed class BeaconInteractionViewModel : ReactiveObject, IAsyncDisposable
{
    private readonly TaskGrpcService _service;
    private readonly ScriptEngine _engine;
    private readonly CancellationTokenSource _cts = new();
    private readonly Queue<TaskEntryViewModel> _pending = new();
    private readonly Dictionary<uint, TaskEntryViewModel> _byId = new();
    private readonly List<string> _history = [];
    private int _historyIndex = -1;
    private string _draft = string.Empty;

    private List<string>? _pathCandidates;
    private int _pathCandidateIndex;
    private string? _pathCompletionBase;
    private bool _applyingCompletion;

    public BeaconSessionViewModel Session { get; }
    public ObservableCollection<TaskEntryViewModel> Entries { get; } = [];
    public ObservableCollection<TaskEntryViewModel> FilteredEntries { get; } = [];

    public string Search
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            RefreshFilter();
            foreach (var entry in Entries)
            {
                entry.HighlightTerm = value;
            }
        }
    } = string.Empty;

    public string Title => $"{Session.UserDisplay}@{Session.Computer} : {Session.Process} ({Session.Pid})";

    public string Input
    {
        get;
        set
        {
            if (!_applyingCompletion) _pathCandidates = null;
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Suggestion));
        }
    } = string.Empty;

    public string? Suggestion
    {
        get
        {
            var input = Input;

            if (string.IsNullOrEmpty(input) || input.Contains(' ')) return null;

            return _engine
                .GetCommandNames()
                .FirstOrDefault(n => n.StartsWith(input, StringComparison.OrdinalIgnoreCase)
                                     && !n.Equals(input, StringComparison.OrdinalIgnoreCase));
        }
    }

    public string Watermark
    {
        get
        {
            var parts = _engine.GetCommandNames();
            return string.Join(" · ", parts);
        }
    }

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SubmitCommand { get; }

    public Action<BeaconSessionViewModel>? OpenScriptEditor { get; set; }

    public void TryComplete()
    {
        // Advance through an existing path-candidate cycle.
        if (_pathCandidates is { Count: > 0 })
        {
            _pathCandidateIndex = (_pathCandidateIndex + 1) % _pathCandidates.Count;
            ApplyPathCandidate();
            return;
        }

        // Try path completion on the last token.
        var input = Input;
        var spaceIdx = input.LastIndexOf(' ');
        if (spaceIdx >= 0)
        {
            var lastToken = input[(spaceIdx + 1)..].Trim('"');
            if (LooksLikePath(lastToken))
            {
                var candidates = GetPathCandidates(lastToken);
                if (candidates.Count > 0)
                {
                    _pathCandidates = candidates;
                    _pathCandidateIndex = 0;
                    _pathCompletionBase = input[..(spaceIdx + 1)];
                    ApplyPathCandidate();
                    return;
                }
            }
        }

        // Fall back to command-name ghost completion.
        if (Suggestion is { } suggestion)
            Input = suggestion + " ";
    }

    private void ApplyPathCandidate()
    {
        var candidate = _pathCandidates![_pathCandidateIndex];
        var quoted = candidate.Contains(' ') ? $"\"{candidate}\"" : candidate;
        _applyingCompletion = true;
        Input = _pathCompletionBase + quoted;
        _applyingCompletion = false;
    }

    private static bool LooksLikePath(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        return token.Contains('\\') || token.Contains('/')
            || (token.Length >= 2 && char.IsLetter(token[0]) && token[1] == ':');
    }

    private static List<string> GetPathCandidates(string partial)
    {
        try
        {
            var lastSep = partial.LastIndexOfAny(['\\', '/']);
            string dir, prefix;
            if (lastSep < 0)
            {
                dir = ".";
                prefix = partial;
            }
            else
            {
                dir = partial[..(lastSep + 1)];
                prefix = partial[(lastSep + 1)..];
            }

            if (!Directory.Exists(dir)) return [];

            return [.. Directory
                .EnumerateFileSystemEntries(dir, prefix + "*")
                .Select(p => Directory.Exists(p) ? p + "\\" : p)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)];
        }
        catch
        {
            return [];
        }
    }

    public void ClearEntries()
    {
        var toRemove = Entries
            .Where(e => e.Status is not (CrystalC2.Tasks.TaskStatus.Pending or CrystalC2.Tasks.TaskStatus.Tasked))
            .ToList();

        foreach (var entry in toRemove)
        {
            if (entry.TaskId.HasValue)
            {
                _byId.Remove(entry.TaskId.Value);
            }
            
            Entries.Remove(entry);
        }

        RefreshFilter();
    }

    public BeaconInteractionViewModel(BeaconSessionViewModel session, TaskGrpcService service, ScriptEngine engine)
    {
        Session = session;
        _service = service;
        _engine = engine;

        _engine.CommandsChanged += OnCommandsChanged;
        _engine.BeaconOutput += OnBeaconOutput;

        Entries.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is null) return;
            foreach (TaskEntryViewModel entry in e.NewItems)
            {
                entry.HighlightTerm = Search;
                if (Matches(entry)) FilteredEntries.Add(entry);
            }
        };

        var canSubmit = this.WhenAnyValue(x => x.Input, s => !string.IsNullOrWhiteSpace(s));
        SubmitCommand = ReactiveCommand.CreateFromTask(SubmitAsync, canSubmit);

        _ = _service.StartAsync(OnResponse, _cts.Token);
    }

    private void OnBeaconOutput(uint beaconId, string message)
    {
        if (beaconId != Session.BeaconId) return;

        Dispatcher.UIThread.Post(() =>
        {
            var entry = Entries.LastOrDefault();
            if (entry is not null)
                entry.Output = entry.Output is null ? message : entry.Output + "\n" + message;
        });
    }

    private void OnCommandsChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            this.RaisePropertyChanged(nameof(Watermark));
            this.RaisePropertyChanged(nameof(Suggestion));
        });

    private Task SubmitAsync()
    {
        var text = Input.Trim();

        if (string.IsNullOrEmpty(text)) return Task.CompletedTask;

        Input = string.Empty;

        if (_history.Count == 0 || _history[^1] != text)
        {
            _history.Add(text);
        }
        
        _historyIndex = -1;
        _draft = string.Empty;

        var parts = CommandParser.SplitArgs(text);

        var entry = new TaskEntryViewModel(text);
        Entries.Add(entry);

        if (_engine.IsCommand(parts[0]))
        {
            _engine.ExecuteCommand(parts[0], Session.BeaconId, parts[1..],
                localOutput: output =>
                {
                    entry.Output = output;
                    entry.Status = CrystalC2.Tasks.TaskStatus.Complete;
                },
                sendTask: async req =>
                {
                    entry.Observer = _engine.ConsumeNextObserver();
                    _pending.Enqueue(entry);
                    await _service.SendAsync(req);
                });
        }
        else
        {
            entry.Status = CrystalC2.Tasks.TaskStatus.Unknown;
            entry.Output = $"unknown command: {parts[0]}";
        }

        return Task.CompletedTask;
    }

    private bool Matches(TaskEntryViewModel entry)
    {
        if (string.IsNullOrEmpty(Search)) return true;
        
        return entry.Command.Contains(Search, StringComparison.OrdinalIgnoreCase)
               || (entry.Output?.Contains(Search, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private void RefreshFilter()
    {
        FilteredEntries.Clear();
        
        foreach (var entry in Entries)
        {
            if (Matches(entry))
            {
                FilteredEntries.Add(entry);
            }
        }
    }

    public void HistoryUp()
    {
        if (_history.Count == 0) return;

        switch (_historyIndex)
        {
            case -1:
            {
                _draft = Input;
                _historyIndex = _history.Count - 1;
                break;
            }
            case > 0:
            {
                _historyIndex--;
                break;
            }
        }

        Input = _history[_historyIndex];
    }

    public void HistoryDown()
    {
        if (_historyIndex == -1) return;

        if (_historyIndex < _history.Count - 1)
        {
            _historyIndex++;
            Input = _history[_historyIndex];
        }
        else
        {
            _historyIndex = -1;
            Input = _draft;
            _draft = string.Empty;
        }
    }

    private void OnResponse(TaskResponse response)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_byId.TryGetValue(response.TaskId, out var entry))
            {
                if (!_pending.TryDequeue(out entry)) return;

                entry.TaskId = response.TaskId;
                _byId[response.TaskId] = entry;
            }

            entry.Status = response.Status;

            if (response.HasOutput)
            {
                var chunk = Encoding.UTF8.GetString(response.Output.ToByteArray());
                entry.Output = entry.Output is null ? chunk : entry.Output + chunk;
            }

            entry.Observer?.Invoke(response);
        });
    }

    public async ValueTask DisposeAsync()
    {
        _engine.CommandsChanged -= OnCommandsChanged;
        _engine.BeaconOutput -= OnBeaconOutput;
        await _cts.CancelAsync();
        _cts.Dispose();
        await _service.DisposeAsync();
    }
}