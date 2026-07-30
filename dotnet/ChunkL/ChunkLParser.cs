using ChunkL.Diagnostics;
using ChunkL.Lexing;
using ChunkL.Syntax;
using ChunkL.Writing;

namespace ChunkL;

/// <summary>
/// Public static facade for parsing and writing ChunkL files.
/// </summary>
public static class ChunkLParser
{
    /// <summary>
    /// Parse a ChunkL source string into an AST.
    /// </summary>
    public static ParseResult ParseSource(string source)
    {
        var diagnostics = new DiagnosticBag();

        try
        {
            var lexer = new Lexer(source);
            var tokens = lexer.Tokenize();
            var parser = new Parsing.Parser(tokens, source, diagnostics);
            var file = parser.ParseFile();
            return new ParseResult(file, diagnostics.ToArray());
        }
        catch (Exception ex)
        {
            diagnostics.ReportError($"Fatal parse error: {ex.Message}", new SourcePosition(1, 1));
            return new ParseResult(null, diagnostics.ToArray());
        }
    }

    /// <summary>
    /// Parse a .chunkl file from disk.
    /// </summary>
    public static ParseResult Parse(string filePath)
    {
        var source = File.ReadAllText(filePath);
        return ParseSource(source);
    }

    /// <summary>
    /// Parse ChunkL source from a stream.
    /// </summary>
    public static ParseResult Parse(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var source = reader.ReadToEnd();
        return ParseSource(source);
    }

    /// <summary>
    /// Write a ChunkL AST back to .chunkl text.
    /// </summary>
    public static string Write(ChunkLFile file, WriterOptions? options = null)
    {
        var writer = new ChunkLWriter(options);
        return writer.Write(file);
    }
}
