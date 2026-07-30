using ChunkL.Lexing;
using Xunit;

namespace ChunkL.Tests.Lexing;

public class LexerTests
{
    private static List<Token> Tokenize(string source) => new Lexer(source).Tokenize();

    [Fact]
    public void Tokenize_HexLiteral()
    {
        var tokens = Tokenize("0x03057000");
        Assert.Equal(TokenKind.HexLiteral, tokens[0].Kind);
        Assert.Equal("0x03057000", tokens[0].Text);
    }

    [Fact]
    public void Tokenize_IntLiteral()
    {
        var tokens = Tokenize("42");
        Assert.Equal(TokenKind.IntLiteral, tokens[0].Kind);
        Assert.Equal("42", tokens[0].Text);
    }

    [Fact]
    public void Tokenize_FloatLiteral()
    {
        var tokens = Tokenize("0.5f");
        Assert.Equal(TokenKind.IntLiteral, tokens[0].Kind);
        Assert.Equal("0.5f", tokens[0].Text);
    }

    [Fact]
    public void Tokenize_Identifier()
    {
        var tokens = Tokenize("CGameCtnBlock");
        Assert.Equal(TokenKind.Identifier, tokens[0].Kind);
        Assert.Equal("CGameCtnBlock", tokens[0].Text);
    }

    [Fact]
    public void Tokenize_StringLiteral()
    {
        var tokens = Tokenize("\"hello world\"");
        Assert.Equal(TokenKind.StringLiteral, tokens[0].Kind);
        Assert.Equal("\"hello world\"", tokens[0].Text);
    }

    [Fact]
    public void Tokenize_DoubleSlashComment()
    {
        var tokens = Tokenize("// this is a comment");
        Assert.Equal(TokenKind.Comment, tokens[0].Kind);
        Assert.Equal("// this is a comment", tokens[0].Text);
    }

    [Fact]
    public void Tokenize_HashComment()
    {
        var tokens = Tokenize("# this is a comment");
        Assert.Equal(TokenKind.Comment, tokens[0].Kind);
        Assert.Equal("# this is a comment", tokens[0].Text);
    }

    [Fact]
    public void Tokenize_MultiCharOperators()
    {
        var tokens = Tokenize("== != <= >= << >> :: .. && ||");
        Assert.Equal(TokenKind.EqualsEquals, tokens[0].Kind);
        Assert.Equal(TokenKind.BangEquals, tokens[1].Kind);
        Assert.Equal(TokenKind.LessThanEquals, tokens[2].Kind);
        Assert.Equal(TokenKind.GreaterThanEquals, tokens[3].Kind);
        Assert.Equal(TokenKind.LessLess, tokens[4].Kind);
        Assert.Equal(TokenKind.GreaterGreater, tokens[5].Kind);
        Assert.Equal(TokenKind.ColonColon, tokens[6].Kind);
        Assert.Equal(TokenKind.DotDot, tokens[7].Kind);
        Assert.Equal(TokenKind.AmpersandAmpersand, tokens[8].Kind);
        Assert.Equal(TokenKind.PipePipe, tokens[9].Kind);
    }

    [Fact]
    public void Tokenize_SingleCharOperators()
    {
        var tokens = Tokenize("( ) [ ] + - * / & | ! ~ ^ ? . , : =");
        Assert.Equal(TokenKind.OpenParen, tokens[0].Kind);
        Assert.Equal(TokenKind.CloseParen, tokens[1].Kind);
        Assert.Equal(TokenKind.OpenBracket, tokens[2].Kind);
        Assert.Equal(TokenKind.CloseBracket, tokens[3].Kind);
        Assert.Equal(TokenKind.Plus, tokens[4].Kind);
        Assert.Equal(TokenKind.Minus, tokens[5].Kind);
        Assert.Equal(TokenKind.Star, tokens[6].Kind);
        Assert.Equal(TokenKind.Slash, tokens[7].Kind);
        Assert.Equal(TokenKind.Ampersand, tokens[8].Kind);
        Assert.Equal(TokenKind.Pipe, tokens[9].Kind);
        Assert.Equal(TokenKind.Bang, tokens[10].Kind);
        Assert.Equal(TokenKind.Tilde, tokens[11].Kind);
        Assert.Equal(TokenKind.Caret, tokens[12].Kind);
        Assert.Equal(TokenKind.Question, tokens[13].Kind);
        Assert.Equal(TokenKind.Dot, tokens[14].Kind);
        Assert.Equal(TokenKind.Comma, tokens[15].Kind);
        Assert.Equal(TokenKind.Colon, tokens[16].Kind);
        Assert.Equal(TokenKind.Equals, tokens[17].Kind);
    }

    [Fact]
    public void Tokenize_Newlines()
    {
        var tokens = Tokenize("a\nb");
        Assert.Equal(TokenKind.Identifier, tokens[0].Kind);
        Assert.Equal(TokenKind.Newline, tokens[1].Kind);
        Assert.Equal(TokenKind.Identifier, tokens[2].Kind);
    }

    [Fact]
    public void Tokenize_LeadingSpaces()
    {
        var tokens = Tokenize("  int Value");
        Assert.Equal(TokenKind.Identifier, tokens[0].Kind);
        Assert.Equal("int", tokens[0].Text);
        Assert.Equal(2, tokens[0].LeadingSpaces);
    }

    [Fact]
    public void Tokenize_EndOfFile()
    {
        var tokens = Tokenize("");
        Assert.Single(tokens);
        Assert.Equal(TokenKind.EndOfFile, tokens[0].Kind);
    }

    [Fact]
    public void Tokenize_ChunkHeader()
    {
        var tokens = Tokenize("0x002 [TM10]");
        Assert.Equal(TokenKind.HexLiteral, tokens[0].Kind);
        Assert.Equal("0x002", tokens[0].Text);
        Assert.Equal(TokenKind.OpenBracket, tokens[1].Kind);
        Assert.Equal(TokenKind.Identifier, tokens[2].Kind);
        Assert.Equal("TM10", tokens[2].Text);
        Assert.Equal(TokenKind.CloseBracket, tokens[3].Kind);
    }

    [Fact]
    public void Tokenize_NegativeIntInExpression()
    {
        var tokens = Tokenize("= -1");
        Assert.Equal(TokenKind.Equals, tokens[0].Kind);
        Assert.Equal(TokenKind.Minus, tokens[1].Kind);
        Assert.Equal(TokenKind.IntLiteral, tokens[2].Kind);
        Assert.Equal("1", tokens[2].Text);
    }

    [Fact]
    public void Tokenize_TypeWithCast()
    {
        var tokens = Tokenize("byte<Direction>");
        Assert.Equal(TokenKind.Identifier, tokens[0].Kind);
        Assert.Equal("byte", tokens[0].Text);
        Assert.Equal(TokenKind.LessThan, tokens[1].Kind);
        Assert.Equal(TokenKind.Identifier, tokens[2].Kind);
        Assert.Equal("Direction", tokens[2].Text);
        Assert.Equal(TokenKind.GreaterThan, tokens[3].Kind);
    }

    [Fact]
    public void Tokenize_VersionCondition()
    {
        var tokens = Tokenize("v2+");
        Assert.Equal(TokenKind.Identifier, tokens[0].Kind);
        Assert.Equal("v2", tokens[0].Text);
        Assert.Equal(TokenKind.Plus, tokens[1].Kind);
    }

    [Fact]
    public void Tokenize_VersionRange()
    {
        var tokens = Tokenize("v3..7");
        Assert.Equal(TokenKind.Identifier, tokens[0].Kind);
        Assert.Equal("v3", tokens[0].Text);
        Assert.Equal(TokenKind.DotDot, tokens[1].Kind);
        Assert.Equal(TokenKind.IntLiteral, tokens[2].Kind);
        Assert.Equal("7", tokens[2].Text);
    }

    [Fact]
    public void Tokenize_SourceOffsets_AreCorrect()
    {
        var source = "int Value";
        var tokens = Tokenize(source);
        Assert.Equal(0, tokens[0].SourceOffset);
        Assert.Equal(3, tokens[0].SourceLength);
        Assert.Equal(4, tokens[1].SourceOffset);
        Assert.Equal(5, tokens[1].SourceLength);
    }

    [Fact]
    public void Tokenize_ClassAttribute()
    {
        var tokens = Tokenize("- inherits: CPlugTreeGenerator");
        Assert.Equal(TokenKind.Minus, tokens[0].Kind);
        Assert.Equal(TokenKind.Identifier, tokens[1].Kind);
        Assert.Equal("inherits", tokens[1].Text);
        Assert.Equal(TokenKind.Colon, tokens[2].Kind);
        Assert.Equal(TokenKind.Identifier, tokens[3].Kind);
        Assert.Equal("CPlugTreeGenerator", tokens[3].Text);
    }
}
