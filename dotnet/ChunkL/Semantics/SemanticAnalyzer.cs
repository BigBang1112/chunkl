using ChunkL.Diagnostics;
using ChunkL.Lexing;
using ChunkL.Syntax;

namespace ChunkL.Semantics;

/// <summary>
/// Resolves local stored members and initialization order. External types and callers remain the host's responsibility.
/// </summary>
public static class SemanticAnalyzer
{
    public static SemanticModel Analyze(ChunkLFile file)
    {
        var diagnostics = new DiagnosticBag();
        var model = new SemanticModel();
        var classDeclarations = new List<FieldDeclaration>();
        foreach (var declaration in file.GetDeclarationsInSourceOrder())
        {
            if (declaration is ChunkDeclaration chunk)
                classDeclarations.AddRange(Walk(chunk.Body).OfType<FieldDeclaration>());
            else if (declaration is ArchiveDeclaration archive && archive.Name == null)
                classDeclarations.AddRange(Walk(archive.Body).OfType<FieldDeclaration>());
        }
        if (file.Constructor != null)
            model.ConstructorAssignments.AddRange(file.Constructor.Body.OfType<ComputedAssignment>());
        ResolveFields(model.Class, classDeclarations, model.ConstructorAssignments.Select(a => a.TargetName), diagnostics);
        foreach (var archive in file.Archives)
        {
            var scope = new ArchiveScope { Declaration = archive };
            if (archive.Name != null)
                ResolveFields(scope, Walk(archive.Body).OfType<FieldDeclaration>().ToList(), [], diagnostics);
            scope.RequiresExternalVersion = VersionFlow(archive.Body, false, archive, file, diagnostics,
                new HashSet<ArchiveDeclaration> { archive }).NeedsExternal;
            model.Archives.Add(scope);
        }
        foreach (var chunk in file.Chunks)
        {
            var flow = VersionFlow(chunk.Body, false, null, file, diagnostics, []);
            if (flow.NeedsExternal)
                diagnostics.ReportError("Chunk version blocks require a preceding version or versionb field", chunk.Position.Start);
        }
        ValidateProperties(file, model.Class, model.Archives, diagnostics);
        model.Diagnostics = diagnostics.ToArray();
        return model;
    }

    private static void ResolveFields(FieldScope scope, List<FieldDeclaration> declarations,
        IEnumerable<string> constructorTargets, DiagnosticBag diagnostics)
    {
        var targets = new HashSet<string>(constructorTargets, StringComparer.Ordinal);
        var fields = new Dictionary<string, StoredField>(StringComparer.Ordinal);
        var defaults = new List<StoredField>();
        foreach (var declaration in declarations.Where(d => d.Name != null && !d.IsSpecialKeyword))
        {
            var name = declaration.Name!;
            if (!fields.TryGetValue(name, out var field))
            {
                field = new StoredField { Name = name, Type = declaration.Type, IsDefaultSkipped = targets.Contains(name) };
                fields.Add(name, field);
                scope.Fields.Add(field);
            }
            field.Declarations.Add(declaration);
            if (declaration.DefaultValue != null)
            {
                if (field.DefaultDeclaration == null)
                {
                    field.DefaultDeclaration = declaration;
                    defaults.Add(field);
                }
                else if (DefaultSyntax(field.DefaultDeclaration) != DefaultSyntax(declaration))
                    diagnostics.ReportError($"Conflicting inline defaults for shared field '{name}'", declaration.Position.Start);
            }
        }
        foreach (var field in scope.Fields)
        {
            var types = field.Declarations.Select(d => d.Type).ToList();
            var first = types[0];
            var compatibleModifiers = types.All(t => ModifiersMatch(first, t));
            var integerTypes = first.CastTarget == null && first.ArrayDimensions == 0 &&
                types.All(t => IntegerInfo(t.Name) != null);
            var storedType = integerTypes
                ? types.OrderByDescending(t => IntegerInfo(t.Name)!.Value.Width).FirstOrDefault(candidate =>
                    types.All(t => IntegerContains(candidate.Name, t.Name)))
                : types.All(t => t.Name == first.Name) ? first : null;
            if (!compatibleModifiers || storedType == null)
                diagnostics.ReportError($"Incompatible declarations for shared field '{field.Name}'",
                    field.Declarations[field.Declarations.Count - 1].Position.Start);
            else
                field.Type = storedType;
        }
        scope.InlineDefaults.AddRange(defaults.Where(f => !f.IsDefaultSkipped));
    }

    private static string DefaultSyntax(FieldDeclaration field)
    {
        var current = ChunkLParser.WriteExpression(field.DefaultValue!);
        if (field.DefaultValueSource is string source)
        {
            var parsed = new Parsing.ExpressionParser(new Lexer(source).Tokenize(), 0).Parse();
            if (TokenSyntax(ChunkLParser.WriteExpression(parsed)) == TokenSyntax(current))
                return TokenSyntax(source);
        }
        return TokenSyntax(current);
    }

    private static string TokenSyntax(string text) => string.Join("\u001f", new Lexer(text).Tokenize()
        .Where(t => t.Kind is not (TokenKind.Newline or TokenKind.Comment or TokenKind.EndOfFile))
        .Select(t => $"{t.Kind}:{t.Text}"));

    private static bool ModifiersMatch(TypeReference a, TypeReference b) =>
        a.CastTarget?.Name == b.CastTarget?.Name && a.CastTarget?.QualifyingType == b.CastTarget?.QualifyingType &&
        a.IsNullable == b.IsNullable && a.ChunkPreference == b.ChunkPreference &&
        ArrayShape(a).SequenceEqual(ArrayShape(b));

    private static IEnumerable<string?> ArrayShape(TypeReference type) =>
        Enumerable.Range(0, type.ArrayDimensions).Select(i =>
        {
            var count = type.ArrayCounts.Count == type.ArrayDimensions ? type.ArrayCounts[i] :
                i == 0 ? type.FixedArrayCount : null;
            return count == null ? null : TokenSyntax(count);
        });

    private static (int Width, bool Signed)? IntegerInfo(string name) => name switch
    {
        "sbyte" => (8, true), "byte" => (8, false), "short" => (16, true), "ushort" => (16, false),
        "int" => (32, true), "uint" => (32, false), "long" => (64, true), "ulong" => (64, false), _ => null
    };

    private static bool IntegerContains(string target, string source)
    {
        var a = IntegerInfo(target)!.Value;
        var b = IntegerInfo(source)!.Value;
        return a.Signed == b.Signed ? a.Width >= b.Width : a.Signed && a.Width > b.Width;
    }

    internal static IEnumerable<IBodyStatement> Walk(IEnumerable<IBodyStatement> body)
    {
        foreach (var statement in body)
        {
            yield return statement;
            foreach (var child in ChildBodies(statement))
                foreach (var nested in Walk(child)) yield return nested;
        }
    }

    private static IEnumerable<List<IBodyStatement>> ChildBodies(IBodyStatement statement)
    {
        switch (statement)
        {
            case VersionCondition condition: yield return condition.Body; break;
            case BlockStatement block: yield return block.Body; break;
            case LoopStatement loop: yield return loop.Body; break;
            case WhileStatement loop: yield return loop.Body; break;
            case IfStatement branch:
                yield return branch.Body;
                foreach (var clause in branch.ElseIfs) yield return clause.Body;
                if (branch.Else != null) yield return branch.Else.Body;
                break;
            case SwitchStatement selection:
                foreach (var clause in selection.Cases) yield return clause.Body;
                if (selection.Default != null) yield return selection.Default.Body;
                break;
        }
    }

    private readonly record struct VersionState(bool Established, bool FallsThrough, bool NeedsExternal);

    private static VersionState VersionFlow(IEnumerable<IBodyStatement> body, bool established,
        ArchiveDeclaration? archive, ChunkLFile file, DiagnosticBag diagnostics, HashSet<ArchiveDeclaration> inheritance)
    {
        var needsExternal = false;
        foreach (var statement in body)
        {
            switch (statement)
            {
                case FieldDeclaration field when field.Type.Name is "version" or "versionb":
                    established = true;
                    break;
                case FieldDeclaration field when field.Type.Name == "base" && archive != null:
                    var parentName = archive.Attributes?.Entries.FirstOrDefault(e => e.Name == "inherits")?.Value;
                    var parent = file.Archives.FirstOrDefault(a => a.Name != null && a.Name == parentName);
                    if (parent != null)
                    {
                        if (!inheritance.Add(parent))
                        {
                            diagnostics.ReportError("Cyclic archive inheritance", archive.Position.Start);
                            break;
                        }
                        var parentFlow = VersionFlow(parent.Body, established, parent, file, diagnostics, inheritance);
                        inheritance.Remove(parent);
                        needsExternal |= parentFlow.NeedsExternal;
                        established = parentFlow.Established;
                    }
                    break;
                case ReturnStatement or ThrowStatement:
                    return new VersionState(established, false, needsExternal);
                case BlockStatement block:
                    var blockFlow = VersionFlow(block.Body, established, archive, file, diagnostics, inheritance);
                    needsExternal |= blockFlow.NeedsExternal;
                    established = blockFlow.Established;
                    if (!blockFlow.FallsThrough) return new VersionState(established, false, needsExternal);
                    break;
                case VersionCondition condition:
                    needsExternal |= !established;
                    needsExternal |= VersionFlow(condition.Body, established, archive, file, diagnostics, inheritance).NeedsExternal;
                    break;
                case IfStatement branch:
                    var branches = ChildBodies(branch).Select(b => VersionFlow(b, established, archive, file, diagnostics, inheritance)).ToList();
                    if (branch.Else == null) branches.Add(new VersionState(established, true, false));
                    needsExternal |= branches.Any(b => b.NeedsExternal);
                    var continuing = branches.Where(b => b.FallsThrough).ToList();
                    if (continuing.Count == 0) return new VersionState(established, false, needsExternal);
                    established = continuing.All(b => b.Established);
                    break;
                case SwitchStatement selection:
                    var cases = ChildBodies(selection).Select(b => VersionFlow(b, established, archive, file, diagnostics, inheritance)).ToList();
                    if (selection.Default == null) cases.Add(new VersionState(established, true, false));
                    needsExternal |= cases.Any(b => b.NeedsExternal);
                    var remaining = cases.Where(b => b.FallsThrough).ToList();
                    if (remaining.Count == 0) return new VersionState(established, false, needsExternal);
                    established = remaining.All(b => b.Established);
                    break;
                case LoopStatement or WhileStatement:
                    foreach (var child in ChildBodies(statement))
                        needsExternal |= VersionFlow(child, established, archive, file, diagnostics, inheritance).NeedsExternal;
                    break;
            }
        }
        return new VersionState(established, true, needsExternal);
    }

    private static void ValidateProperties(ChunkLFile file, FieldScope scope, IEnumerable<ArchiveScope> archives, DiagnosticBag diagnostics)
    {
        var fields = scope.Fields.ToDictionary(f => f.Name, StringComparer.Ordinal);
        var propertiesAllowed = true;
        var properties = new Dictionary<string, PropertyDeclaration>(StringComparer.Ordinal);
        foreach (var property in file.Properties)
        {
            if (fields.ContainsKey(property.Name) || properties.ContainsKey(property.Name))
                diagnostics.ReportError($"Duplicate class member '{property.Name}'", property.Position.Start);
            else properties.Add(property.Name, property);
        }
        var calls = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var property in properties.Values)
        {
            if (property.Getter != null)
            {
                var edges = new HashSet<string>();
                calls.Add("get:" + property.Name, edges);
                CheckReads(property.Getter.Expression, property.Getter.Position.Start, null, edges, true);
                CheckCompatible(property.Type, property.Getter.Expression, property.Getter.Position.Start, null);
            }
            if (property.Setter != null)
            {
                var edges = new HashSet<string>();
                calls.Add("set:" + property.Name, edges);
                CheckBody(property.Setter.Body, property.Type, edges);
            }
        }
        if (file.Constructor != null) CheckBody(file.Constructor.Body, null, []);
        foreach (var chunk in file.Chunks) CheckPropertyUses(chunk.Body);
        foreach (var archive in file.Archives.Where(a => a.Name == null)) CheckPropertyUses(archive.Body);
        foreach (var start in calls.Keys)
            if (Reaches(start, start, new HashSet<string>()))
                diagnostics.ReportError($"Recursive property accessor '{start}'", properties[start.Substring(4)].Position.Start);
        propertiesAllowed = false;
        foreach (var archive in archives.Where(a => a.Declaration.Name != null))
        {
            fields = archive.Fields.ToDictionary(f => f.Name, StringComparer.Ordinal);
            CheckPropertyUses(archive.Declaration.Body);
        }

        bool Reaches(string current, string target, HashSet<string> visited)
        {
            if (!visited.Add(current) || !calls.TryGetValue(current, out var edges)) return false;
            return edges.Any(edge => edge == target || Reaches(edge, target, visited));
        }

        void CheckTarget(ComputedAssignment assignment, HashSet<string> edges, bool requireDeclared, TypeReference? incomingType = null)
        {
            if (fields.TryGetValue(assignment.TargetName, out var field))
                CheckCompatible(field.Type, assignment.Expression, assignment.Position.Start, incomingType);
            else if (properties.TryGetValue(assignment.TargetName, out var target))
            {
                if (!propertiesAllowed)
                    diagnostics.ReportError("Class properties cannot be used in named archives", assignment.Position.Start);
                else if (target.Setter == null)
                    diagnostics.ReportError($"Property '{target.Name}' is read-only", assignment.Position.Start);
                else edges.Add("set:" + target.Name);
                CheckCompatible(target.Type, assignment.Expression, assignment.Position.Start, incomingType);
            }
            else if (requireDeclared && !fields.ContainsKey(assignment.TargetName) && !file.ClassAttributes.Any(a => a.Name == "inherits"))
                diagnostics.ReportError($"Unknown class field or property '{assignment.TargetName}'", assignment.Position.Start);
        }

        void CheckReads(Expression expression, SourcePosition position, TypeReference? incomingType, HashSet<string> edges, bool requireDeclared = false)
        {
            foreach (var name in Identifiers(expression))
            {
                if (fields.ContainsKey(name) || incomingType != null && name == "value") continue;
                if (properties.TryGetValue(name, out var target))
                {
                    if (!propertiesAllowed)
                        diagnostics.ReportError("Class properties cannot be used in named archives", position);
                    else if (target.Getter == null)
                        diagnostics.ReportError($"Property '{name}' is write-only", position);
                    else edges.Add("get:" + name);
                }
                else if (requireDeclared && !fields.ContainsKey(name) && !file.ClassAttributes.Any(a => a.Name == "inherits"))
                    diagnostics.ReportError($"Unknown class field or readable property '{name}'", position);
            }
            CheckPatterns(expression, position, incomingType);
        }

        void CheckPatterns(Expression expression, SourcePosition position, TypeReference? incomingType)
        {
            if (expression is PatternTestExpression test)
            {
                var kind = InferKind(test.Value, incomingType);
                if (kind != null && kind is not ("string" or "array" or "null") &&
                    PatternConstants(test.Pattern).Any(c => c is LiteralExpression { Kind: LiteralKind.Empty }))
                    diagnostics.ReportError("The empty pattern requires a string or array", position);
                if (kind != null && kind is not ("string" or "null") &&
                    PatternConstants(test.Pattern).Any(c => c is LiteralExpression { Kind: LiteralKind.String, Value: "\"\"" }))
                    diagnostics.ReportError("The empty string pattern requires a string", position);
            }
            if (expression is BinaryExpression { Operator: BinaryOperator.LogicalAnd or BinaryOperator.LogicalOr } logical)
            {
                if (InferKind(logical.Left, incomingType) is string leftKind && leftKind != "bool" ||
                    InferKind(logical.Right, incomingType) is string rightKind && rightKind != "bool")
                    diagnostics.ReportError("Logical operators require boolean operands", position);
            }
            if (expression is UnaryExpression { Operator: UnaryOperator.Not } negated &&
                InferKind(negated.Operand, incomingType) is string operandKind && operandKind != "bool")
                diagnostics.ReportError("Logical operators require boolean operands", position);
            foreach (var child in ExpressionChildren(expression)) CheckPatterns(child, position, incomingType);
        }

        void CheckBody(IEnumerable<IBodyStatement> body, TypeReference? incomingType, HashSet<string> edges)
        {
            foreach (var statement in Walk(body))
            {
                if (statement is ComputedAssignment assignment) CheckTarget(assignment, edges, true, incomingType);
                foreach (var expression in StatementExpressions(statement))
                    CheckReads(expression, ((SyntaxNode)statement).Position.Start, incomingType, edges, true);
            }
        }

        void CheckPropertyUses(IEnumerable<IBodyStatement> body)
        {
            foreach (var statement in Walk(body))
            {
                if (statement is ComputedAssignment assignment) CheckTarget(assignment, [], false);
                foreach (var expression in StatementExpressions(statement))
                    CheckReads(expression, ((SyntaxNode)statement).Position.Start, null, []);
            }
        }

        string? TypeKind(TypeReference type) => type.ArrayDimensions > 0 ? "array" : type.CastTarget != null ? "other" : type.Name switch
        {
            "bool" => "bool", "string" => "string", "float" or "double" or "timefloat" => "number",
            _ when IntegerInfo(type.Name) != null || type.Name == "timeint" => "number", _ => "other"
        };

        string? InferKind(Expression expression, TypeReference? incomingType) => expression switch
        {
            IdentifierExpression id when id.Name == "value" && incomingType != null => TypeKind(incomingType),
            IdentifierExpression id when fields.TryGetValue(id.Name, out var field) => TypeKind(field.Type),
            IdentifierExpression id when propertiesAllowed && properties.TryGetValue(id.Name, out var property) => TypeKind(property.Type),
            LiteralExpression literal => literal.Kind switch
            {
                LiteralKind.True or LiteralKind.False => "bool", LiteralKind.String => "string", LiteralKind.Null => "null",
                LiteralKind.Integer or LiteralKind.Hex or LiteralKind.Float => "number", _ => null
            },
            ParenthesizedExpression grouped => InferKind(grouped.Inner, incomingType),
            PatternTestExpression => "bool",
            ScopedIdentifierExpression or TupleExpression => "other",
            UnaryExpression { Operator: UnaryOperator.Not } => "bool",
            UnaryExpression unary => InferKind(unary.Operand, incomingType),
            BinaryExpression binary => binary.Operator is BinaryOperator.LogicalAnd or BinaryOperator.LogicalOr or
                BinaryOperator.Equal or BinaryOperator.NotEqual or BinaryOperator.LessThan or BinaryOperator.GreaterThan or
                BinaryOperator.LessOrEqual or BinaryOperator.GreaterOrEqual ? "bool" : InferKind(binary.Left, incomingType),
            _ => null
        };

        void CheckCompatible(TypeReference target, Expression expression, SourcePosition position, TypeReference? incomingType)
        {
            var expected = TypeKind(target);
            var actual = InferKind(expression, incomingType);
            if (expected == null || actual == null || expected == "other" || actual == "other") return;
            if (actual == "null" && (target.IsNullable || expected is "string" or "array")) return;
            if (expected != actual)
                diagnostics.ReportError("Expression result is incompatible with the target type", position);
        }
    }

    private static IEnumerable<Expression> StatementExpressions(IBodyStatement statement) => statement switch
    {
        FieldDeclaration { DefaultValue: not null } field => [field.DefaultValue],
        ComputedAssignment assignment => [assignment.Expression], IfStatement branch => [branch.Condition, .. branch.ElseIfs.Select(c => c.Condition)],
        SwitchStatement selection => [selection.Expression, .. selection.Cases.Select(c => c.Value)],
        WhileStatement loop => [loop.Condition], LoopStatement loop => [loop.CountExpression],
        AssertStatement assertion => [assertion.Condition], SkipStatement skip => [skip.Expression], _ => []
    };

    private static IEnumerable<string> Identifiers(Expression expression)
    {
        if (expression is IdentifierExpression identifier) yield return identifier.Name;
        foreach (var child in ExpressionChildren(expression))
            foreach (var name in Identifiers(child)) yield return name;
    }

    private static IEnumerable<Expression> ExpressionChildren(Expression expression) => expression switch
    {
        UnaryExpression unary => [unary.Operand], BinaryExpression binary => [binary.Left, binary.Right],
        ParenthesizedExpression grouped => [grouped.Inner], TupleExpression tuple => tuple.Elements,
        PatternTestExpression test => [test.Value, .. PatternConstants(test.Pattern)], _ => []
    };

    private static IEnumerable<Expression> PatternConstants(Pattern pattern) => pattern switch
    {
        ConstantPattern constant => [constant.Value], NotPattern negated => PatternConstants(negated.Operand),
        ParenthesizedPattern grouped => PatternConstants(grouped.Inner),
        BinaryPattern binary => PatternConstants(binary.Left).Concat(PatternConstants(binary.Right)), _ => []
    };
}
