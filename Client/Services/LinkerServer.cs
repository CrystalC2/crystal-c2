using System;
using System.Diagnostics;
using System.IO;

namespace Client.Services;

internal static class LinkerServer
{
    private static Process? _process;

    internal static void Start()
    {
        var jarPath = Path.Combine(AppContext.BaseDirectory, "crystalpalace.jar");

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = $"-Dcrystalpalace.verbose=false -cp \"{jarPath}\" crystalpalace.server.LinkerServer",
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _process.Start();
    }

    internal static void Stop()
    {
        if (_process is null || _process.HasExited) return;

        try
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(3000);
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }
}