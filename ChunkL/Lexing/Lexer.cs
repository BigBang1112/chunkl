namespace ChunkL.Lexing;

/// <summary>
/// Tokenizes a ChunkL source string into a list of tokens.
/// </summary>
public sealed class Lexer
{
    private readonly string _source;
    private int _pos;
    private int _line = 1;
    private int _column = 1;
    private int _leadingSpaces;
    private bool _atLineStart = true;

    public Lexer(string source)
    {
        _source = source;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (_pos < _source.Length)
        {
            var ch = _source[_pos];

            // Track leading whitespace at start of line
            if (_atLineStart && ch == ' ')
            {
                _leadingSpaces = 0;
                while (_pos < _source.Length && _source[_pos] == ' ')
                {
                    _leadingSpaces++;
                    Advance();
                }
                _atLineStart = false;
                continue;
            }

            if (_atLineStart)
            {
                _leadingSpaces = 0;
                _atLineStart = false;
            }

            // Skip non-leading whitespace (spaces/tabs within a line)
            if (ch == ' ' || ch == '\t')
            {
                Advance();
                continue;
            }

            // Newlines
            if (ch == '\n')
            {
                tokens.Add(MakeToken(TokenKind.Newline, "\n", _pos, 1));
                Advance();
                _atLineStart = true;
                continue;
            }

            if (ch == '\r')
            {
                if (_pos + 1 < _source.Length && _source[_pos + 1] == '\n')
                {
                    tokens.Add(MakeToken(TokenKind.Newline, "\r\n", _pos, 2));
                    Advance();
                    Advance();
                }
                else
                {
                    tokens.Add(MakeToken(TokenKind.Newline, "\r", _pos, 1));
                    Advance();
                }
                _atLineStart = true;
                continue;
            }

            // Comments: // or #
            if (ch == '/' && _pos + 1 < _source.Length && _source[_pos + 1] == '/')
            {
                var start = _pos;
                var startCol = _column;
                var startLine = _line;
                while (_pos < _source.Length && _source[_pos] != '\n' && _source[_pos] != '\r')
                {
                    Advance();
                }
                var text = _source.Substring(start, _pos - start);
                tokens.Add(new Token(TokenKind.Comment, text, new SourcePosition(startLine, startCol), _leadingSpaces, start, _pos - start));
                continue;
            }

            if (ch == '#')
            {
                var start = _pos;
                var startCol = _column;
                var startLine = _line;
                while (_pos < _source.Length && _source[_pos] != '\n' && _source[_pos] != '\r')
                {
                    Advance();
                }
                var text = _source.Substring(start, _pos - start);
                tokens.Add(new Token(TokenKind.Comment, text, new SourcePosition(startLine, startCol), _leadingSpaces, start, _pos - start));
                continue;
            }

            // String literals
            if (ch == '"')
            {
                tokens.Add(ReadStringLiteral());
                continue;
            }

            // Hex literals: 0x...
            if (ch == '0' && _pos + 1 < _source.Length && (_source[_pos + 1] == 'x' || _source[_pos + 1] == 'X'))
            {
                tokens.Add(ReadHexLiteral());
                continue;
            }

            // Integer literals (or negative if preceded by context)
            if (char.IsDigit(ch))
            {
                tokens.Add(ReadIntLiteral());
                continue;
            }

            // Identifiers and keywords
            if (IsIdentStart(ch))
            {
                tokens.Add(ReadIdentifier());
                continue;
            }

            // Multi-char operators
            tokens.Add(ReadOperator());
        }

        tokens.Add(MakeToken(TokenKind.EndOfFile, "", _pos, 0));
        return tokens;
    }

    private Token ReadStringLiteral()
    {
        var start = _pos;
        var startCol = _column;
        var startLine = _line;

        Advance(); // skip opening "
        while (_pos < _source.Length && _source[_pos] != '"' && _source[_pos] != '\n' && _source[_pos] != '\r')
        {
            if (_source[_pos] == '\\' && _pos + 1 < _source.Length)
            {
                Advance(); // skip escape char
            }
            Advance();
        }

        if (_pos < _source.Length && _source[_pos] == '"')
        {
            Advance(); // skip closing "
        }

        var text = _source.Substring(start, _pos - start);
        return new Token(TokenKind.StringLiteral, text, new SourcePosition(startLine, startCol), _leadingSpaces, start, _pos - start);
    }

    private Token ReadHexLiteral()
    {
        var start = _pos;
        var startCol = _column;
        var startLine = _line;

        Advance(); // 0
        Advance(); // x

        while (_pos < _source.Length && IsHexDigit(_source[_pos]))
        {
            Advance();
        }

        var text = _source.Substring(start, _pos - start);
        return new Token(TokenKind.HexLiteral, text, new SourcePosition(startLine, startCol), _leadingSpaces, start, _pos - start);
    }

    private Token ReadIntLiteral()
    {
        var start = _pos;
        var startCol = _column;
        var startLine = _line;

        while (_pos < _source.Length && char.IsDigit(_source[_pos]))
        {
            Advance();
        }

        // Check for float-like suffix (e.g. 0.5f or 1.0)
        if (_pos < _source.Length && _source[_pos] == '.' && _pos + 1 < _source.Length && char.IsDigit(_source[_pos + 1]))
        {
            Advance(); // skip .
            while (_pos < _source.Length && char.IsDigit(_source[_pos]))
            {
                Advance();
            }
            // Optional f suffix
            if (_pos < _source.Length && (_source[_pos] == 'f' || _source[_pos] == 'F'))
            {
                Advance();
            }
        }
        else if (_pos < _source.Length && (_source[_pos] == 'f' || _source[_pos] == 'F'))
        {
            Advance();
        }

        var text = _source.Substring(start, _pos - start);
        return new Token(TokenKind.IntLiteral, text, new SourcePosition(startLine, startCol), _leadingSpaces, start, _pos - start);
    }

    private Token ReadIdentifier()
    {
        var start = _pos;
        var startCol = _column;
        var startLine = _line;

        while (_pos < _source.Length && IsIdentPart(_source[_pos]))
        {
            Advance();
        }

        var text = _source.Substring(start, _pos - start);
        return new Token(TokenKind.Identifier, text, new SourcePosition(startLine, startCol), _leadingSpaces, start, _pos - start);
    }

    private Token ReadOperator()
    {
        var ch = _source[_pos];
        var start = _pos;

        switch (ch)
        {
            case '.':
                if (_pos + 1 < _source.Length && _source[_pos + 1] == '.')
                {
                    Advance(); Advance();
                    return new Token(TokenKind.DotDot, "..", new SourcePosition(_line, _column - 2), _leadingSpaces, start, 2);
                }
                Advance();
                return new Token(TokenKind.Dot, ".", new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);

            case ',':
                Advance();
                return new Token(TokenKind.Comma, ",", new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);

            case ':':
                if (_pos + 1 < _source.Length && _source[_pos + 1] == ':')
                {
                    Advance(); Advance();
                    return new Token(TokenKind.ColonColon, "::", new SourcePosition(_line, _column - 2), _leadingSpaces, start, 2);
                }
                Advance();
                return new Token(TokenKind.Colon, ":", new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);

            case '=':
                if (_pos + 1 < _source.Length && _source[_pos + 1] == '=')
                {
                    Advance(); Advance();
                    return new Token(TokenKind.EqualsEquals, "==", new SourcePosition(_line, _column - 2), _leadingSpaces, start, 2);
                }
                Advance();
                return new Token(TokenKind.Equals, "=", new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);

            case '!':
                if (_pos + 1 < _source.Length && _source[_pos + 1] == '=')
                {
                    Advance(); Advance();
                    return new Token(TokenKind.BangEquals, "!=", new SourcePosition(_line, _column - 2), _leadingSpaces, start, 2);
                }
                Advance();
                return new Token(TokenKind.Bang, "!", new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);

            case '<':
                if (_pos + 1 < _source.Length && _source[_pos + 1] == '=')
                {
                    Advance(); Advance();
                    return new Token(TokenKind.LessThanEquals, "<=", new SourcePosition(_line, _column - 2), _leadingSpaces, start, 2);
                }
                if (_pos + 1 < _source.Length && _source[_pos + 1] == '<')
                {
                    Advance(); Advance();
                    return new Token(TokenKind.LessLess, "<<", new SourcePosition(_line, _column - 2), _leadingSpaces, start, 2);
                }
                Advance();
                return new Token(TokenKind.LessThan, "<", new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);

            case '>':
                if (_pos + 1 < _source.Length && _source[_pos + 1] == '=')
                {
                    Advance(); Advance();
                    return new Token(TokenKind.GreaterThanEquals, ">=", new SourcePosition(_line, _column - 2), _leadingSpaces, start, 2);
                }
                if (_pos + 1 < _source.Length && _source[_pos + 1] == '>')
                {
                    Advance(); Advance();
                    return new Token(TokenKind.GreaterGreater, ">>", new SourcePosition(_line, _column - 2), _leadingSpaces, start, 2);
                }
                Advance();
                return new Token(TokenKind.GreaterThan, ">", new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);

            case '(':
                Advance();
                return new Token(TokenKind.OpenParen, "(", new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);

            case ')':
                Advance();
                return new Token(TokenKind.CloseParen, ")", new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);

            case '[':
                Advance();
                return new Token(TokenKind.OpenBracket, "[", new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);

            case ']':
                Advance();
                return new Token(TokenKind.CloseBracket, "]", new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);

            case '+':
                Advance();
                return new Token(TokenKind.Plus, "+", new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);

            case '-':
                Advance();
                return new Token(TokenKind.Minus, "-", new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);

            case '*':
                Advance();
                return new Token(TokenKind.Star, "*", new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);

            case '/':
                Advance();
                return new Token(TokenKind.Slash, "/", new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);

            case '&':
                if (_pos + 1 < _source.Length && _source[_pos + 1] == '&')
                {
                    Advance(); Advance();
                    return new Token(TokenKind.AmpersandAmpersand, "&&", new SourcePosition(_line, _column - 2), _leadingSpaces, start, 2);
                }
                Advance();
                return new Token(TokenKind.Ampersand, "&", new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);

            case '|':
                if (_pos + 1 < _source.Length && _source[_pos + 1] == '|')
                {
                    Advance(); Advance();
                    return new Token(TokenKind.PipePipe, "||", new SourcePosition(_line, _column - 2), _leadingSpaces, start, 2);
                }
                Advance();
                return new Token(TokenKind.Pipe, "|", new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);

            case '~':
                Advance();
                return new Token(TokenKind.Tilde, "~", new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);

            case '^':
                Advance();
                return new Token(TokenKind.Caret, "^", new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);

            case '?':
                Advance();
                return new Token(TokenKind.Question, "?", new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);

            default:
                Advance();
                return new Token(TokenKind.Unknown, ch.ToString(), new SourcePosition(_line, _column - 1), _leadingSpaces, start, 1);
        }
    }

    private Token MakeToken(TokenKind kind, string text, int offset, int length)
    {
        return new Token(kind, text, new SourcePosition(_line, _column), _leadingSpaces, offset, length);
    }

    private void Advance()
    {
        if (_pos < _source.Length)
        {
            if (_source[_pos] == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
            _pos++;
        }
    }

    private static bool IsIdentStart(char ch) => char.IsLetter(ch) || ch == '_';
    private static bool IsIdentPart(char ch) => char.IsLetterOrDigit(ch) || ch == '_';
    private static bool IsHexDigit(char ch) =>
        char.IsDigit(ch) || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');
}
