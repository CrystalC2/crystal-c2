using System;

namespace Client.Scripts;

internal sealed record ScriptLogEntry(string Message, DateTimeOffset Timestamp)
{
    public string Display => $"[{Timestamp:HH:mm:ss}] {Message}";
}