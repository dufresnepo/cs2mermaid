using System;
using System.Collections.Generic;

namespace Cs2Mermaid.Core;

public enum TypeKind { Class, Struct, Interface, Enum, Delegate, RecordClass, RecordStruct }

public sealed record TypeIR(
    string DocId, string Name, string Namespace, TypeKind Kind,
    string Accessibility, bool IsAbstract, bool IsSealed, bool IsStatic);

public enum RelationKind { Inheritance, Realization }

public sealed record RelationIR(string FromDocId, string ToDocId, RelationKind Kind);

public sealed record ProjectIR(string Name, IReadOnlyList<TypeIR> Types, IReadOnlyList<RelationIR> Relations);

public sealed record SolutionIR(IReadOnlyList<ProjectIR> Projects);
