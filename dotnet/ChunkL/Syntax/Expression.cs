namespace ChunkL.Syntax;

public abstract class Expression;

public sealed class LiteralExpression : Expression
{
    public required LiteralKind Kind { get; set; }
    public required string Value { get; set; }
}

public enum LiteralKind
{
    Integer,
    Hex,
    Float,
    String,
    True,
    False,
    Null,
    Empty
}

public sealed class IdentifierExpression : Expression
{
    public required string Name { get; set; }
}

public sealed class ScopedIdentifierExpression : Expression
{
    public required string Qualifier { get; set; }
    public required string Name { get; set; }
}

public sealed class UnaryExpression : Expression
{
    public required UnaryOperator Operator { get; set; }
    public required Expression Operand { get; set; }
}

public enum UnaryOperator
{
    Not,
    BitwiseNot,
    Negate
}

public sealed class BinaryExpression : Expression
{
    public required Expression Left { get; set; }
    public required BinaryOperator Operator { get; set; }
    public required Expression Right { get; set; }
}

public enum BinaryOperator
{
    LogicalOr,
    LogicalAnd,
    Equal,
    NotEqual,
    LessThan,
    GreaterThan,
    LessOrEqual,
    GreaterOrEqual,
    BitwiseOr,
    BitwiseXor,
    BitwiseAnd,
    ShiftLeft,
    ShiftRight,
    Add,
    Subtract,
    Multiply,
    Divide
}

public sealed class ParenthesizedExpression : Expression
{
    public required Expression Inner { get; set; }
}

public sealed class TupleExpression : Expression
{
    public required List<Expression> Elements { get; set; }
}
