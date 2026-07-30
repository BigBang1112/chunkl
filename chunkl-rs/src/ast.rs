use crate::SourceRange;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ChunkLFile {
    pub header: ClassHeader,
    pub class_attributes: Vec<ClassAttribute>,
    pub chunks: Vec<ChunkDeclaration>,
    pub archives: Vec<ArchiveDeclaration>,
    pub enums: Vec<EnumDeclaration>,
    pub flags: Vec<FlagsDeclaration>,
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
    Switch(SwitchStatement),
    Assignment(ComputedAssignment),
    Comment(Comment),
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct FieldDeclaration {
    pub ty: TypeReference,
    pub name: Option<String>,
    pub default_value: Option<String>,
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
    pub kind: VersionConditionKind,
    pub version: u32,
    pub version_end: Option<u32>,
    pub body: Vec<BodyStatement>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct IfStatement {
    pub condition: String,
    pub body: Vec<BodyStatement>,
    pub else_ifs: Vec<ElseIfClause>,
    pub else_clause: Option<ElseClause>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ElseIfClause {
    pub condition: String,
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
    pub expression: String,
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
    pub count_expression: String,
    pub body: Vec<BodyStatement>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SwitchStatement {
    pub expression: String,
    pub cases: Vec<SwitchCase>,
    pub default: Option<SwitchDefault>,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SwitchCase {
    pub value: String,
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
    pub target_name: String,
    pub expression: String,
    pub trailing_comment: Option<Comment>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ArchiveDeclaration {
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
