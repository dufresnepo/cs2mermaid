# cs2mermaid

`cs2mermaid` is a Roslyn-powered toolchain for generating [Mermaid](https://mermaid.js.org/) class diagrams from C# solutions and projects. It contains three deliverables:

- **Cs2Mermaid.Core** – the workspace loader, metadata extractor, and Mermaid emitter.
- **Cs2Mermaid.Cli** – a `dotnet tool` providing commands to emit diagrams, compare outputs, and manage build integration toggles.
- **Cs2Mermaid.Build** – a build-transitive NuGet package that wires diagram generation into `dotnet build` with write-if-changed semantics.

## Installation

### Quick Start (NuGet.org)

```bash
# Add build integration to auto-generate diagrams
dotnet add package Cs2Mermaid.Build

# Or install the CLI tool
dotnet tool install cs2mermaid --global
```

### Alternative: GitHub Packages

Packages are also available from GitHub Packages. See [Package Consumption Guide](docs/PACKAGE-CONSUMPTION.md) for setup instructions.

## Usage

### Automatic (Build Integration)

Add `Cs2Mermaid.Build` to your project and diagrams generate automatically on build:

```bash
dotnet build
# Creates: YourProject.net9.0.mmd
```

### Manual (CLI)

```bash
cs2mermaid emit YourProject.csproj --out diagram.mmd --direction LR --min-access public
```

See the CLI help (`cs2mermaid --help`) for all available commands.

## Configuration

Customize via `Directory.Build.props` or your `.csproj`:

```xml
<PropertyGroup>
  <Cs2MermaidArgs>--direction TB --min-access internal</Cs2MermaidArgs>
  <Cs2MermaidOutputFile>docs/diagram.mmd</Cs2MermaidOutputFile>
</PropertyGroup>
```

## Package Sources

| Source | Package ID | Installation |
|--------|-----------|--------------|
| **NuGet.org** | `Cs2Mermaid.Build` | `dotnet add package Cs2Mermaid.Build` |
| **GitHub Packages** | `Cs2Mermaid.Build` | See [guide](docs/PACKAGE-CONSUMPTION.md) |
