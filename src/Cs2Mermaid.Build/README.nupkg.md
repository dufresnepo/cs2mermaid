# Cs2Mermaid.Build

Automatically generate [Mermaid](https://mermaid.js.org/) class diagrams from your C# code during `dotnet build`.

## Quick Start

1. Install the package:
   ```bash
   dotnet add package Cs2Mermaid.Build
   ```

2. Build your project:
   ```bash
   dotnet build
   ```

3. Find your diagram at: `YourProject.net9.0.mmd` (or appropriate TFM)

## Features

✅ **Zero-configuration** – Works out of the box with sensible defaults  
✅ **Build integration** – Runs automatically after compilation  
✅ **Write-if-changed** – Only updates diagram when content changes  
✅ **Multi-targeting safe** – TFM-suffixed output avoids conflicts  
✅ **Highly configurable** – Control via MSBuild properties

## Configuration

Customize via `Directory.Build.props` or your `.csproj`:

```xml
<PropertyGroup>
  <!-- Disable diagram generation -->
  <Cs2MermaidEnabled>false</Cs2MermaidEnabled>
  
  <!-- Change diagram direction -->
  <Cs2MermaidArgs>--direction TB --min-access internal</Cs2MermaidArgs>
  
  <!-- Custom output path -->
  <Cs2MermaidOutputFile>$(MSBuildProjectDirectory)\docs\diagram.mmd</Cs2MermaidOutputFile>
</PropertyGroup>
```

## Requirements

- .NET SDK 8.0 or later
- `cs2mermaid` CLI tool (install via `dotnet tool install cs2mermaid`)

## Available Properties

| Property | Default | Description |
|----------|---------|-------------|
| `Cs2MermaidEnabled` | `true` | Enable/disable diagram generation |
| `Cs2MermaidCommand` | `dotnet tool run cs2mermaid` | Command to invoke the tool |
| `Cs2MermaidOutputFile` | `$(MSBuildProjectName).$(TargetFramework).mmd` | Output file path |
| `Cs2MermaidArgs` | `--direction LR --min-access public` | CLI arguments |

## Learn More

- [GitHub Repository](https://github.com/uniflow-technologies/cs2mermaid)
- [Mermaid Documentation](https://mermaid.js.org/)
- [CLI Tool Documentation](https://github.com/uniflow-technologies/cs2mermaid#cli)

