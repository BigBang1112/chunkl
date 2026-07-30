using ChunkL.Diagnostics;
using ChunkL.Lexing;
using ChunkL.Syntax;

namespace ChunkL.Parsing;

/// <summary>
/// Recursive descent parser: tokens to AST.
/// </summary>
public sealed class Parser
{
    private static readonly HashSet<string> SpecialKeywords =
    [
        "version", "versionb", "base", "return", "throw",
        "block", "switch", "skip", "assert", "loop"
    ];

    private readonly List<Token> _tokens;
    private readonly string _source;
    private readonly DiagnosticBag _diagnostics;
    private int _pos;

    public Parser(List<Token> tokens, string source, DiagnosticBag diagnostics)
    {
        _tokens = tokens;
        _source = source;
        _diagnostics = diagnostics;
    }

    private Token Current => _pos < _tokens.Count ? _tokens[_pos] : _tokens[_tokens.Count - 1];
    private Token Peek(int offset = 1) =>
        _pos + offset < _tokens.Count ? _tokens[_pos + offset] : _tokens[_tokens.Count - 1];

    private Token Advance()
    {
        var token = Current;
        _pos++;
        return token;
    }

    private Token Expect(TokenKind kind)
    {
        if (Current.Kind == kind)
            return Advance();

        var token = Current;
        _diagnostics.ReportError($"Expected {kind}, got {Current.Kind} '{Current.Text}'", Current.Position);
        Advance();
        return token;
    }

    private void SkipNewlines()
    {
        while (Current.Kind == TokenKind.Newline)
            Advance();
    }

    private void SkipToNextLine()
    {
        while (Current.Kind != TokenKind.Newline && Current.Kind != TokenKind.EndOfFile)
            Advance();
        if (Current.Kind == TokenKind.Newline)
            Advance();
    }

    private SourceRange MakeRange(Token start, Token? end = null)
    {
        var endToken = end ?? _tokens[Math.Max(0, _pos - 1)];
        return new SourceRange(start.Position, new SourcePosition(endToken.Position.Line, endToken.Position.Column + endToken.Text.Length));
    }

    private Comment? TryParseTrailingComment()
    {
        if (Current.Kind == TokenKind.Comment)
        {
            var token = Advance();
            var style = token.Text.StartsWith("//") ? CommentStyle.DoubleSlash : CommentStyle.Hash;
            var text = style == CommentStyle.DoubleSlash
                ? token.Text.Substring(2).TrimStart()
                : token.Text.Substring(1).TrimStart();
            return new Comment { Text = text, Style = style, Position = MakeRange(token, token) };
        }
        return null;
    }

    private int GetIndent()
    {
        return Current.LeadingSpaces;
    }

    public ChunkLFile ParseFile()
    {
        var firstToken = Current;
        var file = new ChunkLFile
        {
            Header = ParseClassHeader()
        };

        // Skip newlines after header
        SkipNewlines();

        // Parse class attributes (lines starting with -)
        while (Current.Kind == TokenKind.Minus && GetIndent() == 0)
        {
            file.ClassAttributes.Add(ParseClassAttribute());
            SkipNewlines();
        }

        // Parse top-level declarations
        while (Current.Kind != TokenKind.EndOfFile)
        {
            SkipNewlines();
            if (Current.Kind == TokenKind.EndOfFile)
                break;

            if (Current.Kind == TokenKind.HexLiteral && GetIndent() == 0)
            {
                file.Chunks.Add(ParseChunkDeclaration());
            }
            else if (Current.Kind == TokenKind.Identifier && Current.Text == "archive" && GetIndent() == 0)
            {
                file.Archives.Add(ParseArchiveDeclaration());
            }
            else if (Current.Kind == TokenKind.Identifier && Current.Text == "enum" && GetIndent() == 0)
            {
                file.Enums.Add(ParseEnumDeclaration());
            }
            else if (Current.Kind == TokenKind.Identifier && Current.Text == "flags" && GetIndent() == 0)
            {
                file.Flags.Add(ParseFlagsDeclaration());
            }
            else if (Current.Kind == TokenKind.Comment && GetIndent() == 0)
            {
                var commentToken = Current;
                var style = commentToken.Text.StartsWith("//") ? CommentStyle.DoubleSlash : CommentStyle.Hash;
                var text = style == CommentStyle.DoubleSlash
                    ? commentToken.Text.Substring(2).TrimStart()
                    : commentToken.Text.Substring(1).TrimStart();
                file.TopLevelComments.Add(new Comment { Text = text, Style = style, Position = MakeRange(commentToken, commentToken) });
                Advance();
                SkipNewlines();
            }
            else
            {
                _diagnostics.ReportError($"Unexpected token '{Current.Text}' at top level", Current.Position);
                SkipToNextLine();
            }
        }

        file.Position = MakeRange(firstToken);
        return file;
    }

    private ClassHeader ParseClassHeader()
    {
        SkipNewlines();
        var className = Expect(TokenKind.Identifier);
        var classId = Expect(TokenKind.HexLiteral);
        var comment = TryParseTrailingComment();

        return new ClassHeader
        {
            ClassName = className.Text,
            ClassId = classId.Text,
            TrailingComment = comment,
            Position = MakeRange(className)
        };
    }

    private ClassAttribute ParseClassAttribute()
    {
        Expect(TokenKind.Minus); // -

        // Read attribute name - everything up to : or newline/comment
        var name = ReadClassAttributeName();
        string? value = null;

        if (Current.Kind == TokenKind.Colon)
        {
            Advance(); // skip :
            value = ReadClassAttributeValue();
        }

        var comment = TryParseTrailingComment();

        // consume newline
        if (Current.Kind == TokenKind.Newline)
            Advance();

        return new ClassAttribute
        {
            Name = name,
            Value = value,
            TrailingComment = comment
        };
    }

    private string ReadClassAttributeName()
    {
        var parts = new List<string>();
        while (Current.Kind == TokenKind.Identifier)
        {
            parts.Add(Current.Text);
            Advance();
            // If next is also an identifier (with no colon in between), it's a multi-word name
            // But we need to stop before : or newline
            if (Current.Kind == TokenKind.Colon || Current.Kind == TokenKind.Newline ||
                Current.Kind == TokenKind.Comment || Current.Kind == TokenKind.EndOfFile)
                break;
        }
        return string.Join(" ", parts);
    }

    private string ReadClassAttributeValue()
    {
        var parts = new List<string>();
        while (Current.Kind != TokenKind.Newline && Current.Kind != TokenKind.Comment &&
               Current.Kind != TokenKind.EndOfFile)
        {
            parts.Add(Current.Text);
            Advance();
        }
        return string.Join(" ", parts).Trim();
    }

    private ChunkDeclaration ParseChunkDeclaration()
    {
        var offsetToken = Expect(TokenKind.HexLiteral);
        var isFullId = offsetToken.Text.Length > 5; // 0xNNN = 5 chars for short, longer for full

        var offset = new ChunkOffset
        {
            HexValue = offsetToken.Text,
            IsFullId = isFullId
        };

        AttributeList? attributes = null;
        if (Current.Kind == TokenKind.OpenParen)
        {
            attributes = ParseAttributeList();
        }

        var versionQualifiers = new List<VersionQualifier>();
        if (Current.Kind == TokenKind.OpenBracket)
        {
            versionQualifiers = ParseVersionQualifiers();
        }

        var comment = TryParseTrailingComment();

        // Consume newline
        if (Current.Kind == TokenKind.Newline)
            Advance();

        // Parse body at indent >= 2
        var body = ParseBody(2);

        return new ChunkDeclaration
        {
            Offset = offset,
            Attributes = attributes,
            VersionQualifiers = versionQualifiers,
            Body = body,
            TrailingComment = comment,
            Position = MakeRange(offsetToken)
        };
    }

    private AttributeList ParseAttributeList()
    {
        Expect(TokenKind.OpenParen);
        var entries = new List<AttributeEntry>();

        while (Current.Kind != TokenKind.CloseParen && Current.Kind != TokenKind.EndOfFile)
        {
            var entry = ParseAttributeEntry();
            entries.Add(entry);

            if (Current.Kind == TokenKind.Comma)
                Advance();
        }

        Expect(TokenKind.CloseParen);

        return new AttributeList { Entries = entries };
    }

    private AttributeEntry ParseAttributeEntry()
    {
        // Read name tokens up to : or , or )
        var nameParts = new List<string>();
        while (Current.Kind != TokenKind.Colon && Current.Kind != TokenKind.Comma &&
               Current.Kind != TokenKind.CloseParen && Current.Kind != TokenKind.EndOfFile &&
               Current.Kind != TokenKind.Newline)
        {
            nameParts.Add(Current.Text);
            Advance();
        }

        string? value = null;
        if (Current.Kind == TokenKind.Colon)
        {
            Advance(); // skip :
            var valueParts = new List<string>();
            while (Current.Kind != TokenKind.Comma && Current.Kind != TokenKind.CloseParen &&
                   Current.Kind != TokenKind.EndOfFile && Current.Kind != TokenKind.Newline)
            {
                valueParts.Add(Current.Text);
                Advance();
            }
            value = string.Join(" ", valueParts).Trim();
        }

        return new AttributeEntry
        {
            Name = string.Join(" ", nameParts).Trim(),
            Value = value
        };
    }

    private List<VersionQualifier> ParseVersionQualifiers()
    {
        Expect(TokenKind.OpenBracket);
        var qualifiers = new List<VersionQualifier>();

        while (Current.Kind != TokenKind.CloseBracket && Current.Kind != TokenKind.EndOfFile)
        {
            var label = "";
            // Read label: identifiers, dots, etc. up to comma or ]
            while (Current.Kind != TokenKind.Comma && Current.Kind != TokenKind.CloseBracket &&
                   Current.Kind != TokenKind.EndOfFile)
            {
                label += Current.Text;
                Advance();
            }

            // Parse the label: might be "TM10.v3" -> label=TM10, maxVersion=3
            string qualLabel;
            int? maxVersion = null;

            var dotVIndex = label.IndexOf(".v", StringComparison.Ordinal);
            if (dotVIndex >= 0)
            {
                qualLabel = label.Substring(0, dotVIndex);
                var versionStr = label.Substring(dotVIndex + 2);
                maxVersion = int.TryParse(versionStr, out var v) ? v : null;
            }
            else
            {
                qualLabel = label;
            }

            qualifiers.Add(new VersionQualifier { Label = qualLabel.Trim(), MaxVersion = maxVersion });

            if (Current.Kind == TokenKind.Comma)
                Advance();
        }

        Expect(TokenKind.CloseBracket);
        return qualifiers;
    }

    private List<IBodyStatement> ParseBody(int expectedIndent)
    {
        var statements = new List<IBodyStatement>();

        while (true)
        {
            // Skip blank lines
            SkipNewlines();

            if (Current.Kind == TokenKind.EndOfFile)
                break;

            // Check indent level
            var indent = GetIndent();
            if (indent < expectedIndent)
                break;

            var stmt = ParseStatement(expectedIndent);
            if (stmt != null)
                statements.Add(stmt);
        }

        return statements;
    }

    private IBodyStatement? ParseStatement(int expectedIndent)
    {
        // Check for standalone comment
        if (Current.Kind == TokenKind.Comment)
        {
            var commentToken = Current;
            var style = commentToken.Text.StartsWith("//") ? CommentStyle.DoubleSlash : CommentStyle.Hash;
            var text = style == CommentStyle.DoubleSlash
                ? commentToken.Text.Substring(2).TrimStart()
                : commentToken.Text.Substring(1).TrimStart();
            var comment = new Comment { Text = text, Style = style, Position = MakeRange(commentToken, commentToken) };
            Advance();
            if (Current.Kind == TokenKind.Newline)
                Advance();
            return comment;
        }

        // Check for version condition: v\d+[+-=] or v\d+..
        if (Current.Kind == TokenKind.Identifier && IsVersionConditionStart(Current.Text))
        {
            return ParseVersionCondition(expectedIndent);
        }

        // Check for keywords
        if (Current.Kind == TokenKind.Identifier)
        {
            switch (Current.Text)
            {
                case "if":
                    return ParseIfStatement(expectedIndent);
                case "else":
                    // else should be handled by ParseIfStatement caller
                    // If we hit it here, it's a parse error
                    _diagnostics.ReportError("Unexpected 'else' without matching 'if'", Current.Position);
                    SkipToNextLine();
                    return null;
                case "return":
                    return ParseReturnStatement();
                case "throw":
                    return ParseThrowStatement();
                case "skip":
                    return ParseSkipStatement();
                case "assert":
                    return ParseAssertStatement();
                case "block":
                    return ParseBlockStatement(expectedIndent);
                case "loop":
                    return ParseLoopStatement(expectedIndent);
                case "switch":
                    return ParseSwitchStatement(expectedIndent);
            }

            // Check for computed assignment: Identifier = expr
            // where Identifier is NOT a known type
            if (IsComputedAssignment())
            {
                return ParseComputedAssignment();
            }
        }

        // Otherwise: field declaration
        return ParseFieldDeclaration();
    }

    private bool IsVersionConditionStart(string text)
    {
        // Match v\d+[+-=] or v\d+.. pattern
        if (text.Length < 2 || text[0] != 'v' || !char.IsDigit(text[1]))
            return false;

        // Check if this is "version" or "versionb" (keywords, not version conditions)
        if (text == "version" || text == "versionb")
            return false;

        // Check rest is digits
        var i = 1;
        while (i < text.Length && char.IsDigit(text[i]))
            i++;

        // Must have consumed at least one digit and then hit end
        if (i < text.Length)
            return false;

        // The token after this identifier should be +, -, =, or ..
        var next = Peek();
        return next.Kind == TokenKind.Plus || next.Kind == TokenKind.Minus ||
               next.Kind == TokenKind.Equals || next.Kind == TokenKind.DotDot;
    }

    private VersionCondition ParseVersionCondition(int currentIndent)
    {
        var token = Advance(); // vN
        var version = int.Parse(token.Text.Substring(1));

        VersionConditionKind kind;
        int? versionEnd = null;

        if (Current.Kind == TokenKind.Plus)
        {
            kind = VersionConditionKind.GreaterOrEqual;
            Advance();
        }
        else if (Current.Kind == TokenKind.Minus)
        {
            kind = VersionConditionKind.LessOrEqual;
            Advance();
        }
        else if (Current.Kind == TokenKind.Equals)
        {
            kind = VersionConditionKind.Exact;
            Advance();
        }
        else if (Current.Kind == TokenKind.DotDot)
        {
            kind = VersionConditionKind.Range;
            Advance();
            var endToken = Expect(TokenKind.IntLiteral);
            versionEnd = int.Parse(endToken.Text);
        }
        else
        {
            kind = VersionConditionKind.GreaterOrEqual;
            _diagnostics.ReportError("Expected +, -, =, or .. after version number", Current.Position);
        }

        var comment = TryParseTrailingComment();

        // Consume newline
        if (Current.Kind == TokenKind.Newline)
            Advance();

        // Parse body at deeper indent
        var body = ParseBody(currentIndent + 2);

        return new VersionCondition
        {
            Kind = kind,
            Version = version,
            VersionEnd = versionEnd,
            Body = body,
            TrailingComment = comment,
            Position = MakeRange(token)
        };
    }

    private IfStatement ParseIfStatement(int currentIndent)
    {
        var ifToken = Current;
        Advance(); // skip 'if'

        var condition = ReadFreeFormExpression();
        if (string.IsNullOrWhiteSpace(condition))
            _diagnostics.ReportError("Expected expression", Current.Position);
        var comment = TryParseTrailingComment();

        if (Current.Kind == TokenKind.Newline)
            Advance();

        var body = ParseBody(currentIndent + 2);

        var elseIfs = new List<ElseIfClause>();
        ElseClause? elseClause = null;

        // Check for else if / else at the same indent level
        while (true)
        {
            SkipNewlines();
            if (Current.Kind == TokenKind.EndOfFile)
                break;

            if (GetIndent() != currentIndent)
                break;

            if (Current.Kind == TokenKind.Identifier && Current.Text == "else")
            {
                var nextAfterElse = Peek();
                if (nextAfterElse.Kind == TokenKind.Identifier && nextAfterElse.Text == "if")
                {
                    // else if
                    Advance(); // skip 'else'
                    Advance(); // skip 'if'
                    var eifCondition = ReadFreeFormExpression();
                    if (string.IsNullOrWhiteSpace(eifCondition))
                        _diagnostics.ReportError("Expected expression", Current.Position);
                    var eifComment = TryParseTrailingComment();
                    if (Current.Kind == TokenKind.Newline)
                        Advance();
                    var eifBody = ParseBody(currentIndent + 2);
                    elseIfs.Add(new ElseIfClause
                    {
                        Condition = eifCondition,
                        Body = eifBody,
                        TrailingComment = eifComment
                    });
                }
                else
                {
                    // else
                    Advance(); // skip 'else'
                    var elseComment = TryParseTrailingComment();
                    if (Current.Kind == TokenKind.Newline)
                        Advance();
                    var elseBody = ParseBody(currentIndent + 2);
                    elseClause = new ElseClause
                    {
                        Body = elseBody,
                        TrailingComment = elseComment
                    };
                    break; // else is always last
                }
            }
            else
            {
                break;
            }
        }

        return new IfStatement
        {
            Condition = condition,
            Body = body,
            ElseIfs = elseIfs,
            Else = elseClause,
            TrailingComment = comment,
            Position = MakeRange(ifToken)
        };
    }

    private ReturnStatement ParseReturnStatement()
    {
        var startToken = Current;
        Advance(); // skip 'return'

        AttributeList? attrs = null;
        if (Current.Kind == TokenKind.OpenParen)
        {
            attrs = ParseAttributeList();
        }

        var comment = TryParseTrailingComment();
        if (Current.Kind == TokenKind.Newline)
            Advance();

        return new ReturnStatement
        {
            Attributes = attrs,
            TrailingComment = comment,
            Position = MakeRange(startToken)
        };
    }

    private ThrowStatement ParseThrowStatement()
    {
        var startToken = Current;
        Advance(); // skip 'throw'

        AttributeList? attrs = null;
        if (Current.Kind == TokenKind.OpenParen)
        {
            attrs = ParseAttributeList();
        }

        var comment = TryParseTrailingComment();
        if (Current.Kind == TokenKind.Newline)
            Advance();

        return new ThrowStatement
        {
            Attributes = attrs,
            TrailingComment = comment,
            Position = MakeRange(startToken)
        };
    }

    private SkipStatement ParseSkipStatement()
    {
        var startToken = Current;
        Advance(); // skip 'skip'
        var expr = ReadFreeFormExpressionBeforeAttrs();
        if (string.IsNullOrWhiteSpace(expr))
            _diagnostics.ReportError("Expected expression", Current.Position);

        AttributeList? attrs = null;
        if (Current.Kind == TokenKind.OpenParen)
        {
            attrs = ParseAttributeList();
        }

        var comment = TryParseTrailingComment();
        if (Current.Kind == TokenKind.Newline)
            Advance();

        return new SkipStatement
        {
            Expression = expr,
            Attributes = attrs,
            TrailingComment = comment,
            Position = MakeRange(startToken)
        };
    }

    private AssertStatement ParseAssertStatement()
    {
        var startToken = Current;
        Advance(); // skip 'assert'
        var condition = ReadFreeFormExpressionBeforeAttrs();
        if (string.IsNullOrWhiteSpace(condition))
            _diagnostics.ReportError("Expected expression", Current.Position);

        AttributeList? attrs = null;
        if (Current.Kind == TokenKind.OpenParen)
        {
            attrs = ParseAttributeList();
        }

        var comment = TryParseTrailingComment();
        if (Current.Kind == TokenKind.Newline)
            Advance();

        return new AssertStatement
        {
            Condition = condition,
            Attributes = attrs,
            TrailingComment = comment,
            Position = MakeRange(startToken)
        };
    }

    private BlockStatement ParseBlockStatement(int currentIndent)
    {
        var startToken = Current;
        Advance(); // skip 'block'

        AttributeList? attrs = null;
        if (Current.Kind == TokenKind.OpenParen)
        {
            attrs = ParseAttributeList();
        }

        var comment = TryParseTrailingComment();
        if (Current.Kind == TokenKind.Newline)
            Advance();

        var body = ParseBody(currentIndent + 2);

        return new BlockStatement
        {
            Attributes = attrs,
            Body = body,
            TrailingComment = comment,
            Position = MakeRange(startToken)
        };
    }

    private LoopStatement ParseLoopStatement(int currentIndent)
    {
        var startToken = Current;
        Advance(); // skip 'loop'
        var countExpr = ReadFreeFormExpression();
        if (string.IsNullOrWhiteSpace(countExpr))
            _diagnostics.ReportError("Expected expression", Current.Position);

        var comment = TryParseTrailingComment();
        if (Current.Kind == TokenKind.Newline)
            Advance();

        var body = ParseBody(currentIndent + 2);

        return new LoopStatement
        {
            CountExpression = countExpr,
            Body = body,
            TrailingComment = comment,
            Position = MakeRange(startToken)
        };
    }

    private SwitchStatement ParseSwitchStatement(int currentIndent)
    {
        var startToken = Current;
        Advance(); // skip 'switch'
        var expr = ReadFreeFormExpression();
        if (string.IsNullOrWhiteSpace(expr))
            _diagnostics.ReportError("Expected expression", Current.Position);

        var comment = TryParseTrailingComment();
        if (Current.Kind == TokenKind.Newline)
            Advance();

        var cases = new List<SwitchCase>();
        SwitchDefault? defaultCase = null;

        // Parse case/default blocks at currentIndent + 2
        while (true)
        {
            SkipNewlines();
            if (Current.Kind == TokenKind.EndOfFile)
                break;

            var indent = GetIndent();
            if (indent < currentIndent + 2)
                break;

            if (Current.Kind == TokenKind.Identifier && Current.Text == "case")
            {
                Advance(); // skip 'case'
                var caseValue = ReadFreeFormExpression();
                if (string.IsNullOrWhiteSpace(caseValue))
                    _diagnostics.ReportError("Expected expression", Current.Position);
                var caseComment = TryParseTrailingComment();
                if (Current.Kind == TokenKind.Newline)
                    Advance();
                var caseBody = ParseBody(currentIndent + 4);
                cases.Add(new SwitchCase
                {
                    Value = caseValue,
                    Body = caseBody,
                    TrailingComment = caseComment
                });
            }
            else if (Current.Kind == TokenKind.Identifier && Current.Text == "default")
            {
                Advance(); // skip 'default'
                var defComment = TryParseTrailingComment();
                if (Current.Kind == TokenKind.Newline)
                    Advance();
                var defBody = ParseBody(currentIndent + 4);
                defaultCase = new SwitchDefault
                {
                    Body = defBody,
                    TrailingComment = defComment
                };
                break;
            }
            else
            {
                break;
            }
        }

        return new SwitchStatement
        {
            Expression = expr,
            Cases = cases,
            Default = defaultCase,
            TrailingComment = comment,
            Position = MakeRange(startToken)
        };
    }

    private bool IsComputedAssignment()
    {
        // Check: Identifier = expr where Identifier is NOT a known type and NOT a special keyword
        if (Current.Kind != TokenKind.Identifier)
            return false;

        var name = Current.Text;

        // If it's a known type or special keyword, it's not a computed assignment
        if (SpecialKeywords.Contains(name))
            return false;

        // Look ahead: next meaningful token should be = (but not ==)
        var next = Peek();
        if (next.Kind != TokenKind.Equals)
            return false;

        // Additional heuristic: if followed by = and the identifier could be a type
        // (starts with uppercase or is a known class name), treat as field declaration.
        // But the spec says computed assignments don't include the type keyword,
        // so: if the identifier is PascalCase AND the thing after = looks like a field name...
        // Actually, version = 1 is a field declaration (version is a known type).
        // If identifier has < after it, it's a type with cast, so field declaration.
        // Check if after identifier there's < (type cast), * (chunk pref), ? (nullable), [] (array) - those are type modifiers.
        // For computed assignment, the pattern is strictly: Identifier = Expression

        // Actually, the key distinction from the spec:
        // "version = 1" -> field declaration (version is a type keyword)
        // "Flags = Flags & 0x1FFFF" -> computed assignment (Flags is not a type)
        // Unknown identifiers that could be class types (e.g. CGameCtnBlock) are types.
        // Computed assignments are for variables that were previously declared.
        // Heuristic: if the identifier looks like a class type (uppercase first letter)
        // AND is NOT followed by = or has type modifiers, treat as type.

        // Simplest reliable heuristic: if the identifier is NOT a known primitive and
        // the next is = (not ==), treat as computed assignment UNLESS the identifier
        // has type modifiers (< or * or ? or []) after it.
        // But for computed assignment the pattern is always "Name = expr" with nothing between Name and =.
        return true;
    }

    private ComputedAssignment ParseComputedAssignment()
    {
        var nameToken = Advance(); // identifier
        Advance(); // =
        var expr = ReadFreeFormExpression();
        var comment = TryParseTrailingComment();
        if (Current.Kind == TokenKind.Newline)
            Advance();

        return new ComputedAssignment
        {
            TargetName = nameToken.Text,
            Expression = expr,
            TrailingComment = comment,
            Position = MakeRange(nameToken)
        };
    }

    private FieldDeclaration ParseFieldDeclaration()
    {
        var startToken = Current;
        var typeRef = ParseTypeReference();

        string? name = null;
        string? defaultValue = null;
        AttributeList? attrs = null;
        var isSpecial = SpecialKeywords.Contains(typeRef.Name);

        // For special keywords like "version", "base", etc., the name is optional
        // and they might immediately have = or (attrs) or comment

        // Check for field name - it's an identifier that follows the type
        if (!isSpecial && Current.Kind == TokenKind.Identifier &&
            Current.Kind != TokenKind.EndOfFile &&
            !IsStatementTerminator())
        {
            name = Advance().Text;
        }

        // Check for default value
        if (Current.Kind == TokenKind.Equals)
        {
            Advance(); // skip =
            defaultValue = ReadFreeFormExpressionBeforeAttrs();
        }

        // Check for attribute list
        if (Current.Kind == TokenKind.OpenParen)
        {
            attrs = ParseAttributeList();
        }

        var comment = TryParseTrailingComment();
        if (Current.Kind == TokenKind.Newline)
            Advance();

        return new FieldDeclaration
        {
            Type = typeRef,
            Name = name,
            DefaultValue = defaultValue,
            Attributes = attrs,
            TrailingComment = comment,
            IsSpecialKeyword = isSpecial,
            Position = MakeRange(startToken)
        };
    }

    private bool IsStatementTerminator()
    {
        return Current.Kind == TokenKind.Newline || Current.Kind == TokenKind.Comment ||
               Current.Kind == TokenKind.EndOfFile;
    }

    private TypeReference ParseTypeReference()
    {
        // Read the type name - could be a dotted name for cross-file references
        var nameToken = Advance();
        var name = nameToken.Text;

        CastType? castTarget = null;
        var chunkPreference = false;
        var isNullable = false;
        var arrayDims = 0;
        string? fixedArrayCount = null;

        // Check for cast: <TargetType> or <Qualifier.TargetType>
        if (Current.Kind == TokenKind.LessThan)
        {
            Advance(); // <
            var castName = "";
            string? qualifier = null;

            // Read cast target tokens until >
            while (Current.Kind != TokenKind.GreaterThan && Current.Kind != TokenKind.EndOfFile &&
                   Current.Kind != TokenKind.Newline)
            {
                castName += Current.Text;
                Advance();
            }

            if (Current.Kind == TokenKind.GreaterThan)
                Advance(); // >

            // Check if castName contains a dot for qualifying type
            var dotIdx = castName.LastIndexOf('.');
            if (dotIdx >= 0)
            {
                qualifier = castName.Substring(0, dotIdx);
                castName = castName.Substring(dotIdx + 1);
            }

            castTarget = new CastType { Name = castName, QualifyingType = qualifier };
        }

        // Check for chunk preference *
        if (Current.Kind == TokenKind.Star)
        {
            chunkPreference = true;
            Advance();
        }

        // Check for nullable ?
        if (Current.Kind == TokenKind.Question)
        {
            isNullable = true;
            Advance();
        }

        // Check for array dimensions [] or [count]
        while (Current.Kind == TokenKind.OpenBracket)
        {
            Advance(); // [
            if (Current.Kind == TokenKind.CloseBracket)
            {
                Advance(); // ] â€” dynamic array
            }
            else
            {
                // Fixed-count array: [N] or [FieldName]
                var countExpr = Current.Text;
                Advance(); // consume the count token
                Expect(TokenKind.CloseBracket);
                fixedArrayCount = countExpr;
            }
            arrayDims++;
        }

        return new TypeReference
        {
            Name = name,
            CastTarget = castTarget,
            ChunkPreference = chunkPreference,
            IsNullable = isNullable,
            ArrayDimensions = arrayDims,
            FixedArrayCount = fixedArrayCount
        };
    }

    private ArchiveDeclaration ParseArchiveDeclaration()
    {
        var startToken = Current;
        Advance(); // skip 'archive'

        string? name = null;
        AttributeList? attrs = null;

        // Check for name (optional - self archive has no name)
        if (Current.Kind == TokenKind.Identifier)
        {
            name = Advance().Text;
        }

        // Check for attributes
        if (Current.Kind == TokenKind.OpenParen)
        {
            attrs = ParseAttributeList();
        }

        var comment = TryParseTrailingComment();
        if (Current.Kind == TokenKind.Newline)
            Advance();

        var body = ParseBody(2);

        return new ArchiveDeclaration
        {
            Name = name,
            Attributes = attrs,
            Body = body,
            TrailingComment = comment,
            Position = MakeRange(startToken)
        };
    }

    private EnumDeclaration ParseEnumDeclaration()
    {
        var startToken = Current;
        Advance(); // skip 'enum'
        var nameToken = Expect(TokenKind.Identifier);
        var comment = TryParseTrailingComment();
        if (Current.Kind == TokenKind.Newline)
            Advance();

        var members = new List<EnumMember>();

        while (true)
        {
            SkipNewlines();
            if (Current.Kind == TokenKind.EndOfFile)
                break;
            if (GetIndent() < 2)
                break;

            if (Current.Kind == TokenKind.DotDot || (Current.Kind == TokenKind.Dot && Peek().Kind == TokenKind.Dot))
            {
                // "..." ellipsis - skip
                while (Current.Kind == TokenKind.Dot || Current.Kind == TokenKind.DotDot)
                    Advance();
                if (Current.Kind == TokenKind.Newline)
                    Advance();
                continue;
            }

            var memberName = Expect(TokenKind.Identifier);
            string? explicitValue = null;

            if (Current.Kind == TokenKind.Equals)
            {
                Advance(); // =
                explicitValue = ReadFreeFormExpression();
            }

            var memberComment = TryParseTrailingComment();
            if (Current.Kind == TokenKind.Newline)
                Advance();

            members.Add(new EnumMember
            {
                Name = memberName.Text,
                ExplicitValue = explicitValue,
                TrailingComment = memberComment
            });
        }

        return new EnumDeclaration
        {
            Name = nameToken.Text,
            Members = members,
            TrailingComment = comment,
            Position = MakeRange(startToken)
        };
    }

    private FlagsDeclaration ParseFlagsDeclaration()
    {
        var startToken = Current;
        Advance(); // skip 'flags'
        var nameToken = Expect(TokenKind.Identifier);
        var comment = TryParseTrailingComment();
        if (Current.Kind == TokenKind.Newline)
            Advance();

        var members = new List<FlagsMember>();

        while (true)
        {
            SkipNewlines();
            if (Current.Kind == TokenKind.EndOfFile)
                break;
            if (GetIndent() < 2)
                break;

            var memberName = Expect(TokenKind.Identifier);

            // Parse bit range [N] or [N..M]
            Expect(TokenKind.OpenBracket);
            var startBit = Expect(TokenKind.IntLiteral);
            int? endBit = null;

            if (Current.Kind == TokenKind.DotDot)
            {
                Advance(); // ..
                var endToken = Expect(TokenKind.IntLiteral);
                endBit = int.Parse(endToken.Text);
            }

            Expect(TokenKind.CloseBracket);

            var memberComment = TryParseTrailingComment();
            if (Current.Kind == TokenKind.Newline)
                Advance();

            members.Add(new FlagsMember
            {
                Name = memberName.Text,
                Bits = new BitRange
                {
                    Start = int.Parse(startBit.Text),
                    End = endBit
                },
                TrailingComment = memberComment
            });
        }

        return new FlagsDeclaration
        {
            Name = nameToken.Text,
            Members = members,
            TrailingComment = comment,
            Position = MakeRange(startToken)
        };
    }

    /// <summary>
    /// Read a free-form expression from the source text using token offsets.
    /// Stops at newline, comment, or EOF.
    /// </summary>
    private string ReadFreeFormExpression()
    {
        if (Current.Kind == TokenKind.Newline || Current.Kind == TokenKind.Comment ||
            Current.Kind == TokenKind.EndOfFile)
            return "";

        var startOffset = Current.SourceOffset;
        var endOffset = startOffset;

        while (Current.Kind != TokenKind.Newline && Current.Kind != TokenKind.Comment &&
               Current.Kind != TokenKind.EndOfFile)
        {
            endOffset = Current.SourceOffset + Current.SourceLength;
            Advance();
        }

        return _source.Substring(startOffset, endOffset - startOffset).Trim();
    }

    /// <summary>
    /// Read a free-form expression, stopping before attribute list (...), comment, or newline.
    /// </summary>
    private string ReadFreeFormExpressionBeforeAttrs()
    {
        if (Current.Kind == TokenKind.Newline || Current.Kind == TokenKind.Comment ||
            Current.Kind == TokenKind.EndOfFile || Current.Kind == TokenKind.OpenParen)
            return "";

        var startOffset = Current.SourceOffset;
        var endOffset = startOffset;

        // We need to detect when we hit an attribute-list open paren.
        // An attribute list paren is one that's NOT part of the expression itself.
        // Heuristic: track paren depth. A ( at depth 0 that follows a name/value
        // and doesn't look like it continues an arithmetic expression is an attribute list.
        var parenDepth = 0;

        while (Current.Kind != TokenKind.Newline && Current.Kind != TokenKind.Comment &&
               Current.Kind != TokenKind.EndOfFile)
        {
            if (Current.Kind == TokenKind.OpenParen)
            {
                if (parenDepth == 0)
                {
                    // Check if this looks like an attribute list rather than expression grouping
                    // If the previous meaningful content ended and this ( starts attributes
                    // Look ahead: if the content inside looks like "flag" or "key: value", it's attrs
                    if (LooksLikeAttributeListAhead())
                        break;
                }
                parenDepth++;
            }
            else if (Current.Kind == TokenKind.CloseParen)
            {
                parenDepth--;
            }

            endOffset = Current.SourceOffset + Current.SourceLength;
            Advance();
        }

        return _source.Substring(startOffset, endOffset - startOffset).Trim();
    }

    private bool LooksLikeAttributeListAhead()
    {
        // Look ahead past the ( to see if it looks like "identifier" or "identifier:" pattern
        // which would indicate an attribute list
        var savedPos = _pos;
        Advance(); // skip (

        // Check if first token after ( is an identifier
        if (Current.Kind == TokenKind.Identifier)
        {
            var afterIdent = Peek();
            // If followed by : or , or ) -> attribute list
            if (afterIdent.Kind == TokenKind.Colon || afterIdent.Kind == TokenKind.Comma ||
                afterIdent.Kind == TokenKind.CloseParen)
            {
                _pos = savedPos;
                return true;
            }
        }

        _pos = savedPos;
        return false;
    }
}
