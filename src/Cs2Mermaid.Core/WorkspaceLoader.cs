using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;

namespace Cs2Mermaid.Core;

public static class WorkspaceLoader
{
    private static volatile bool _registered;

    private static void EnsureMsbuildRegistered()
    {
        if (_registered) return;
        try
        {
            MSBuildLocator.RegisterDefaults();
            _registered = true;
        }
        catch (InvalidOperationException)
        {
            // already registered by host; ignore
            _registered = true;
        }
    }

    public static async Task<MSBuildWorkspace> CreateWorkspaceAsync(CancellationToken ct)
    {
        EnsureMsbuildRegistered();
        var props = new System.Collections.Generic.Dictionary<string, string>
        {
            ["AlwaysCompileMarkupFilesInSeparateDomain"] = "true", // VS friendliness
        };
        var ws = MSBuildWorkspace.Create(props);
        ws.WorkspaceFailed += (_, e) =>
        {
            // keep going; collect diagnostics upstream if you want
            if (e.Diagnostic.Kind == Microsoft.CodeAnalysis.WorkspaceDiagnosticKind.Failure)
                Console.Error.WriteLine($"[load] {e.Diagnostic.Message}");
        };
        await Task.Yield();
        return ws;
    }

    public static bool IsSolution(string path) => path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase);
    public static bool IsProject(string path) => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

    public static string EnsureFullPath(string path) => Path.GetFullPath(path);
}
