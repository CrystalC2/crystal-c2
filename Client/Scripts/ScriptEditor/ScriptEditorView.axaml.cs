using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace Client.Scripts.ScriptEditor;

public partial class ScriptEditorView : Window
{
    private CompletionWindow? _completionWindow;

    private static readonly string[] s_keywords  = ["import", "if", "else", "while"];
    private static readonly string[] s_builtins  = ["BeaconPrintf", "BeaconOutput", "BeaconIsAdmin", "buf", "arg_int", "arg_str", "arg_wstr", "arg_bytes"];
    private static readonly string[] s_dlls      =
    [
        "kernel32.dll", "ntdll.dll", "user32.dll", "advapi32.dll",
        "ws2_32.dll",   "shell32.dll", "ole32.dll", "msvcrt.dll", "beacon"
    ];

    public ScriptEditorView()
    {
        InitializeComponent();
        LoadSyntaxHighlighting();
        DataContextChanged += OnDataContextChanged;
    }

    private void LoadSyntaxHighlighting()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://CrystalClient/Assets/railgun.xshd"));
            using var reader = new XmlTextReader(stream);
            Editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
        catch { /* highlighting is cosmetic; swallow load failures */ }

        // Style the line-number margin to match the dark theme
        Editor.Options.HighlightCurrentLine    = true;
        Editor.Options.EnableHyperlinks        = false;
        Editor.Options.EnableEmailHyperlinks   = false;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not ScriptEditorViewModel vm) return;

        vm.GetSource        = () => Editor.Text;
        vm.SetSource        = text => { Editor.Text = text; };
        vm.RequestOpenFile  = OpenFileDialogAsync;
        vm.RequestSaveFile  = SaveFileDialogAsync;

        Editor.TextArea.KeyDown   += OnEditorKeyDown;
        Editor.TextArea.TextInput += OnEditorTextInput;
    }

    // ── Completion ────────────────────────────────────────────────────────────

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ShowCompletions();
            e.Handled = true;
        }
    }

    private void OnEditorTextInput(object? sender, TextInputEventArgs e)
    {
        // Auto-trigger after the first letter of an identifier or a '$'
        if (e.Text is { Length: 1 } t && (char.IsLetter(t[0]) || t[0] == '$' || t[0] == '_'))
        {
            // Post so the character is already in the document when we scan
            Dispatcher.UIThread.Post(ShowCompletions, DispatcherPriority.Background);
        }
    }

    private void ShowCompletions()
    {
        if (_completionWindow is not null) return;

        var textArea = Editor.TextArea;
        var offset   = textArea.Caret.Offset;
        var doc      = Editor.Document;

        // Find the beginning of the current word (supports $var, foo, kernel32.dll)
        var wordStart = offset;
        while (wordStart > 0)
        {
            var ch = doc.GetCharAt(wordStart - 1);
            if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '$' && ch != '.') break;
            wordStart--;
        }

        var prefix = doc.GetText(wordStart, offset - wordStart);
        if (string.IsNullOrEmpty(prefix)) return;

        var items = BuildCompletionList(prefix);
        if (items.Count == 0) return;

        _completionWindow = new CompletionWindow(textArea) { StartOffset = wordStart };
        foreach (var item in items)
            _completionWindow.CompletionList.CompletionData.Add(item);

        _completionWindow.Closed += (_, _) => _completionWindow = null;
        _completionWindow.Show();
    }

    private List<ICompletionData> BuildCompletionList(string prefix)
    {
        var result   = new List<ICompletionData>();
        var isDollar = prefix.StartsWith('$');
        var lower    = isDollar ? prefix[1..].ToLowerInvariant() : prefix.ToLowerInvariant();

        if (isDollar)
        {
            // Variable names: scan existing $name tokens in the document
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in Regex.Matches(Editor.Document.Text, @"\$([a-zA-Z_][a-zA-Z0-9_]*)"))
            {
                var name = m.Groups[1].Value;
                if (seen.Add(name) && name.StartsWith(lower, StringComparison.OrdinalIgnoreCase))
                    result.Add(new CompletionItem($"${name}", "variable"));
            }
            return result;
        }

        foreach (var kw in s_keywords)
            if (kw.StartsWith(lower, StringComparison.OrdinalIgnoreCase))
                result.Add(new CompletionItem(kw, "keyword"));

        foreach (var fn in s_builtins)
            if (fn.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                result.Add(new CompletionItem(fn, "beacon function"));

        foreach (var dll in s_dlls)
            if (dll.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                result.Add(new CompletionItem(dll, "module"));

        return result;
    }

    // ── File dialogs ──────────────────────────────────────────────────────────

    private async Task<(string Path, string Content)?> OpenFileDialogAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title         = "Open Railgun Script",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Railgun Script") { Patterns = ["*.rgs"] },
                new FilePickerFileType("Text Files")     { Patterns = ["*.txt"] },
                new FilePickerFileType("All Files")      { Patterns = ["*"]     }
            ]
        });

        if (files.Count == 0) return null;

        var file = files[0];
        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream);
        return (file.Name, await reader.ReadToEndAsync());
    }

    private async Task<bool> SaveFileDialogAsync(string source)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title              = "Save Railgun Script",
            SuggestedFileName  = "script.rgs",
            FileTypeChoices    =
            [
                new FilePickerFileType("Railgun Script") { Patterns = ["*.rgs"] },
                new FilePickerFileType("All Files")      { Patterns = ["*"]     }
            ]
        });

        if (file is null) return false;

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(source);
        return true;
    }
}

// ── Completion item ───────────────────────────────────────────────────────────

internal sealed class CompletionItem(string text, string description) : ICompletionData
{
    public Avalonia.Media.IImage? Image => null;
    public string Text    { get; } = text;
    public object Content => Text;
    public object Description => description;
    public double Priority => 0;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
    }
}