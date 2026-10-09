using ChunkL.Diagnostics;
using ChunkL.Syntax;

namespace ChunkL.Semantics;

/// <summary>A shared stored member, with each occurrence's wire type preserved in its declaration.</summary>
public sealed class StoredField
{
    public required string Name { get; init; }
    public required TypeReference Type { get; set; }
    public List<FieldDeclaration> Declarations { get; } = [];
    public FieldDeclaration? DefaultDeclaration { get; internal set; }
    public FieldDeclaration? FallbackDefaultDeclaration { get; internal set; }
    public Dictionary<string, GameDefaultDefinition> GameDefaults { get; } = new(StringComparer.Ordinal);
    public Expression? DefaultValue { get; internal set; }
    public bool IsDefaultSkipped { get; internal set; }
}

public sealed record GameDefaultDefinition(FieldDeclaration Declaration, GameDefault Default);

/// <summary>Type defaults apply to Fields, followed by InlineDefaults in the listed order.</summary>
public class FieldScope
{
    public List<StoredField> Fields { get; } = [];
    public List<StoredField> InlineDefaults { get; } = [];
}

public sealed class ArchiveScope : FieldScope
{
    public required ArchiveDeclaration Declaration { get; init; }
    public bool RequiresExternalVersion { get; internal set; }
}

public sealed class SemanticModel
{
    public FieldScope Class { get; } = new();
    public List<ArchiveScope> Archives { get; } = [];
    public List<ComputedAssignment> ConstructorAssignments { get; } = [];
    public Diagnostic[] Diagnostics { get; internal set; } = [];
    public bool Success => !Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
}
