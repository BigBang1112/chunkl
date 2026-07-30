use std::fs;
use std::io::{self, Read};
use std::path::Path;

use crate::ast::*;
use crate::{Diagnostic, ParseResult, SourcePosition, SourceRange};

#[derive(Clone, Copy)]
struct Line<'a> {
    indent: usize,
    text: &'a str,
    number: usize,
}

pub fn parse_file(path: impl AsRef<Path>) -> io::Result<ParseResult> {
    fs::read_to_string(path).map(|source| parse_source(&source))
}

pub fn parse_reader(mut reader: impl Read) -> io::Result<ParseResult> {
    let mut source = String::new();
    reader.read_to_string(&mut source)?;
    Ok(parse_source(&source))
}

pub fn parse_source(source: &str) -> ParseResult {
    Parser::new(source).parse()
}

struct Parser<'a> {
    lines: Vec<Line<'a>>,
    index: usize,
    diagnostics: Vec<Diagnostic>,
}

impl<'a> Parser<'a> {
    fn new(source: &'a str) -> Self {
        let lines = source
            .lines()
            .enumerate()
            .map(|(index, raw)| {
                let raw = raw.trim_end_matches('\r');
                let whitespace = raw.len() - raw.trim_start_matches([' ', '\t']).len();
                let indent = raw[..whitespace]
                    .chars()
                    .map(|character| if character == '\t' { 4 } else { 1 })
                    .sum();
                Line {
                    indent,
                    text: raw[whitespace..].trim_end(),
                    number: index + 1,
                }
            })
            .collect();
        Self {
            lines,
            index: 0,
            diagnostics: Vec::new(),
        }
    }

    fn parse(mut self) -> ParseResult {
        self.skip_blank();
        let Some(header_line) = self.current() else {
            self.error(1, "Expected a class header");
            return ParseResult {
                file: None,
                diagnostics: self.diagnostics,
            };
        };
        let (header_text, header_comment) = split_comment(header_line.text);
        let mut header_parts = header_text.split_whitespace();
        let class_name = header_parts.next().unwrap_or_default();
        let class_id = header_parts.next().unwrap_or_default();
        if class_name.is_empty() || !is_hex(class_id) {
            self.error(header_line.number, "Expected '<class name> <hex class id>'");
        }
        let header = ClassHeader {
            class_name: class_name.to_owned(),
            class_id: class_id.to_owned(),
            trailing_comment: header_comment,
        };
        self.index += 1;

        let mut file = ChunkLFile {
            header,
            class_attributes: Vec::new(),
            chunks: Vec::new(),
            archives: Vec::new(),
            enums: Vec::new(),
            flags: Vec::new(),
            top_level_comments: Vec::new(),
            range: SourceRange {
                start: SourcePosition::new(header_line.number, 1),
                end: SourcePosition::new(self.lines.len().max(1), 1),
            },
        };

        while self.index < self.lines.len() {
            self.skip_blank();
            let Some(line) = self.current() else { break };
            if line.indent != 0 {
                self.error(line.number, "Unexpected indented line at top level");
                self.index += 1;
                continue;
            }

            if line.text.starts_with("- ") {
                file.class_attributes.push(self.parse_class_attribute());
            } else if line.text.starts_with("0x") || line.text.starts_with("0X") {
                file.chunks.push(self.parse_chunk());
            } else if starts_keyword(line.text, "archive") {
                file.archives.push(self.parse_archive());
            } else if starts_keyword(line.text, "enum") {
                file.enums.push(self.parse_enum());
            } else if starts_keyword(line.text, "flags") {
                file.flags.push(self.parse_flags());
            } else if let Some(comment) = parse_standalone_comment(line.text) {
                file.top_level_comments.push(comment);
                self.index += 1;
            } else {
                self.error(
                    line.number,
                    format!("Unexpected top-level declaration '{}'", line.text),
                );
                self.index += 1;
            }
        }

        ParseResult {
            file: Some(file),
            diagnostics: self.diagnostics,
        }
    }

    fn parse_class_attribute(&mut self) -> ClassAttribute {
        let line = self.take();
        let (text, trailing_comment) = split_comment(line.text);
        let text = text.trim_start_matches('-').trim();
        let (name, value) = split_once_trimmed(text, ':');
        ClassAttribute {
            name: name.to_owned(),
            value: value.map(str::to_owned),
            trailing_comment,
        }
    }

    fn parse_chunk(&mut self) -> ChunkDeclaration {
        let line = self.take();
        let (text, trailing_comment) = split_comment(line.text);
        let offset_end = text.find(char::is_whitespace).unwrap_or(text.len());
        let offset_text = &text[..offset_end];
        if !is_hex(offset_text) {
            self.error(line.number, "Invalid chunk offset");
        }
        let mut remainder = text[offset_end..].trim();
        let (attributes, rest) = take_leading_attributes(remainder);
        remainder = rest;
        let mut version_qualifiers = Vec::new();
        if remainder.starts_with('[') {
            if let Some(end) = remainder.find(']') {
                version_qualifiers = remainder[1..end]
                    .split(',')
                    .filter_map(|part| {
                        let part = part.trim();
                        if part.is_empty() {
                            return None;
                        }
                        let (label, max_version) = part
                            .rsplit_once(".v")
                            .map(|(label, version)| (label, version.parse().ok()))
                            .unwrap_or((part, None));
                        Some(VersionQualifier {
                            label: label.to_owned(),
                            max_version,
                        })
                    })
                    .collect();
            } else {
                self.error(line.number, "Unclosed version qualifier list");
            }
        }
        ChunkDeclaration {
            offset: ChunkOffset {
                hex_value: offset_text.to_owned(),
                is_full_id: offset_text.len() > 5,
            },
            attributes,
            version_qualifiers,
            body: self.parse_body(2),
            trailing_comment,
        }
    }

    fn parse_archive(&mut self) -> ArchiveDeclaration {
        let line = self.take();
        let (text, trailing_comment) = split_comment(line.text);
        let remainder = text["archive".len()..].trim();
        let (without_attributes, attributes) = take_trailing_attributes(remainder);
        ArchiveDeclaration {
            name: (!without_attributes.is_empty()).then(|| without_attributes.to_owned()),
            attributes,
            body: self.parse_body(2),
            trailing_comment,
        }
    }

    fn parse_enum(&mut self) -> EnumDeclaration {
        let line = self.take();
        let (text, trailing_comment) = split_comment(line.text);
        let name = text["enum".len()..].trim().to_owned();
        let mut members = Vec::new();
        while let Some(member_line) = self.current() {
            if member_line.text.is_empty() {
                self.index += 1;
                continue;
            }
            if member_line.indent < 2 {
                break;
            }
            let member_line = self.take();
            let (member_text, member_comment) = split_comment(member_line.text);
            let (member_name, value) = split_once_trimmed(member_text, '=');
            members.push(EnumMember {
                name: member_name.to_owned(),
                explicit_value: value.map(str::to_owned),
                trailing_comment: member_comment,
            });
        }
        EnumDeclaration {
            name,
            members,
            trailing_comment,
        }
    }

    fn parse_flags(&mut self) -> FlagsDeclaration {
        let line = self.take();
        let (text, trailing_comment) = split_comment(line.text);
        let name = text["flags".len()..].trim().to_owned();
        let mut members = Vec::new();
        while let Some(member_line) = self.current() {
            if member_line.text.is_empty() {
                self.index += 1;
                continue;
            }
            if member_line.indent < 2 {
                break;
            }
            let member_line = self.take();
            let (member_text, member_comment) = split_comment(member_line.text);
            let Some(open) = member_text.rfind('[') else {
                self.error(member_line.number, "Expected a bit range");
                continue;
            };
            let range = member_text[open + 1..].trim_end_matches(']');
            let (start, end) = range
                .split_once("..")
                .map(|(start, end)| (start.parse().unwrap_or(0), end.parse().ok()))
                .unwrap_or((range.parse().unwrap_or(0), None));
            members.push(FlagsMember {
                name: member_text[..open].trim().to_owned(),
                bits: BitRange { start, end },
                trailing_comment: member_comment,
            });
        }
        FlagsDeclaration {
            name,
            members,
            trailing_comment,
        }
    }

    fn parse_body(&mut self, expected_indent: usize) -> Vec<BodyStatement> {
        let mut statements = Vec::new();
        loop {
            self.skip_blank();
            let Some(line) = self.current() else { break };
            if line.indent < expected_indent {
                break;
            }
            if line.indent > expected_indent {
                self.error(
                    line.number,
                    format!("Unexpected indentation; expected {expected_indent} spaces"),
                );
            }
            if line.indent != expected_indent || starts_keyword(line.text, "else") {
                break;
            }
            statements.push(self.parse_statement(expected_indent));
        }
        statements
    }

    fn parse_statement(&mut self, indent: usize) -> BodyStatement {
        let line = self.current().expect("statement line");
        if let Some(comment) = parse_standalone_comment(line.text) {
            self.index += 1;
            return BodyStatement::Comment(comment);
        }
        if parse_version_marker(line.text).is_some() {
            return self.parse_version_condition(indent);
        }
        for keyword in [
            "if", "switch", "loop", "block", "skip", "assert", "return", "throw",
        ] {
            if starts_keyword(line.text, keyword) {
                return match keyword {
                    "if" => BodyStatement::If(self.parse_if(indent)),
                    "switch" => BodyStatement::Switch(self.parse_switch(indent)),
                    "loop" => BodyStatement::Loop(self.parse_loop(indent)),
                    "block" => BodyStatement::Block(self.parse_block(indent)),
                    "skip" => BodyStatement::Skip(self.parse_expression_statement("skip")),
                    "assert" => BodyStatement::Assert(self.parse_expression_statement("assert")),
                    "return" => BodyStatement::Return(self.parse_simple_statement("return")),
                    "throw" => BodyStatement::Throw(self.parse_simple_statement("throw")),
                    _ => unreachable!(),
                };
            }
        }
        self.parse_field_or_assignment()
    }

    fn parse_version_condition(&mut self, indent: usize) -> BodyStatement {
        let line = self.take();
        let (text, trailing_comment) = split_comment(line.text);
        let (kind, version, version_end) = parse_version_marker(text).unwrap();
        BodyStatement::VersionCondition(VersionCondition {
            kind,
            version,
            version_end,
            body: self.parse_body(indent + 2),
            trailing_comment,
        })
    }

    fn parse_if(&mut self, indent: usize) -> IfStatement {
        let line = self.take();
        let (text, trailing_comment) = split_comment(line.text);
        let condition = text["if".len()..].trim().to_owned();
        let body = self.parse_body(indent + 2);
        let mut else_ifs = Vec::new();
        let mut else_clause = None;
        loop {
            self.skip_blank();
            let Some(next) = self.current() else { break };
            if next.indent != indent {
                break;
            }
            if next.text.starts_with("else if ") {
                let clause_line = self.take();
                let (clause_text, comment) = split_comment(clause_line.text);
                else_ifs.push(ElseIfClause {
                    condition: clause_text["else if".len()..].trim().to_owned(),
                    body: self.parse_body(indent + 2),
                    trailing_comment: comment,
                });
            } else if next.text == "else" || next.text.starts_with("else ") {
                let clause_line = self.take();
                let (_, comment) = split_comment(clause_line.text);
                else_clause = Some(ElseClause {
                    body: self.parse_body(indent + 2),
                    trailing_comment: comment,
                });
                break;
            } else {
                break;
            }
        }
        IfStatement {
            condition,
            body,
            else_ifs,
            else_clause,
            trailing_comment,
        }
    }

    fn parse_loop(&mut self, indent: usize) -> LoopStatement {
        let line = self.take();
        let (text, trailing_comment) = split_comment(line.text);
        LoopStatement {
            count_expression: text["loop".len()..].trim().to_owned(),
            body: self.parse_body(indent + 2),
            trailing_comment,
        }
    }

    fn parse_block(&mut self, indent: usize) -> BlockStatement {
        let line = self.take();
        let (text, trailing_comment) = split_comment(line.text);
        let (_, attributes) = take_trailing_attributes(text["block".len()..].trim());
        BlockStatement {
            attributes,
            body: self.parse_body(indent + 2),
            trailing_comment,
        }
    }

    fn parse_switch(&mut self, indent: usize) -> SwitchStatement {
        let line = self.take();
        let (text, trailing_comment) = split_comment(line.text);
        let mut cases = Vec::new();
        let mut default = None;
        loop {
            self.skip_blank();
            let Some(case_line) = self.current() else {
                break;
            };
            if case_line.indent != indent + 2 {
                break;
            }
            if starts_keyword(case_line.text, "case") {
                let case_line = self.take();
                let (case_text, comment) = split_comment(case_line.text);
                cases.push(SwitchCase {
                    value: case_text["case".len()..].trim().to_owned(),
                    body: self.parse_body(indent + 4),
                    trailing_comment: comment,
                });
            } else if case_line.text == "default" || case_line.text.starts_with("default ") {
                let default_line = self.take();
                let (_, comment) = split_comment(default_line.text);
                default = Some(SwitchDefault {
                    body: self.parse_body(indent + 4),
                    trailing_comment: comment,
                });
            } else {
                break;
            }
        }
        SwitchStatement {
            expression: text["switch".len()..].trim().to_owned(),
            cases,
            default,
            trailing_comment,
        }
    }

    fn parse_simple_statement(&mut self, keyword: &str) -> SimpleStatement {
        let line = self.take();
        let (text, trailing_comment) = split_comment(line.text);
        let (_, attributes) = take_trailing_attributes(text[keyword.len()..].trim());
        SimpleStatement {
            attributes,
            trailing_comment,
        }
    }

    fn parse_expression_statement(&mut self, keyword: &str) -> ExpressionStatement {
        let line = self.take();
        let (text, trailing_comment) = split_comment(line.text);
        let remainder = text[keyword.len()..].trim();
        let (expression, attributes) = take_trailing_attributes(remainder);
        ExpressionStatement {
            expression: expression.to_owned(),
            attributes,
            trailing_comment,
        }
    }

    fn parse_field_or_assignment(&mut self) -> BodyStatement {
        let line = self.take();
        let (text, trailing_comment) = split_comment(line.text);
        if let Some((left, right)) = text.split_once('=') {
            let left = left.trim();
            if !left.contains(char::is_whitespace) && !is_special_keyword(left) {
                return BodyStatement::Assignment(ComputedAssignment {
                    target_name: left.to_owned(),
                    expression: right.trim().to_owned(),
                    trailing_comment,
                });
            }
        }

        let (without_attributes, attributes) = take_trailing_attributes(text);
        let (declaration, default_value) = without_attributes
            .split_once('=')
            .map(|(declaration, value)| (declaration.trim(), Some(value.trim().to_owned())))
            .unwrap_or((without_attributes, None));
        let mut parts = declaration.split_whitespace();
        let type_text = parts.next().unwrap_or_default();
        let name = parts.next().map(str::to_owned);
        if type_text.is_empty() {
            self.error(line.number, "Expected a field declaration");
        }
        BodyStatement::Field(FieldDeclaration {
            ty: parse_type(type_text),
            name,
            default_value,
            attributes,
            trailing_comment,
            is_special_keyword: is_special_keyword(type_text),
        })
    }

    fn current(&self) -> Option<Line<'a>> {
        self.lines.get(self.index).copied()
    }

    fn take(&mut self) -> Line<'a> {
        let line = self.current().expect("line");
        self.index += 1;
        line
    }

    fn skip_blank(&mut self) {
        while self.current().is_some_and(|line| line.text.is_empty()) {
            self.index += 1;
        }
    }

    fn error(&mut self, line: usize, message: impl Into<String>) {
        self.diagnostics
            .push(Diagnostic::error(message, SourcePosition::new(line, 1)));
    }
}

fn starts_keyword(text: &str, keyword: &str) -> bool {
    text == keyword
        || text
            .strip_prefix(keyword)
            .is_some_and(|rest| rest.starts_with(char::is_whitespace))
}

fn is_hex(text: &str) -> bool {
    text.strip_prefix("0x")
        .or_else(|| text.strip_prefix("0X"))
        .is_some_and(|digits| !digits.is_empty() && digits.chars().all(|c| c.is_ascii_hexdigit()))
}

fn is_special_keyword(text: &str) -> bool {
    matches!(text, "version" | "versionb" | "base")
}

fn parse_version_marker(text: &str) -> Option<(VersionConditionKind, u32, Option<u32>)> {
    let text = text.split_whitespace().next()?;
    let rest = text.strip_prefix('v')?;
    let digit_count = rest.chars().take_while(|c| c.is_ascii_digit()).count();
    if digit_count == 0 {
        return None;
    }
    let version = rest[..digit_count].parse().ok()?;
    let suffix = &rest[digit_count..];
    match suffix {
        "+" => Some((VersionConditionKind::GreaterOrEqual, version, None)),
        "-" => Some((VersionConditionKind::LessOrEqual, version, None)),
        "=" => Some((VersionConditionKind::Exact, version, None)),
        _ => suffix
            .strip_prefix("..")
            .and_then(|end| end.parse().ok())
            .map(|end| (VersionConditionKind::Range, version, Some(end))),
    }
}

fn parse_type(text: &str) -> TypeReference {
    let mut base_end = text.len();
    for (index, character) in text.char_indices() {
        if matches!(character, '<' | '*' | '?' | '[') {
            base_end = index;
            break;
        }
    }
    let mut remainder = &text[base_end..];
    let mut cast_target = None;
    if remainder.starts_with('<') {
        if let Some(end) = remainder.find('>') {
            let cast = &remainder[1..end];
            let (qualifying_type, name) = cast
                .rsplit_once('.')
                .map(|(qualifier, name)| (Some(qualifier.to_owned()), name.to_owned()))
                .unwrap_or((None, cast.to_owned()));
            cast_target = Some(CastType {
                name,
                qualifying_type,
            });
            remainder = &remainder[end + 1..];
        }
    }
    let chunk_preference = remainder.starts_with('*');
    if chunk_preference {
        remainder = &remainder[1..];
    }
    let is_nullable = remainder.starts_with('?');
    if is_nullable {
        remainder = &remainder[1..];
    }
    let mut array_dimensions = 0;
    let mut fixed_array_count = None;
    while let Some(array) = remainder.strip_prefix('[') {
        let Some(end) = array.find(']') else { break };
        let count = &array[..end];
        array_dimensions += 1;
        if !count.is_empty() && fixed_array_count.is_none() {
            fixed_array_count = Some(count.to_owned());
        }
        remainder = &array[end + 1..];
    }
    TypeReference {
        name: text[..base_end].to_owned(),
        cast_target,
        chunk_preference,
        is_nullable,
        array_dimensions,
        fixed_array_count,
    }
}

fn split_comment(text: &str) -> (&str, Option<Comment>) {
    let mut quoted = false;
    let bytes = text.as_bytes();
    let mut index = 0;
    while index < bytes.len() {
        if bytes[index] == b'"' && (index == 0 || bytes[index - 1] != b'\\') {
            quoted = !quoted;
        }
        if !quoted && bytes[index] == b'#' {
            return (
                text[..index].trim_end(),
                Some(Comment {
                    text: text[index + 1..].trim_start().to_owned(),
                    style: CommentStyle::Hash,
                }),
            );
        }
        if !quoted && index + 1 < bytes.len() && &bytes[index..index + 2] == b"//" {
            return (
                text[..index].trim_end(),
                Some(Comment {
                    text: text[index + 2..].trim_start().to_owned(),
                    style: CommentStyle::DoubleSlash,
                }),
            );
        }
        index += 1;
    }
    (text.trim_end(), None)
}

fn parse_standalone_comment(text: &str) -> Option<Comment> {
    if let Some(text) = text.strip_prefix("//") {
        Some(Comment {
            text: text.trim_start().to_owned(),
            style: CommentStyle::DoubleSlash,
        })
    } else {
        text.strip_prefix('#').map(|text| Comment {
            text: text.trim_start().to_owned(),
            style: CommentStyle::Hash,
        })
    }
}

fn split_once_trimmed(text: &str, delimiter: char) -> (&str, Option<&str>) {
    text.split_once(delimiter)
        .map(|(left, right)| (left.trim(), Some(right.trim())))
        .unwrap_or((text.trim(), None))
}

fn take_leading_attributes(text: &str) -> (Option<AttributeList>, &str) {
    if !text.starts_with('(') {
        return (None, text);
    }
    let Some(end) = text.find(')') else {
        return (None, text);
    };
    (
        Some(parse_attributes(&text[1..end])),
        text[end + 1..].trim(),
    )
}

fn take_trailing_attributes(text: &str) -> (&str, Option<AttributeList>) {
    let text = text.trim();
    if !text.ends_with(')') {
        return (text, None);
    }
    let Some(open) = text.rfind('(') else {
        return (text, None);
    };
    let inside = &text[open + 1..text.len() - 1];
    if inside.contains(['=', '&', '|', '!', '<', '>']) {
        return (text, None);
    }
    (text[..open].trim_end(), Some(parse_attributes(inside)))
}

fn parse_attributes(text: &str) -> AttributeList {
    AttributeList {
        entries: text
            .split(',')
            .filter_map(|entry| {
                let entry = entry.trim();
                if entry.is_empty() {
                    return None;
                }
                let (name, value) = split_once_trimmed(entry, ':');
                Some(AttributeEntry {
                    name: name.to_owned(),
                    value: value.map(str::to_owned),
                })
            })
            .collect(),
    }
}
