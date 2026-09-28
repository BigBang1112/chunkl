use crate::SourceRange;

// ── Expression AST ──────────────────────────────────────────────────────────

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Expression {
    Literal(Literal),
    Identifier(String),
    ScopedIdentifier {
        qualifier: String,
        name: String,
    },
    Unary {
        operator: UnaryOp,
        operand: Box<Expression>,
    },
    Binary {
        left: Box<Expression>,
        operator: BinaryOp,
        right: Box<Expression>,
    },
    Parenthesized(Box<Expression>),
    Tuple(Vec<Expression>),
    PatternTest {
        value: Box<Expression>,
        pattern: Pattern,
    },
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Pattern {
    Constant(Box<Expression>),
    Not(Box<Pattern>),
    And(Box<Pattern>, Box<Pattern>),
    Or(Box<Pattern>, Box<Pattern>),
    Parenthesized(Box<Pattern>),
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Literal {
    Integer(String),
    Hex(String),
    Float(String),
    String(String),
    Bool(bool),
    Null,
    Empty,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum UnaryOp {
    Not,
    BitwiseNot,
    Negate,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BinaryOp {
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
    Divide,
}

// ── File-level AST ──────────────────────────────────────────────────────────

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ChunkLFile {
    pub header: ClassHeader,
    pub class_attributes: Vec<ClassAttribute>,
    pub chunks: Vec<ChunkDeclaration>,
    pub archives: Vec<ArchiveDeclaration>,
    pub enums: Vec<EnumDeclaration>,
    pub flags: Vec<FlagsDeclaration>,
    pub properties: Vec<PropertyDeclaration>,
    pub constructor: Option<ConstructorDeclaration>,
    pub declaration_order: Vec<DeclarationReference>,
    pub top_level_comments: Vec<Comment>,
    pub range: SourceRange,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ClassHeader {
    pub class_name: String,
    pub class_id: String,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ClassAttribute {
    pub name: String,
    pub value: Option<String>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ChunkDeclaration {
    pub range: SourceRange,
    pub offset: ChunkOffset,
    pub attributes: Option<AttributeList>,
    pub version_qualifiers: Vec<VersionQualifier>,
    pub body: Vec<BodyStatement>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ChunkOffset {
    pub hex_value: String,
    pub is_full_id: bool,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct VersionQualifier {
    pub label: String,
    pub max_version: Option<u32>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct AttributeList {
    pub entries: Vec<AttributeEntry>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct AttributeEntry {
    pub name: String,
    pub value: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum BodyStatement {
    Field(FieldDeclaration),
    VersionCondition(VersionCondition),
    If(IfStatement),
    Return(SimpleStatement),
    Throw(SimpleStatement),
    Skip(ExpressionStatement),
    Assert(ExpressionStatement),
    Block(BlockStatement),
    Loop(LoopStatement),
    While(WhileStatement),
    Switch(SwitchStatement),
    Assignment(ComputedAssignment),
    Comment(Comment),
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct FieldDeclaration {
    pub range: SourceRange,
    pub ty: TypeReference,
    pub name: Option<String>,
    pub default_value: Option<Expression>,
    pub default_value_source: Option<String>,
    pub attributes: Option<AttributeList>,
    pub trailing_comment: Option<Comment>,
    pub is_special_keyword: bool,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TypeReference {
    pub name: String,
    pub cast_target: Option<CastType>,
    pub chunk_preference: bool,
    pub is_nullable: bool,
    pub array_dimensions: usize,
    pub fixed_array_count: Option<String>,
    pub array_counts: Vec<Option<String>>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CastType {
    pub name: String,
    pub qualifying_type: Option<String>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum VersionConditionKind {
    GreaterOrEqual,
    LessOrEqual,
    Exact,
    Range,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct VersionCondition {
    pub range: SourceRange,
    pub kind: VersionConditionKind,
    pub version: u32,
    pub version_end: Option<u32>,
    pub attributes: Option<AttributeList>,
    pub body: Vec<BodyStatement>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct IfStatement {
    pub condition: Expression,
    pub body: Vec<BodyStatement>,
    pub else_ifs: Vec<ElseIfClause>,
    pub else_clause: Option<ElseClause>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ElseIfClause {
    pub condition: Expression,
    pub body: Vec<BodyStatement>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ElseClause {
    pub body: Vec<BodyStatement>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SimpleStatement {
    pub attributes: Option<AttributeList>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ExpressionStatement {
    pub expression: Expression,
    pub attributes: Option<AttributeList>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BlockStatement {
    pub attributes: Option<AttributeList>,
    pub body: Vec<BodyStatement>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct LoopStatement {
    pub count_expression: Expression,
    pub body: Vec<BodyStatement>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct WhileStatement {
    pub condition: Expression,
    pub body: Vec<BodyStatement>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SwitchStatement {
    pub expression: Expression,
    pub cases: Vec<SwitchCase>,
    pub default: Option<SwitchDefault>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SwitchCase {
    pub value: Expression,
    pub body: Vec<BodyStatement>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SwitchDefault {
    pub body: Vec<BodyStatement>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ComputedAssignment {
    pub range: SourceRange,
    pub target_name: String,
    pub expression: Expression,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ArchiveDeclaration {
    pub range: SourceRange,
    pub name: Option<String>,
    pub attributes: Option<AttributeList>,
    pub body: Vec<BodyStatement>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct EnumDeclaration {
    pub name: String,
    pub members: Vec<EnumMember>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct EnumMember {
    pub name: String,
    pub explicit_value: Option<String>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct FlagsDeclaration {
    pub name: String,
    pub members: Vec<FlagsMember>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct FlagsMember {
    pub name: String,
    pub bits: BitRange,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BitRange {
    pub start: u32,
    pub end: Option<u32>,
}

impl BitRange {
    pub fn is_single_bit(&self) -> bool {
        self.end.is_none()
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CommentStyle {
    DoubleSlash,
    Hash,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Comment {
    pub text: String,
    pub style: CommentStyle,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum DeclarationReference {
    Chunk(usize),
    Archive(usize),
    Enum(usize),
    Flags(usize),
    Property(usize),
    Constructor,
    Comment(usize),
}

impl ChunkLFile {
    /// Source order for parsed declarations, followed by newly appended declarations.
    pub fn declarations_in_source_order(&self) -> Vec<DeclarationReference> {
        use DeclarationReference::*;
        let current: Vec<_> = (0..self.chunks.len())
            .map(Chunk)
            .chain((0..self.archives.len()).map(Archive))
            .chain((0..self.enums.len()).map(Enum))
            .chain((0..self.flags.len()).map(Flags))
            .chain((0..self.properties.len()).map(Property))
            .chain((0..self.top_level_comments.len()).map(Comment))
            .chain(self.constructor.as_ref().map(|_| Constructor))
            .collect();
        let mut remaining: std::collections::HashSet<_> = current.iter().copied().collect();
        self.declaration_order
            .iter()
            .chain(&current)
            .copied()
            .filter(|reference| remaining.remove(reference))
            .collect()
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ConstructorDeclaration {
    pub body: Vec<BodyStatement>,
    pub trailing_comment: Option<Comment>,
    pub range: SourceRange,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct PropertyDeclaration {
    pub ty: TypeReference,
    pub name: String,
    pub accessors: Vec<PropertyAccessor>,
    pub trailing_comment: Option<Comment>,
    pub range: SourceRange,
}

impl PropertyDeclaration {
    pub fn getter(&self) -> Option<&GetterAccessor> {
        self.accessors.iter().find_map(|a| {
            if let PropertyAccessor::Get(g) = a {
                Some(g)
            } else {
                None
            }
        })
    }
    pub fn setter(&self) -> Option<&SetterAccessor> {
        self.accessors.iter().find_map(|a| {
            if let PropertyAccessor::Set(s) = a {
                Some(s)
            } else {
                None
            }
        })
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum PropertyAccessor {
    Get(GetterAccessor),
    Set(SetterAccessor),
    Comment(Comment),
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GetterAccessor {
    pub expression: Expression,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SetterAccessor {
    pub body: Vec<BodyStatement>,
    pub trailing_comment: Option<Comment>,
}
