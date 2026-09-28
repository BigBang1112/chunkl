using ChunkL.Lexing;
using ChunkL.Syntax;
using ChunkL.Diagnostics;

namespace ChunkL.Parsing;

internal sealed class ExpressionParser
{
    private readonly List<Token> _tokens;
    private int _pos;
    private readonly DiagnosticBag? _diagnostics;

    public ExpressionParser(List<Token> tokens, int startPos, DiagnosticBag? diagnostics = null)
    {
        _tokens = tokens;
        _pos = startPos;
        _diagnostics = diagnostics;
    }

    public int Position => _pos;

    private Token Current => _pos < _tokens.Count ? _tokens[_pos] : _tokens[_tokens.Count - 1];
    private Token Peek(int offset = 1) =>
        _pos + offset < _tokens.Count ? _tokens[_pos + offset] : _tokens[_tokens.Count - 1];

    private Token Advance()
    {
        var token = Current;
        _pos++;
        return token;
    }

    private bool AtEnd =>
        Current.Kind is TokenKind.EndOfFile or TokenKind.Newline or TokenKind.Comment;

    public Expression Parse() => ParseLogicalOr();

    private Expression ParseLogicalOr()
    {
        var left = ParseLogicalAnd();
        while (!AtEnd && (Current.Kind == TokenKind.PipePipe || IsKeyword("or")))
        {
            Advance();
            var right = ParseLogicalAnd();
            left = new BinaryExpression { Left = left, Operator = BinaryOperator.LogicalOr, Right = right };
        }
        return left;
    }

    private Expression ParseLogicalAnd()
    {
        var left = ParseEquality();
        while (!AtEnd && (Current.Kind == TokenKind.AmpersandAmpersand || IsKeyword("and")))
        {
            Advance();
            var right = ParseEquality();
            left = new BinaryExpression { Left = left, Operator = BinaryOperator.LogicalAnd, Right = right };
        }
        return left;
    }

    private Expression ParseEquality()
    {
        var left = ParseComparison();
        while (!AtEnd)
        {
            BinaryOperator op;
            if (Current.Kind == TokenKind.EqualsEquals)
                op = BinaryOperator.Equal;
            else if (Current.Kind == TokenKind.BangEquals)
                op = BinaryOperator.NotEqual;
            else
                break;
            Advance();
            var right = ParseComparison();
            left = new BinaryExpression { Left = left, Operator = op, Right = right };
        }
        return left;
    }

    private Expression ParseComparison()
    {
        var left = ParseBitwiseOr();
        while (!AtEnd)
        {
            if (IsKeyword("is"))
            {
                Advance();
                left = new PatternTestExpression { Value = left, Pattern = ParsePatternOr() };
                continue;
            }
            BinaryOperator op;
            if (Current.Kind == TokenKind.LessThan)
                op = BinaryOperator.LessThan;
            else if (Current.Kind == TokenKind.GreaterThan)
                op = BinaryOperator.GreaterThan;
            else if (Current.Kind == TokenKind.LessThanEquals)
                op = BinaryOperator.LessOrEqual;
            else if (Current.Kind == TokenKind.GreaterThanEquals)
                op = BinaryOperator.GreaterOrEqual;
            else
                break;
            Advance();
            var right = ParseBitwiseOr();
            left = new BinaryExpression { Left = left, Operator = op, Right = right };
        }
        return left;
    }

    private bool IsKeyword(string keyword) => Current.Kind == TokenKind.Identifier && Current.Text == keyword;

    private bool CanStartPattern(int offset)
    {
        var token = Peek(offset);
        if (token.Kind is TokenKind.IntLiteral or TokenKind.HexLiteral or TokenKind.StringLiteral)
            return true;
        if (token.Kind == TokenKind.OpenParen || (token.Kind == TokenKind.Identifier && token.Text == "not"))
            return offset < _tokens.Count - _pos && CanStartPattern(offset + 1);
        return token.Kind == TokenKind.Identifier &&
            (token.Text is "true" or "false" or "null" or "empty" || Peek(offset + 1).Kind == TokenKind.ColonColon);
    }

    private Pattern ParsePatternOr()
    {
        var left = ParsePatternAnd();
        while (IsKeyword("or") && CanStartPattern(1))
        {
            Advance();
            left = new BinaryPattern { Left = left, Operator = PatternOperator.Or, Right = ParsePatternAnd() };
        }
        return left;
    }

    private Pattern ParsePatternAnd()
    {
        var left = ParsePatternNot();
        while (IsKeyword("and") && CanStartPattern(1))
        {
            Advance();
            left = new BinaryPattern { Left = left, Operator = PatternOperator.And, Right = ParsePatternNot() };
        }
        return left;
    }

    private Pattern ParsePatternNot()
    {
        if (IsKeyword("not"))
        {
            Advance();
            return new NotPattern { Operand = ParsePatternNot() };
        }
        if (Current.Kind == TokenKind.OpenParen)
        {
            Advance();
            var inner = ParsePatternOr();
            ExpectCloseParen();
            return new ParenthesizedPattern { Inner = inner };
        }
        if (!CanStartPattern(0))
            _diagnostics?.ReportError("Expected a literal or scoped identifier pattern", Current.Position);
        return new ConstantPattern { Value = ParsePrimary() };
    }

    private void ExpectCloseParen()
    {
        if (Current.Kind == TokenKind.CloseParen)
            Advance();
        else
            _diagnostics?.ReportError("Expected ')'", Current.Position);
    }

    private Expression ParseBitwiseOr()
    {
        var left = ParseBitwiseXor();
        while (!AtEnd && Current.Kind == TokenKind.Pipe)
        {
            Advance();
            var right = ParseBitwiseXor();
            left = new BinaryExpression { Left = left, Operator = BinaryOperator.BitwiseOr, Right = right };
        }
        return left;
    }

    private Expression ParseBitwiseXor()
    {
        var left = ParseBitwiseAnd();
        while (!AtEnd && Current.Kind == TokenKind.Caret)
        {
            Advance();
            var right = ParseBitwiseAnd();
            left = new BinaryExpression { Left = left, Operator = BinaryOperator.BitwiseXor, Right = right };
        }
        return left;
    }

    private Expression ParseBitwiseAnd()
    {
        var left = ParseShift();
        while (!AtEnd && Current.Kind == TokenKind.Ampersand)
        {
            Advance();
            var right = ParseShift();
            left = new BinaryExpression { Left = left, Operator = BinaryOperator.BitwiseAnd, Right = right };
        }
        return left;
    }

    private Expression ParseShift()
    {
        var left = ParseAdditive();
        while (!AtEnd)
        {
            BinaryOperator op;
            if (Current.Kind == TokenKind.LessLess)
                op = BinaryOperator.ShiftLeft;
            else if (Current.Kind == TokenKind.GreaterGreater)
                op = BinaryOperator.ShiftRight;
            else
                break;
            Advance();
            var right = ParseAdditive();
            left = new BinaryExpression { Left = left, Operator = op, Right = right };
        }
        return left;
    }

    private Expression ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (!AtEnd)
        {
            BinaryOperator op;
            if (Current.Kind == TokenKind.Plus)
                op = BinaryOperator.Add;
            else if (Current.Kind == TokenKind.Minus)
                op = BinaryOperator.Subtract;
            else
                break;
            Advance();
            var right = ParseMultiplicative();
            left = new BinaryExpression { Left = left, Operator = op, Right = right };
        }
        return left;
    }

    private Expression ParseMultiplicative()
    {
        var left = ParseUnary();
        while (!AtEnd)
        {
            BinaryOperator op;
            if (Current.Kind == TokenKind.Star)
                op = BinaryOperator.Multiply;
            else if (Current.Kind == TokenKind.Slash)
                op = BinaryOperator.Divide;
            else
                break;
            Advance();
            var right = ParseUnary();
            left = new BinaryExpression { Left = left, Operator = op, Right = right };
        }
        return left;
    }

    private Expression ParseUnary()
    {
        if (AtEnd) return ParsePrimary();

        UnaryOperator? op = Current.Kind switch
        {
            TokenKind.Bang => UnaryOperator.Not,
            TokenKind.Tilde => UnaryOperator.BitwiseNot,
            TokenKind.Minus => UnaryOperator.Negate,
            _ => null
        };

        if (op is { } unaryOp)
        {
            Advance();
            var operand = ParseUnary();
            return new UnaryExpression { Operator = unaryOp, Operand = operand };
        }

        return ParsePrimary();
    }

    private Expression ParsePrimary()
    {
        switch (Current.Kind)
        {
            case TokenKind.OpenParen:
            {
                Advance(); // (
                var first = ParseLogicalOr();
                if (Current.Kind == TokenKind.Comma)
                {
                    var elements = new List<Expression> { first };
                    while (Current.Kind == TokenKind.Comma)
                    {
                        Advance();
                        elements.Add(ParseLogicalOr());
                    }
                    ExpectCloseParen();
                    return new TupleExpression { Elements = elements };
                }
                ExpectCloseParen();
                return new ParenthesizedExpression { Inner = first };
            }
            case TokenKind.IntLiteral:
            {
                var token = Advance();
                var kind = token.Text.Contains('.') || token.Text.EndsWith("f") || token.Text.EndsWith("F")
                    ? LiteralKind.Float
                    : LiteralKind.Integer;
                return new LiteralExpression { Kind = kind, Value = token.Text };
            }
            case TokenKind.HexLiteral:
            {
                var token = Advance();
                return new LiteralExpression { Kind = LiteralKind.Hex, Value = token.Text };
            }
            case TokenKind.StringLiteral:
            {
                var token = Advance();
                var slashCount = 0;
                for (var i = token.Text.Length - 2; i >= 0 && token.Text[i] == '\\'; i--) slashCount++;
                if (token.Text.Length < 2 || !token.Text.EndsWith("\"", StringComparison.Ordinal) || slashCount % 2 != 0)
                    _diagnostics?.ReportError("Unterminated string literal", token.Position);
                return new LiteralExpression { Kind = LiteralKind.String, Value = token.Text };
            }
            case TokenKind.Identifier:
            {
                var token = Advance();
                return token.Text switch
                {
                    "true" => new LiteralExpression { Kind = LiteralKind.True, Value = "true" },
                    "false" => new LiteralExpression { Kind = LiteralKind.False, Value = "false" },
                    "null" => new LiteralExpression { Kind = LiteralKind.Null, Value = "null" },
                    "empty" => new LiteralExpression { Kind = LiteralKind.Empty, Value = "empty" },
                    _ when Current.Kind == TokenKind.ColonColon => ParseScopedIdentifier(token.Text),
                    _ => new IdentifierExpression { Name = token.Text }
                };
            }
            default:
            {
                var token = Current;
                _diagnostics?.ReportError($"Expected an expression, got '{token.Text}'", token.Position);
                if (!AtEnd) Advance();
                return new IdentifierExpression { Name = token.Text };
            }
        }
    }

    private ScopedIdentifierExpression ParseScopedIdentifier(string qualifier)
    {
        Advance(); // ::
        if (Current.Kind != TokenKind.Identifier)
            _diagnostics?.ReportError("Expected an enum or flags member after '::'", Current.Position);
        var member = Current.Kind == TokenKind.Identifier ? Advance().Text : "";
        return new ScopedIdentifierExpression { Qualifier = qualifier, Name = member };
    }

    /// <summary>
    /// Parse, stopping before a trailing attribute list <c>(...)</c>.
    /// Uses the same heuristic as the main parser to distinguish expression parens from attribute parens.
    /// </summary>
    public Expression ParseBeforeAttributes()
    {
        // If the next token is already an attribute list, return a fallback
        if (AtEnd || (Current.Kind == TokenKind.OpenParen && LooksLikeAttributeList()))
            return new IdentifierExpression { Name = "" };

        return ParseWithAttributeStop();
    }

    private Expression ParseWithAttributeStop()
    {
        // Parse normally, but the AtEnd check will short-circuit when needed
        return ParseLogicalOr();
    }

    private bool LooksLikeAttributeList()
    {
        // Look past ( to see if it starts with identifier followed by :, ,, or )
        if (_pos + 1 >= _tokens.Count) return false;
        var afterParen = _tokens[_pos + 1];
        if (afterParen.Kind != TokenKind.Identifier) return false;
        if (_pos + 2 >= _tokens.Count) return true;
        var afterIdent = _tokens[_pos + 2];
        return afterIdent.Kind is TokenKind.Colon or TokenKind.Comma or TokenKind.CloseParen;
    }
}
