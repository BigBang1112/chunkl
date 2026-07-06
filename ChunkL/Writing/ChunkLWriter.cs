using System.Text;
using ChunkL.Syntax;

namespace ChunkL.Writing;

/// <summary>
/// Writes a ChunkL AST back to .chunkl text format.
/// </summary>
public sealed class ChunkLWriter
{
    private readonly StringBuilder _sb = new();
    private readonly WriterOptions _options;
    private int _indentLevel;

    public ChunkLWriter(WriterOptions? options = null)
    {
        _options = options ?? new WriterOptions();
    }

    public string Write(ChunkLFile file)
    {
        _sb.Clear();
        _indentLevel = 0;

        WriteClassHeader(file.Header);

        if (file.ClassAttributes.Count > 0)
        {
            foreach (var attr in file.ClassAttributes)
            {
                WriteClassAttribute(attr);
            }
        }

        var wroteDeclaration = file.ClassAttributes.Count > 0;

        foreach (var chunk in file.Chunks)
        {
            _sb.Append(_options.NewLine);
            WriteChunkDeclaration(chunk);
            wroteDeclaration = true;
        }

        foreach (var archive in file.Archives)
        {
            if (wroteDeclaration)
                _sb.Append(_options.NewLine);
            WriteArchiveDeclaration(archive);
            wroteDeclaration = true;
        }

        foreach (var enumDecl in file.Enums)
        {
            if (wroteDeclaration)
                _sb.Append(_options.NewLine);
            WriteEnumDeclaration(enumDecl);
            wroteDeclaration = true;
        }

        foreach (var flagsDecl in file.Flags)
        {
            if (wroteDeclaration)
                _sb.Append(_options.NewLine);
            WriteFlagsDeclaration(flagsDecl);
            wroteDeclaration = true;
        }

        return _sb.ToString();
    }

    private void WriteIndent()
    {
        for (var i = 0; i < _indentLevel; i++)
            _sb.Append(_options.IndentString);
    }

    private void WriteComment(Comment? comment)
    {
        if (comment == null || !_options.PreserveComments)
            return;

        _sb.Append(' ');
        if (comment.Style == CommentStyle.Hash)
            _sb.Append("# ");
        else
            _sb.Append("// ");
        _sb.Append(comment.Text);
    }

    private void WriteNewLine()
    {
        _sb.Append(_options.NewLine);
    }

    private void WriteClassHeader(ClassHeader header)
    {
        _sb.Append(header.ClassName);
        _sb.Append(' ');
        _sb.Append(header.ClassId);
        WriteComment(header.TrailingComment);
        WriteNewLine();
    }

    private void WriteClassAttribute(ClassAttribute attr)
    {
        _sb.Append("- ");
        _sb.Append(attr.Name);
        if (attr.Value != null)
        {
            _sb.Append(": ");
            _sb.Append(attr.Value);
        }
        WriteComment(attr.TrailingComment);
        WriteNewLine();
    }

    private void WriteChunkDeclaration(ChunkDeclaration chunk)
    {
        _sb.Append(chunk.Offset.HexValue);

        if (chunk.Attributes != null)
        {
            _sb.Append(' ');
            WriteAttributeList(chunk.Attributes);
        }

        if (chunk.VersionQualifiers.Count > 0)
        {
            _sb.Append(' ');
            WriteVersionQualifiers(chunk.VersionQualifiers);
        }

        WriteComment(chunk.TrailingComment);
        WriteNewLine();

        _indentLevel = 1;
        WriteBody(chunk.Body);
        _indentLevel = 0;
    }

    private void WriteAttributeList(AttributeList attrs)
    {
        _sb.Append('(');
        for (var i = 0; i < attrs.Entries.Count; i++)
        {
            if (i > 0)
                _sb.Append(", ");
            _sb.Append(attrs.Entries[i].Name);
            if (attrs.Entries[i].Value != null)
            {
                _sb.Append(": ");
                _sb.Append(attrs.Entries[i].Value);
            }
        }
        _sb.Append(')');
    }

    private void WriteVersionQualifiers(List<VersionQualifier> qualifiers)
    {
        _sb.Append('[');
        for (var i = 0; i < qualifiers.Count; i++)
        {
            if (i > 0)
                _sb.Append(", ");
            _sb.Append(qualifiers[i].Label);
            if (qualifiers[i].MaxVersion is { } maxVer)
            {
                _sb.Append($".v{maxVer}");
            }
        }
        _sb.Append(']');
    }

    private void WriteBody(List<IBodyStatement> body)
    {
        foreach (var stmt in body)
        {
            WriteStatement(stmt);
        }
    }

    private void WriteStatement(IBodyStatement stmt)
    {
        switch (stmt)
        {
            case FieldDeclaration field:
                WriteFieldDeclaration(field);
                break;
            case VersionCondition vc:
                WriteVersionCondition(vc);
                break;
            case IfStatement ifStmt:
                WriteIfStatement(ifStmt);
                break;
            case ReturnStatement ret:
                WriteReturnStatement(ret);
                break;
            case ThrowStatement thr:
                WriteThrowStatement(thr);
                break;
            case SkipStatement skip:
                WriteSkipStatement(skip);
                break;
            case AssertStatement assert:
                WriteAssertStatement(assert);
                break;
            case BlockStatement block:
                WriteBlockStatement(block);
                break;
            case LoopStatement loop:
                WriteLoopStatement(loop);
                break;
            case SwitchStatement sw:
                WriteSwitchStatement(sw);
                break;
            case ComputedAssignment ca:
                WriteComputedAssignment(ca);
                break;
            case Comment comment:
                WriteStandaloneComment(comment);
                break;
        }
    }

    private void WriteFieldDeclaration(FieldDeclaration field)
    {
        WriteIndent();
        WriteTypeReference(field.Type);

        if (field.Name != null)
        {
            _sb.Append(' ');
            _sb.Append(field.Name);
        }

        if (field.DefaultValue != null)
        {
            _sb.Append(" = ");
            _sb.Append(field.DefaultValue);
        }

        if (field.Attributes != null)
        {
            _sb.Append(' ');
            WriteAttributeList(field.Attributes);
        }

        WriteComment(field.TrailingComment);
        WriteNewLine();
    }

    private void WriteTypeReference(TypeReference type)
    {
        _sb.Append(type.Name);

        if (type.CastTarget != null)
        {
            _sb.Append('<');
            if (type.CastTarget.QualifyingType != null)
            {
                _sb.Append(type.CastTarget.QualifyingType);
                _sb.Append('.');
            }
            _sb.Append(type.CastTarget.Name);
            _sb.Append('>');
        }

        if (type.ChunkPreference)
            _sb.Append('*');

        if (type.IsNullable)
            _sb.Append('?');

        if (type.FixedArrayCount != null)
            _sb.Append($"[{type.FixedArrayCount}]");
        // remaining dynamic dimensions
        for (var i = (type.FixedArrayCount != null ? 1 : 0); i < type.ArrayDimensions; i++)
            _sb.Append("[]");
    }

    private void WriteVersionCondition(VersionCondition vc)
    {
        WriteIndent();
        _sb.Append('v');
        _sb.Append(vc.Version);

        switch (vc.Kind)
        {
            case VersionConditionKind.GreaterOrEqual:
                _sb.Append('+');
                break;
            case VersionConditionKind.LessOrEqual:
                _sb.Append('-');
                break;
            case VersionConditionKind.Exact:
                _sb.Append('=');
                break;
            case VersionConditionKind.Range:
                _sb.Append("..");
                _sb.Append(vc.VersionEnd);
                break;
        }

        WriteComment(vc.TrailingComment);
        WriteNewLine();

        _indentLevel++;
        WriteBody(vc.Body);
        _indentLevel--;
    }

    private void WriteIfStatement(IfStatement ifStmt)
    {
        WriteIndent();
        _sb.Append("if ");
        _sb.Append(ifStmt.Condition);
        WriteComment(ifStmt.TrailingComment);
        WriteNewLine();

        _indentLevel++;
        WriteBody(ifStmt.Body);
        _indentLevel--;

        foreach (var elseIf in ifStmt.ElseIfs)
        {
            WriteIndent();
            _sb.Append("else if ");
            _sb.Append(elseIf.Condition);
            WriteComment(elseIf.TrailingComment);
            WriteNewLine();

            _indentLevel++;
            WriteBody(elseIf.Body);
            _indentLevel--;
        }

        if (ifStmt.Else != null)
        {
            WriteIndent();
            _sb.Append("else");
            WriteComment(ifStmt.Else.TrailingComment);
            WriteNewLine();

            _indentLevel++;
            WriteBody(ifStmt.Else.Body);
            _indentLevel--;
        }
    }

    private void WriteReturnStatement(ReturnStatement ret)
    {
        WriteIndent();
        _sb.Append("return");
        if (ret.Attributes != null)
        {
            _sb.Append(' ');
            WriteAttributeList(ret.Attributes);
        }
        WriteComment(ret.TrailingComment);
        WriteNewLine();
    }

    private void WriteThrowStatement(ThrowStatement thr)
    {
        WriteIndent();
        _sb.Append("throw");
        if (thr.Attributes != null)
        {
            _sb.Append(' ');
            WriteAttributeList(thr.Attributes);
        }
        WriteComment(thr.TrailingComment);
        WriteNewLine();
    }

    private void WriteSkipStatement(SkipStatement skip)
    {
        WriteIndent();
        _sb.Append("skip ");
        _sb.Append(skip.Expression);
        if (skip.Attributes != null)
        {
            _sb.Append(' ');
            WriteAttributeList(skip.Attributes);
        }
        WriteComment(skip.TrailingComment);
        WriteNewLine();
    }

    private void WriteAssertStatement(AssertStatement assert)
    {
        WriteIndent();
        _sb.Append("assert ");
        _sb.Append(assert.Condition);
        if (assert.Attributes != null)
        {
            _sb.Append(' ');
            WriteAttributeList(assert.Attributes);
        }
        WriteComment(assert.TrailingComment);
        WriteNewLine();
    }

    private void WriteBlockStatement(BlockStatement block)
    {
        WriteIndent();
        _sb.Append("block");
        if (block.Attributes != null)
        {
            _sb.Append(' ');
            WriteAttributeList(block.Attributes);
        }
        WriteComment(block.TrailingComment);
        WriteNewLine();

        _indentLevel++;
        WriteBody(block.Body);
        _indentLevel--;
    }

    private void WriteLoopStatement(LoopStatement loop)
    {
        WriteIndent();
        _sb.Append("loop ");
        _sb.Append(loop.CountExpression);
        WriteComment(loop.TrailingComment);
        WriteNewLine();

        _indentLevel++;
        WriteBody(loop.Body);
        _indentLevel--;
    }

    private void WriteSwitchStatement(SwitchStatement sw)
    {
        WriteIndent();
        _sb.Append("switch ");
        _sb.Append(sw.Expression);
        WriteComment(sw.TrailingComment);
        WriteNewLine();

        _indentLevel++;
        foreach (var c in sw.Cases)
        {
            WriteIndent();
            _sb.Append("case ");
            _sb.Append(c.Value);
            WriteComment(c.TrailingComment);
            WriteNewLine();

            _indentLevel++;
            WriteBody(c.Body);
            _indentLevel--;
        }

        if (sw.Default != null)
        {
            WriteIndent();
            _sb.Append("default");
            WriteComment(sw.Default.TrailingComment);
            WriteNewLine();

            _indentLevel++;
            WriteBody(sw.Default.Body);
            _indentLevel--;
        }
        _indentLevel--;
    }

    private void WriteComputedAssignment(ComputedAssignment ca)
    {
        WriteIndent();
        _sb.Append(ca.TargetName);
        _sb.Append(" = ");
        _sb.Append(ca.Expression);
        WriteComment(ca.TrailingComment);
        WriteNewLine();
    }

    private void WriteStandaloneComment(Comment comment)
    {
        if (!_options.PreserveComments)
            return;

        WriteIndent();
        if (comment.Style == CommentStyle.Hash)
            _sb.Append("# ");
        else
            _sb.Append("// ");
        _sb.Append(comment.Text);
        WriteNewLine();
    }

    private void WriteArchiveDeclaration(ArchiveDeclaration archive)
    {
        _sb.Append("archive");
        if (archive.Name != null)
        {
            _sb.Append(' ');
            _sb.Append(archive.Name);
        }
        if (archive.Attributes != null)
        {
            _sb.Append(' ');
            WriteAttributeList(archive.Attributes);
        }
        WriteComment(archive.TrailingComment);
        WriteNewLine();

        _indentLevel = 1;
        WriteBody(archive.Body);
        _indentLevel = 0;
    }

    private void WriteEnumDeclaration(EnumDeclaration enumDecl)
    {
        _sb.Append("enum ");
        _sb.Append(enumDecl.Name);
        WriteComment(enumDecl.TrailingComment);
        WriteNewLine();

        foreach (var member in enumDecl.Members)
        {
            _sb.Append(_options.IndentString);
            _sb.Append(member.Name);
            if (member.ExplicitValue != null)
            {
                _sb.Append(" = ");
                _sb.Append(member.ExplicitValue);
            }
            WriteComment(member.TrailingComment);
            WriteNewLine();
        }
    }

    private void WriteFlagsDeclaration(FlagsDeclaration flagsDecl)
    {
        _sb.Append("flags ");
        _sb.Append(flagsDecl.Name);
        WriteComment(flagsDecl.TrailingComment);
        WriteNewLine();

        foreach (var member in flagsDecl.Members)
        {
            _sb.Append(_options.IndentString);
            _sb.Append(member.Name);
            _sb.Append('[');
            _sb.Append(member.Bits.Start);
            if (member.Bits.End != null)
            {
                _sb.Append("..");
                _sb.Append(member.Bits.End);
            }
            _sb.Append(']');
            WriteComment(member.TrailingComment);
            WriteNewLine();
        }
    }
}
