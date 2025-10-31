using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Cs2Mermaid.Core;

public static class Extractor
{
    public static async Task<SolutionIR> LoadAndExtractAsync(string slnOrProj, string minAccess, CancellationToken ct)
    {
        using var ws = await WorkspaceLoader.CreateWorkspaceAsync(ct);
        slnOrProj = WorkspaceLoader.EnsureFullPath(slnOrProj);

        var projects = new List<ProjectIR>();

        if (WorkspaceLoader.IsSolution(slnOrProj))
        {
            var sln = await ws.OpenSolutionAsync(slnOrProj, progress: null, cancellationToken: ct);
            foreach (var proj in sln.Projects.Where(p => p.Language == LanguageNames.CSharp))
            {
                var pir = await ExtractProjectAsync(proj, minAccess, ct);
                projects.Add(pir);
            }
        }
        else if (WorkspaceLoader.IsProject(slnOrProj))
        {
            var proj = await ws.OpenProjectAsync(slnOrProj, progress: null, cancellationToken: ct);
            var pir = await ExtractProjectAsync(proj, minAccess, ct);
            projects.Add(pir);
        }
        else
        {
            throw new ArgumentException("Path must be a .sln or .csproj");
        }

        return new SolutionIR(projects);
    }

    private static async Task<ProjectIR> ExtractProjectAsync(Project project, string minAccess, CancellationToken ct)
    {
        var comp = await project.GetCompilationAsync(ct) ?? throw new InvalidOperationException($"No compilation for {project.Name}");
        var minRank = AccessRank(minAccess);

        var types = new Dictionary<string, TypeIR>();
        var rels = new HashSet<RelationIR>(new RelationComparer());

        void WalkNamespace(INamespaceSymbol ns)
        {
            foreach (var t in ns.GetTypeMembers()) ProcessType(t);
            foreach (var child in ns.GetNamespaceMembers()) WalkNamespace(child);
        }

        void ProcessType(INamedTypeSymbol t)
        {
            if (t == null) return;
            if (t.Locations.All(l => !l.IsInSource)) return; // skip metadata-only
            if (Rank(t.DeclaredAccessibility) < minRank) return;

            var kind = ToKind(t);
            var ns = t.ContainingNamespace?.ToDisplayString() ?? "";
            var docId = t.GetDocumentationCommentId() ?? $"{ns}.{t.Name}";
            var tir = new TypeIR(
                docId,
                t.Name,
                ns,
                kind,
                t.DeclaredAccessibility.ToString().ToLowerInvariant(),
                t.IsAbstract,
                t.IsSealed,
                t.IsStatic);

            types[docId] = tir;

            // inheritance (skip System.Object)
            if (t.BaseType is { } bt && bt.SpecialType != SpecialType.System_Object)
            {
                var toId = bt.GetDocumentationCommentId();
                if (toId != null)
                    rels.Add(new RelationIR(docId, toId, RelationKind.Inheritance));
            }
            // interface realizations
            foreach (var itf in t.AllInterfaces)
            {
                var toId = itf.GetDocumentationCommentId();
                if (toId != null)
                    rels.Add(new RelationIR(docId, toId, RelationKind.Realization));
            }

            // nested types
            foreach (var nt in t.GetTypeMembers()) ProcessType(nt);
        }

        WalkNamespace(comp.Assembly.GlobalNamespace);

        // keep only relations to known or external types by DocId (we render unknown as stub-less edges ignored by emitter)
        var relsList = rels.ToList();

        return new ProjectIR(project.Name, types.Values.ToList(), relsList);
    }

    private static int AccessRank(string a) => a.ToLowerInvariant() switch
    {
        "public" => 4,
        "internal" => 3,
        "protected" => 2,
        "private" => 1,
        _ => 4
    };

    private static int Rank(Accessibility a) => a switch
    {
        Accessibility.Public => 4,
        Accessibility.Internal or Accessibility.ProtectedOrInternal => 3,
        Accessibility.Protected => 2,
        Accessibility.Private or Accessibility.NotApplicable => 1,
        _ => 1
    };

    private static TypeKind ToKind(INamedTypeSymbol t)
    {
        if (t.TypeKind == Microsoft.CodeAnalysis.TypeKind.Interface) return TypeKind.Interface;
        if (t.TypeKind == Microsoft.CodeAnalysis.TypeKind.Struct) return t.IsRecord ? TypeKind.RecordStruct : TypeKind.Struct;
        if (t.IsRecord) return TypeKind.RecordClass;
        return t.TypeKind switch
        {
            Microsoft.CodeAnalysis.TypeKind.Enum => TypeKind.Enum,
            Microsoft.CodeAnalysis.TypeKind.Delegate => TypeKind.Delegate,
            _ => TypeKind.Class
        };
    }

    private sealed class RelationComparer : IEqualityComparer<RelationIR>
    {
        public bool Equals(RelationIR? x, RelationIR? y)
            => x is not null && y is not null &&
               x.FromDocId == y.FromDocId &&
               x.ToDocId == y.ToDocId &&
               x.Kind == y.Kind;

        public int GetHashCode(RelationIR obj)
            => HashCode.Combine(obj.FromDocId, obj.ToDocId, obj.Kind);
    }
}
