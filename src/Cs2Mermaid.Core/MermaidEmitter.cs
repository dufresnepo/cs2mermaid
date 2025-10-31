using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cs2Mermaid.Core;

public static class MermaidEmitter
{
    public static async Task EmitAsync(SolutionIR solution, string direction, string outputPath, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("classDiagram");
        if (!string.IsNullOrWhiteSpace(direction))
            sb.AppendLine($"direction {direction}");

        // Group by namespace across projects
        var allTypes = solution.Projects.SelectMany(p => p.Types).ToList();
        var nsGroups = allTypes.GroupBy(t => t.Namespace).OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var g in nsGroups)
        {
            var nsName = string.IsNullOrWhiteSpace(g.Key) ? "Global" : g.Key;
            sb.AppendLine($"namespace {EscapeNs(nsName)} {{");
            foreach (var t in g.OrderBy(t => t.Name, StringComparer.Ordinal))
            {
                var displayName = QualifiedDisplay(t);
                var stereo = Stereotype(t.Kind, t.IsAbstract, t.IsSealed, t.IsStatic);
                if (string.IsNullOrEmpty(stereo))
                    sb.AppendLine($"  class {displayName}");
                else
                    sb.AppendLine($"  class {displayName} <<{stereo}>>");
            }
            sb.AppendLine("}");
        }

        // Relations (inheritance & realization)
        var typesById = allTypes.ToDictionary(t => t.DocId, t => t);
        foreach (var p in solution.Projects)
        {
            foreach (var r in p.Relations.OrderBy(r => r.FromDocId, StringComparer.Ordinal))
            {
                if (!typesById.TryGetValue(r.FromDocId, out var from)) continue;
                var fromQ = QualifiedDisplay(from);
                string edge = r.Kind == RelationKind.Inheritance ? "<|--" : "..|>";
                // render relation even if 'to' is external by using its DocId tail
                string toLabel = typesById.TryGetValue(r.ToDocId, out var to)
                    ? QualifiedDisplay(to)
                    : EscapeNs(TailOfDocId(r.ToDocId));
                sb.AppendLine($"{from.Namespace}::{from.Name} {edge} {toLabel}");
            }
        }

        await WriteIfChangedAsync(outputPath, sb.ToString(), ct);
    }

    private static string EscapeNs(string ns) => ns.Replace("`", "").Replace(":", "::");
    private static string TailOfDocId(string docId)
    {
        // e.g., T:Namespace.Type`1
        var idx = docId.LastIndexOf(':'); var s = idx >= 0 ? docId[(idx+1)..] : docId;
        return s.Replace('.', ':'); // use :: style in Mermaid namespace for external
    }

    private static string QualifiedDisplay(TypeIR t)
    {
        var ns = string.IsNullOrWhiteSpace(t.Namespace) ? "Global" : t.Namespace;
        return $"{ns}::{t.Name}";
    }

    private static string Stereotype(TypeKind k, bool isAbstract, bool isSealed, bool isStatic) => k switch
    {
        TypeKind.Interface => "Interface",
        TypeKind.Struct => "struct",
        TypeKind.Enum => "Enumeration",
        TypeKind.Delegate => "delegate",
        TypeKind.RecordClass => "record",
        TypeKind.RecordStruct => "record,struct",
        _ => isStatic ? "static" : isAbstract ? "Abstract" : isSealed ? "sealed" : ""
    };

    // Built-in, unconditional write-if-changed with atomic replace
    private static async Task WriteIfChangedAsync(string path, string content, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var tmp = path + ".tmp";
        await File.WriteAllTextAsync(tmp, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct);

        if (File.Exists(path) && await FilesEqualAsync(path, tmp, ct))
        {
            File.Delete(tmp);
            return;
        }

        try
        {
            if (OperatingSystem.IsWindows())
                File.Replace(tmp, path, null, ignoreMetadataErrors: true);
            else
                File.Move(tmp, path, true);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    private static async Task<bool> FilesEqualAsync(string a, string b, CancellationToken ct)
    {
        var fa = new FileInfo(a); var fb = new FileInfo(b);
        if (fa.Length != fb.Length) return false;
        const int Buf = 64 * 1024;
        await using var sa = File.OpenRead(a);
        await using var sb = File.OpenRead(b);
        var ba = new byte[Buf]; var bb = new byte[Buf];
        int ra;
        while ((ra = await sa.ReadAsync(ba, 0, Buf, ct)) > 0)
        {
            var rb = await sb.ReadAsync(bb, 0, Buf, ct);
            if (ra != rb || !ba.AsSpan(0, ra).SequenceEqual(bb.AsSpan(0, rb))) return false;
        }
        return true;
    }
}
