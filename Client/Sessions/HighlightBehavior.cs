using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Client.Sessions;

internal sealed class HighlightBehavior : AvaloniaObject
{
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<HighlightBehavior, TextBlock, string?>("Text");

    public static readonly AttachedProperty<string?> SearchTermProperty =
        AvaloniaProperty.RegisterAttached<HighlightBehavior, TextBlock, string?>("SearchTerm");

    public static string? GetText(TextBlock tb) => tb.GetValue(TextProperty);
    public static void SetText(TextBlock tb, string? v) => tb.SetValue(TextProperty, v);
    public static string? GetSearchTerm(TextBlock tb) => tb.GetValue(SearchTermProperty);
    public static void SetSearchTerm(TextBlock tb, string? v) => tb.SetValue(SearchTermProperty, v);

    static HighlightBehavior()
    {
        TextProperty.Changed.AddClassHandler<TextBlock>((tb, _) => Rebuild(tb));
        SearchTermProperty.Changed.AddClassHandler<TextBlock>((tb, _) => Rebuild(tb));
    }

    private static readonly IBrush HighlightBg = new SolidColorBrush(Color.Parse("#F6C90E"));
    private static readonly IBrush HighlightFg = new SolidColorBrush(Color.Parse("#0D1117"));

    private static void Rebuild(TextBlock tb)
    {
        var text   = GetText(tb);
        var search = GetSearchTerm(tb);

        tb.Inlines?.Clear();

        if (string.IsNullOrEmpty(text)) return;

        if (string.IsNullOrEmpty(search))
        {
            tb.Inlines?.Add(new Run(text));
            return;
        }

        var pos = 0;
        while (pos < text.Length)
        {
            var idx = text.IndexOf(search, pos, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                tb.Inlines?.Add(new Run(text[pos..]));
                break;
            }

            if (idx > pos)
            {
                tb.Inlines?.Add(new Run(text[pos..idx]));
            }

            tb.Inlines?.Add(new Run(text[idx..(idx + search.Length)])
            {
                Background = HighlightBg,
                Foreground = HighlightFg
            });

            pos = idx + search.Length;
        }
    }
}