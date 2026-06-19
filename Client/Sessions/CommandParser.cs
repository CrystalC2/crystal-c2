using System.Collections.Generic;

namespace Client.Sessions;

internal static class CommandParser
{
    // Splits a command line on whitespace while keeping double-quoted spans together.
    // The quotes themselves are preserved so PackRailgunData can strip them:
    //   z:"Hello World"  →  single token  z:"Hello World"
    //   z:Hello          →  single token  z:Hello
    internal static string[] SplitArgs(string text)
    {
        var args = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuote = false;

        foreach (var ch in text)
        {
            switch (ch)
            {
                case '"':
                    inQuote = !inQuote;
                    current.Append(ch);
                    break;
                case ' ' or '\t' when !inQuote:
                    if (current.Length > 0)
                    {
                        args.Add(current.ToString());
                        current.Clear();
                    }
                    break;
                default:
                    current.Append(ch);
                    break;
            }
        }

        if (current.Length > 0)
            args.Add(current.ToString());

        return [.. args];
    }
}