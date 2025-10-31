# cs2mermaid

`cs2mermaid` is a Roslyn-powered toolchain for generating [Mermaid](https://mermaid.js.org/) class diagrams from C# solutions and projects. It contains three deliverables:

- **Cs2Mermaid.Core** – the workspace loader, metadata extractor, and Mermaid emitter.
- **Cs2Mermaid.Cli** – a `dotnet tool` providing commands to emit diagrams, compare outputs, and manage build integration toggles.
- **Cs2Mermaid.Build** – a build-transitive NuGet package that wires diagram generation into `dotnet build` with write-if-changed semantics.

See the CLI help (`cs2mermaid --help`) for the available commands.
