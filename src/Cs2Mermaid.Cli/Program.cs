using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Cs2Mermaid.Core;
using Microsoft.Build.Construction;

var root = new RootCommand("cs2mermaid – C# → Mermaid generator + build toggles");

// ----- emit -----
var emitCmd = new Command("emit", "Generate Mermaid .mmd for a .sln or .csproj");
var pathArg = new Argument<string>("path") { Description = "Path to .sln or .csproj" };
var outOpt = new Option<string?>("--out", "Output .mmd (defaults next to project or <SolutionName>.mmd)");
var directionOpt = new Option<string>("--direction", "Mermaid direction: LR/TB/BT/RL");
directionOpt.SetDefaultValue("LR");
var minAccessOpt = new Option<string>("--min-access", "Minimum accessibility: public|internal|protected|private");
minAccessOpt.SetDefaultValue("public");
emitCmd.AddArgument(pathArg);
emitCmd.AddOption(outOpt);
emitCmd.AddOption(directionOpt);
emitCmd.AddOption(minAccessOpt);

emitCmd.SetHandler(async (InvocationContext ctx) =>
{
    var console = ctx.Console;
    try
    {
        var path = Path.GetFullPath(ctx.ParseResult.GetValueForArgument(pathArg));
        var output = ctx.ParseResult.GetValueForOption(outOpt);
        var direction = ctx.ParseResult.GetValueForOption(directionOpt);
        var minAccess = ctx.ParseResult.GetValueForOption(minAccessOpt) ?? "public";
        var ct = CancellationToken.None;
        var model = await Extractor.LoadAndExtractAsync(path, minAccess, ct);
        string outPath = output ?? DefaultOutputPath(path);
        await MermaidEmitter.EmitAsync(model, direction ?? "LR", outPath, ct);
        console.Out.WriteLine($"wrote: {outPath}");
        ctx.ExitCode = 0;
    }
    catch (Exception ex)
    {
        console.Error.WriteLine($"[emit] error: {ex.Message}");
        ctx.ExitCode = 3;
    }
});
root.AddCommand(emitCmd);

// ----- diff -----
var diffCmd = new Command("diff", "Compare two .mmd files; exit 5 if different");
var oldArg = new Argument<string>("oldFile");
var newArg = new Argument<string>("newFile");
diffCmd.AddArgument(oldArg);
diffCmd.AddArgument(newArg);
diffCmd.SetHandler((InvocationContext ctx) =>
{
    var console = ctx.Console;
    var a = Path.GetFullPath(ctx.ParseResult.GetValueForArgument(oldArg));
    var b = Path.GetFullPath(ctx.ParseResult.GetValueForArgument(newArg));
    if (!File.Exists(a) || !File.Exists(b))
    {
        console.Error.WriteLine("Both files must exist.");
        ctx.ExitCode = 1;
        return;
    }
    var same = FilesEqual(a, b);
    if (same)
    {
        console.Out.WriteLine("No drift.");
        ctx.ExitCode = 0;
    }
    else
    {
        console.Out.WriteLine("Drift detected.");
        ctx.ExitCode = 5;
    }
});
root.AddCommand(diffCmd);

// ----- install/enable/disable/status/uninstall -----
var installCmd = new Command("install", "Add Cs2Mermaid.Build to Directory.Build.props (repo-wide)");
var versionOpt = new Option<string?>("--version", "Package version (default 1.1.0)");
installCmd.AddOption(versionOpt);
installCmd.SetHandler((InvocationContext ctx) =>
{
    var console = ctx.Console;
    var version = ctx.ParseResult.GetValueForOption(versionOpt);
    var chosen = string.IsNullOrWhiteSpace(version) ? "1.1.0" : version!;
    var propsPath = FindOrCreateDirectoryBuildProps();
    UpsertPackageReference(propsPath, "Cs2Mermaid.Build", chosen);
    UpsertProperty(propsPath, "Cs2MermaidEnabled", "true");
    console.Out.WriteLine($"Installed Cs2Mermaid.Build {chosen} in {propsPath}");
    ctx.ExitCode = 0;
});
root.AddCommand(installCmd);

var enableCmd = new Command("enable", "Enable generation (repo-wide, --solution, or --project)");
var disableCmd = new Command("disable", "Disable generation (repo-wide, --solution, or --project)");
var solutionOpt = new Option<bool>("--solution", "Apply to the .sln in current folder (all projects)");
var projectOpt = new Option<string?>("--project", "Path to a specific .csproj");
enableCmd.AddOption(solutionOpt);
enableCmd.AddOption(projectOpt);
disableCmd.AddOption(solutionOpt);
disableCmd.AddOption(projectOpt);

enableCmd.SetHandler(ctx =>
{
    Toggle(true, ctx.ParseResult.GetValueForOption(solutionOpt), ctx.ParseResult.GetValueForOption(projectOpt), ctx.Console, ctx);
});
root.AddCommand(enableCmd);

disableCmd.SetHandler(ctx =>
{
    Toggle(false, ctx.ParseResult.GetValueForOption(solutionOpt), ctx.ParseResult.GetValueForOption(projectOpt), ctx.Console, ctx);
});
root.AddCommand(disableCmd);

var statusCmd = new Command("status", "Show current install and enablement state");
statusCmd.SetHandler(ctx =>
{
    var console = ctx.Console;
    var props = FindDirectoryBuildProps();
    if (props == null)
    {
        console.Out.WriteLine("Directory.Build.props: (none)");
    }
    else
    {
        var text = File.ReadAllText(props);
        var hasPkg = text.Contains("Cs2Mermaid.Build");
        var enabled = ReadProperty(text, "Cs2MermaidEnabled");
        console.Out.WriteLine($"Directory.Build.props: {props}");
        console.Out.WriteLine($"Package: {(hasPkg ? "Cs2Mermaid.Build" : "(not installed)")}");
        console.Out.WriteLine($"Repo default: {(enabled ?? "(unset)")}");
    }

    var sln = FindSolutionInCurrentDir();
    if (sln != null)
    {
        console.Out.WriteLine($"Solution: {sln}");
        foreach (var proj in SolutionFile.Parse(sln).ProjectsInOrder)
        {
            if (proj.ProjectType != SolutionProjectType.KnownToBeMSBuildFormat) continue;
            var csproj = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sln)!, proj.RelativePath));
            var state = ProjectEnablement(csproj);
            console.Out.WriteLine($"  {csproj}  -> {state}");
        }
    }
    ctx.ExitCode = 0;
});
root.AddCommand(statusCmd);

var uninstallCmd = new Command("uninstall", "Remove Cs2Mermaid.Build from Directory.Build.props (repo-wide)");
uninstallCmd.SetHandler(ctx =>
{
    var console = ctx.Console;
    var props = FindDirectoryBuildProps();
    if (props is null)
    {
        console.Out.WriteLine("Nothing to uninstall.");
        ctx.ExitCode = 0;
        return;
    }
    var doc = XDocument.Load(props);
    var ns = doc.Root!.Name.Namespace;

    foreach (var ig in doc.Root!.Elements(ns + "ItemGroup"))
        foreach (var pr in ig.Elements(ns + "PackageReference").Where(e => (string?)e.Attribute("Include") == "Cs2Mermaid.Build").ToList())
            pr.Remove();

    doc.Save(props);
    console.Out.WriteLine($"Removed Cs2Mermaid.Build from {props}");
    ctx.ExitCode = 0;
});
root.AddCommand(uninstallCmd);

return await root.InvokeAsync(args);


// ----------------- helpers -----------------
static string DefaultOutputPath(string path)
{
    if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
    {
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        return Path.Combine(dir, $"{name}.mmd");
    }
    var pdir = Path.GetDirectoryName(path)!;
    var pname = Path.GetFileNameWithoutExtension(path);
    // We don't know TFM here; let MSBuild-driven runs append it. For CLI, keep it plain:
    return Path.Combine(pdir, $"{pname}.mmd");
}

static bool FilesEqual(string a, string b)
{
    var fa = new FileInfo(a); var fb = new FileInfo(b);
    if (fa.Length != fb.Length) return false;
    using var sa = File.OpenRead(a);
    using var sb = File.OpenRead(b);
    Span<byte> ba = stackalloc byte[64 * 1024];
    var bb = new byte[ba.Length];
    int ra;
    while ((ra = sa.Read(ba)) > 0)
    {
        var rb = sb.Read(bb, 0, ra);
        if (rb != ra) return false;
        if (!ba[..ra].SequenceEqual(bb.AsSpan(0, ra))) return false;
    }
    return true;
}

static string? FindSolutionInCurrentDir()
{
    var dir = Directory.GetCurrentDirectory();
    var slns = Directory.GetFiles(dir, "*.sln");
    return slns.FirstOrDefault();
}

static string FindOrCreateDirectoryBuildProps()
{
    var start = Directory.GetCurrentDirectory();
    var root = FindRepoRoot(start) ?? start;
    var props = Path.Combine(root, "Directory.Build.props");
    if (!File.Exists(props))
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(props, """
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
  </PropertyGroup>
  <ItemGroup>
  </ItemGroup>
</Project>
""");
    }
    return props;
}

static string? FindDirectoryBuildProps()
{
    var start = Directory.GetCurrentDirectory();
    var cur = new DirectoryInfo(start);
    while (cur != null)
    {
        var candidate = Path.Combine(cur.FullName, "Directory.Build.props");
        if (File.Exists(candidate)) return candidate;
        cur = cur.Parent;
    }
    return null;
}

static string? FindRepoRoot(string start)
{
    var cur = new DirectoryInfo(start);
    while (cur != null)
    {
        if (Directory.Exists(Path.Combine(cur.FullName, ".git"))) return cur.FullName;
        cur = cur.Parent;
    }
    return null;
}

static void Toggle(bool enable, bool forSolution, string? project, IConsole console, InvocationContext ctx)
{
    if (!string.IsNullOrEmpty(project))
    {
        SetProjectEnablement(project!, enable);
        console.Out.WriteLine($"{(enable ? "Enabled" : "Disabled")} in {Path.GetFullPath(project!)}");
        ctx.ExitCode = 0;
        return;
    }
    if (forSolution)
    {
        var sln = FindSolutionInCurrentDir();
        if (sln is null)
        {
            console.Error.WriteLine("No .sln found in current directory.");
            ctx.ExitCode = 1;
            return;
        }
        foreach (var p in SolutionFile.Parse(sln).ProjectsInOrder)
        {
            if (p.ProjectType != SolutionProjectType.KnownToBeMSBuildFormat) continue;
            var csproj = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sln)!, p.RelativePath));
            SetProjectEnablement(csproj, enable);
            console.Out.WriteLine($"{(enable ? "Enabled" : "Disabled")} in {csproj}");
        }
        ctx.ExitCode = 0;
        return;
    }
    var props = FindOrCreateDirectoryBuildProps();
    UpsertProperty(props, "Cs2MermaidEnabled", enable ? "true" : "false");
    console.Out.WriteLine($"Repo default set to {(enable ? "ENABLED" : "DISABLED")} in {props}");
    ctx.ExitCode = 0;
}

static string ProjectEnablement(string csprojPath)
{
    if (!File.Exists(csprojPath)) return "(missing)";
    var text = File.ReadAllText(csprojPath);
    var local = ReadProperty(text, "Cs2MermaidEnabled");
    return local ?? "(inherits)";
}

static void SetProjectEnablement(string csprojPath, bool enable)
{
    var doc = XDocument.Load(csprojPath);
    var ns = doc.Root!.Name.Namespace;
    // Find first non-conditional PropertyGroup or create one
    var pg = doc.Root!.Elements(ns + "PropertyGroup").FirstOrDefault(e => e.Attribute("Condition") == null);
    if (pg == null)
    {
        pg = new XElement(ns + "PropertyGroup");
        doc.Root!.Add(pg);
    }
    var node = pg.Elements(ns + "Cs2MermaidEnabled").FirstOrDefault();
    if (node == null)
    {
        node = new XElement(ns + "Cs2MermaidEnabled", enable ? "true" : "false");
        pg.Add(node);
    }
    else
    {
        node.Value = enable ? "true" : "false";
    }
    // write-if-changed
    var tmp = csprojPath + ".tmp";
    doc.Save(tmp);
    if (File.Exists(csprojPath) && FilesEqual(csprojPath, tmp)) File.Delete(tmp);
    else
    {
        File.Copy(tmp, csprojPath, true);
        File.Delete(tmp);
    }
}

static void UpsertPackageReference(string propsPath, string packageId, string version)
{
    var doc = XDocument.Load(propsPath);
    var ns = doc.Root!.Name.Namespace;

    var ig = doc.Root!.Elements(ns + "ItemGroup").FirstOrDefault()
          ?? new XElement(ns + "ItemGroup");

    if (ig.Parent == null) doc.Root!.Add(ig);

    var pr = ig.Elements(ns + "PackageReference").FirstOrDefault(e => (string?)e.Attribute("Include") == packageId);
    if (pr == null)
    {
        pr = new XElement(ns + "PackageReference",
            new XAttribute("Include", packageId),
            new XAttribute("Version", version),
            new XAttribute("PrivateAssets", "all"));
        ig.Add(pr);
    }
    else
    {
        pr.SetAttributeValue("Version", version);
        pr.SetAttributeValue("PrivateAssets", "all");
    }
    doc.Save(propsPath);
}

static void UpsertProperty(string propsPath, string name, string value)
{
    var doc = XDocument.Load(propsPath);
    var ns = doc.Root!.Name.Namespace;

    var pg = doc.Root!.Elements(ns + "PropertyGroup").FirstOrDefault()
          ?? new XElement(ns + "PropertyGroup");
    if (pg.Parent == null) doc.Root!.Add(pg);

    var node = pg.Elements(ns + name).FirstOrDefault();
    if (node == null)
    {
        node = new XElement(ns + name, value);
        pg.Add(node);
    }
    else
    {
        node.Value = value;
    }

    doc.Save(propsPath);
}

static string? ReadProperty(string xml, string name)
{
    try
    {
        var doc = XDocument.Parse(xml);
        var ns = doc.Root!.Name.Namespace;
        var node = doc.Root!.Elements(ns + "PropertyGroup").Elements(ns + name).FirstOrDefault();
        return node?.Value;
    }
    catch
    {
        return null;
    }
}
