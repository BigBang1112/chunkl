//! Local field resolution, initialization order, and semantic validation.
//! This module does not execute binary serialization or construct host objects.

use crate::{
    Diagnostic, DiagnosticSeverity, Lexer, SourcePosition, TokenKind, ast::*, write_expression,
};
use std::collections::{HashMap, HashSet};

#[derive(Debug)]
pub struct StoredField<'a> {
    pub name: &'a str,
    pub ty: &'a TypeReference,
    pub declarations: Vec<&'a FieldDeclaration>,
    pub default_declaration: Option<&'a FieldDeclaration>,
    pub is_default_skipped: bool,
}

#[derive(Debug, Default)]
pub struct FieldScope<'a> {
    /// Apply type defaults to these members before evaluating inline defaults.
    pub fields: Vec<StoredField<'a>>,
    /// Indices into fields, in default evaluation order.
    pub inline_defaults: Vec<usize>,
}

#[derive(Debug)]
pub struct ArchiveScope<'a> {
    pub declaration: &'a ArchiveDeclaration,
    pub fields: FieldScope<'a>,
    pub requires_external_version: bool,
}

#[derive(Debug, Default)]
pub struct SemanticModel<'a> {
    pub class: FieldScope<'a>,
    pub archives: Vec<ArchiveScope<'a>>,
    pub constructor_assignments: Vec<&'a ComputedAssignment>,
    pub diagnostics: Vec<Diagnostic>,
}

impl SemanticModel<'_> {
    pub fn success(&self) -> bool {
        !self
            .diagnostics
            .iter()
            .any(|d| d.severity == DiagnosticSeverity::Error)
    }
}

/// Analyze local declarations. External types and caller-supplied archive versions remain the host's responsibility.
pub fn analyze(file: &ChunkLFile) -> SemanticModel<'_> {
    let mut model = SemanticModel::default();
    let mut fields = Vec::new();
    for reference in file.declarations_in_source_order() {
        match reference {
            DeclarationReference::Chunk(index) => fields.extend(walk(&file.chunks[index].body)),
            DeclarationReference::Archive(index) if file.archives[index].name.is_none() => {
                fields.extend(walk(&file.archives[index].body))
            }
            _ => {}
        }
    }
    if let Some(constructor) = &file.constructor {
        model
            .constructor_assignments
            .extend(constructor.body.iter().filter_map(|s| {
                if let BodyStatement::Assignment(a) = s {
                    Some(a)
                } else {
                    None
                }
            }));
    }
    let targets = model
        .constructor_assignments
        .iter()
        .map(|a| a.target_name.as_str())
        .collect();
    model.class = resolve_fields(fields, &targets, &mut model.diagnostics);
    for (index, archive) in file.archives.iter().enumerate() {
        let fields = if archive.name.is_some() {
            resolve_fields(walk(&archive.body), &HashSet::new(), &mut model.diagnostics)
        } else {
            FieldScope::default()
        };
        let flow = version_flow(
            &archive.body,
            false,
            Some(index),
            file,
            &mut model.diagnostics,
            &mut HashSet::from([index]),
        );
        model.archives.push(ArchiveScope {
            declaration: archive,
            fields,
            requires_external_version: flow.needs_external,
        });
    }
    for chunk in &file.chunks {
        if version_flow(
            &chunk.body,
            false,
            None,
            file,
            &mut model.diagnostics,
            &mut HashSet::new(),
        )
        .needs_external
        {
            model.diagnostics.push(Diagnostic::error(
                "Chunk version blocks require a preceding version or versionb field",
                chunk.range.start,
            ));
        }
    }
    validate_properties(file, &model.class, &model.archives, &mut model.diagnostics);
    model
}

fn resolve_fields<'a>(
    statements: Vec<&'a BodyStatement>,
    targets: &HashSet<&str>,
    diagnostics: &mut Vec<Diagnostic>,
) -> FieldScope<'a> {
    let mut scope = FieldScope::default();
    let mut by_name = HashMap::new();
    let mut defaults = Vec::new();
    for statement in statements {
        let BodyStatement::Field(declaration) = statement else {
            continue;
        };
        let Some(name) = declaration
            .name
            .as_deref()
            .filter(|_| !declaration.is_special_keyword)
        else {
            continue;
        };
        let index = *by_name.entry(name).or_insert_with(|| {
            scope.fields.push(StoredField {
                name,
                ty: &declaration.ty,
                declarations: Vec::new(),
                default_declaration: None,
                is_default_skipped: targets.contains(name),
            });
            scope.fields.len() - 1
        });
        let field = &mut scope.fields[index];
        field.declarations.push(declaration);
        if declaration.default_value.is_some() {
            if let Some(previous) = field.default_declaration {
                if default_syntax(previous) != default_syntax(declaration) {
                    diagnostics.push(Diagnostic::error(
                        format!("Conflicting inline defaults for shared field '{name}'"),
                        declaration.range.start,
                    ));
                }
            } else {
                field.default_declaration = Some(declaration);
                defaults.push(index);
            }
        }
    }
    for field in &mut scope.fields {
        let first = field.declarations[0];
        let types: Vec<_> = field.declarations.iter().map(|d| &d.ty).collect();
        let integer_types = first.ty.cast_target.is_none()
            && first.ty.array_dimensions == 0
            && types.iter().all(|t| integer_info(&t.name).is_some());
        let stored_type = if integer_types {
            types
                .iter()
                .copied()
                .filter(|candidate| {
                    types
                        .iter()
                        .all(|t| integer_contains(&candidate.name, &t.name))
                })
                .max_by_key(|candidate| integer_info(&candidate.name).unwrap().0)
        } else if types.iter().all(|t| t.name == first.ty.name) {
            Some(&first.ty)
        } else {
            None
        };
        match stored_type {
            Some(ty) if types.iter().all(|t| modifiers_match(&first.ty, t)) => field.ty = ty,
            _ => diagnostics.push(Diagnostic::error(
                format!(
                    "Incompatible declarations for shared field '{}'",
                    field.name
                ),
                field.declarations.last().unwrap().range.start,
            )),
        }
    }
    scope.inline_defaults.extend(
        defaults
            .into_iter()
            .filter(|i| !scope.fields[*i].is_default_skipped),
    );
    scope
}

fn syntax(text: &str) -> Vec<(TokenKind, String)> {
    Lexer::new(text)
        .tokenize()
        .into_iter()
        .filter(|t| {
            !matches!(
                t.kind,
                TokenKind::Newline | TokenKind::Comment | TokenKind::EndOfFile
            )
        })
        .map(|t| (t.kind, t.text))
        .collect()
}

fn default_syntax(field: &FieldDeclaration) -> Vec<(TokenKind, String)> {
    let current = write_expression(field.default_value.as_ref().unwrap());
    if let Some(source) = &field.default_value_source {
        let (parsed, diagnostics) = crate::parse_expression_checked(source);
        if diagnostics.is_empty() && syntax(&write_expression(&parsed)) == syntax(&current) {
            return syntax(source);
        }
    }
    syntax(&current)
}

fn array_shape(ty: &TypeReference) -> Vec<Option<Vec<(TokenKind, String)>>> {
    (0..ty.array_dimensions)
        .map(|i| {
            let count = if ty.array_counts.len() == ty.array_dimensions {
                ty.array_counts[i].as_deref()
            } else if i == 0 {
                ty.fixed_array_count.as_deref()
            } else {
                None
            };
            count.map(syntax)
        })
        .collect()
}

fn modifiers_match(a: &TypeReference, b: &TypeReference) -> bool {
    a.cast_target == b.cast_target
        && a.is_nullable == b.is_nullable
        && a.chunk_preference == b.chunk_preference
        && array_shape(a) == array_shape(b)
}

fn integer_info(name: &str) -> Option<(u8, bool)> {
    Some(match name {
        "sbyte" => (8, true),
        "byte" => (8, false),
        "short" => (16, true),
        "ushort" => (16, false),
        "int" => (32, true),
        "uint" => (32, false),
        "long" => (64, true),
        "ulong" => (64, false),
        _ => return None,
    })
}

fn integer_contains(target: &str, source: &str) -> bool {
    let a = integer_info(target).unwrap();
    let b = integer_info(source).unwrap();
    if a.1 == b.1 {
        a.0 >= b.0
    } else {
        a.1 && a.0 > b.0
    }
}

fn child_bodies(statement: &BodyStatement) -> Vec<&[BodyStatement]> {
    match statement {
        BodyStatement::VersionCondition(c) => vec![&c.body],
        BodyStatement::Block(b) => vec![&b.body],
        BodyStatement::Loop(l) => vec![&l.body],
        BodyStatement::While(l) => vec![&l.body],
        BodyStatement::If(b) => std::iter::once(b.body.as_slice())
            .chain(b.else_ifs.iter().map(|c| c.body.as_slice()))
            .chain(b.else_clause.as_ref().map(|c| c.body.as_slice()))
            .collect(),
        BodyStatement::Switch(s) => s
            .cases
            .iter()
            .map(|c| c.body.as_slice())
            .chain(s.default.as_ref().map(|c| c.body.as_slice()))
            .collect(),
        _ => Vec::new(),
    }
}

fn walk(body: &[BodyStatement]) -> Vec<&BodyStatement> {
    let mut statements = Vec::new();
    for statement in body {
        statements.push(statement);
        for child in child_bodies(statement) {
            statements.extend(walk(child));
        }
    }
    statements
}

#[derive(Clone, Copy)]
struct VersionState {
    established: bool,
    falls_through: bool,
    needs_external: bool,
}

fn version_flow(
    body: &[BodyStatement],
    mut established: bool,
    archive: Option<usize>,
    file: &ChunkLFile,
    diagnostics: &mut Vec<Diagnostic>,
    inheritance: &mut HashSet<usize>,
) -> VersionState {
    let mut needs_external = false;
    for statement in body {
        match statement {
            BodyStatement::Field(f) if matches!(f.ty.name.as_str(), "version" | "versionb") => {
                established = true
            }
            BodyStatement::Field(f) if f.ty.name == "base" && archive.is_some() => {
                let archive = &file.archives[archive.unwrap()];
                let parent_name = archive
                    .attributes
                    .as_ref()
                    .and_then(|a| a.entries.iter().find(|e| e.name == "inherits"))
                    .and_then(|e| e.value.as_deref());
                if let Some(parent) = file
                    .archives
                    .iter()
                    .position(|a| a.name.is_some() && a.name.as_deref() == parent_name)
                {
                    if !inheritance.insert(parent) {
                        diagnostics.push(Diagnostic::error(
                            "Cyclic archive inheritance",
                            archive.range.start,
                        ));
                        continue;
                    }
                    let flow = version_flow(
                        &file.archives[parent].body,
                        established,
                        Some(parent),
                        file,
                        diagnostics,
                        inheritance,
                    );
                    inheritance.remove(&parent);
                    needs_external |= flow.needs_external;
                    established = flow.established;
                }
            }
            BodyStatement::Return(_) | BodyStatement::Throw(_) => {
                return VersionState {
                    established,
                    falls_through: false,
                    needs_external,
                };
            }
            BodyStatement::Block(b) => {
                let flow = version_flow(
                    &b.body,
                    established,
                    archive,
                    file,
                    diagnostics,
                    inheritance,
                );
                needs_external |= flow.needs_external;
                established = flow.established;
                if !flow.falls_through {
                    return VersionState {
                        established,
                        falls_through: false,
                        needs_external,
                    };
                }
            }
            BodyStatement::VersionCondition(c) => {
                needs_external |= !established;
                needs_external |= version_flow(
                    &c.body,
                    established,
                    archive,
                    file,
                    diagnostics,
                    inheritance,
                )
                .needs_external;
            }
            BodyStatement::If(_) | BodyStatement::Switch(_) => {
                let mut branches: Vec<_> = child_bodies(statement)
                    .into_iter()
                    .map(|b| version_flow(b, established, archive, file, diagnostics, inheritance))
                    .collect();
                let has_default = match statement {
                    BodyStatement::If(b) => b.else_clause.is_some(),
                    BodyStatement::Switch(s) => s.default.is_some(),
                    _ => false,
                };
                if !has_default {
                    branches.push(VersionState {
                        established,
                        falls_through: true,
                        needs_external: false,
                    });
                }
                needs_external |= branches.iter().any(|b| b.needs_external);
                branches.retain(|b| b.falls_through);
                if branches.is_empty() {
                    return VersionState {
                        established,
                        falls_through: false,
                        needs_external,
                    };
                }
                established = branches.iter().all(|b| b.established);
            }
            BodyStatement::Loop(_) | BodyStatement::While(_) => {
                for child in child_bodies(statement) {
                    needs_external |=
                        version_flow(child, established, archive, file, diagnostics, inheritance)
                            .needs_external;
                }
            }
            _ => {}
        }
    }
    VersionState {
        established,
        falls_through: true,
        needs_external,
    }
}

struct PropertyValidator<'a, 'd> {
    fields: HashMap<&'a str, &'a StoredField<'a>>,
    properties: HashMap<&'a str, &'a PropertyDeclaration>,
    has_inherited_class: bool,
    properties_allowed: bool,
    diagnostics: &'d mut Vec<Diagnostic>,
}

fn validate_properties(
    file: &ChunkLFile,
    scope: &FieldScope<'_>,
    archives: &[ArchiveScope<'_>],
    diagnostics: &mut Vec<Diagnostic>,
) {
    let mut validator = PropertyValidator {
        fields: scope.fields.iter().map(|f| (f.name, f)).collect(),
        properties: HashMap::new(),
        has_inherited_class: file.class_attributes.iter().any(|a| a.name == "inherits"),
        properties_allowed: true,
        diagnostics,
    };
    for property in &file.properties {
        if validator.fields.contains_key(property.name.as_str())
            || validator.properties.contains_key(property.name.as_str())
        {
            validator.diagnostics.push(Diagnostic::error(
                format!("Duplicate class member '{}'", property.name),
                property.range.start,
            ));
        } else {
            validator.properties.insert(&property.name, property);
        }
    }
    let mut calls = HashMap::new();
    for property in &file.properties {
        if let Some(getter) = property.getter() {
            let mut edges = HashSet::new();
            validator.check_reads(
                &getter.expression,
                property.range.start,
                None,
                true,
                &mut edges,
            );
            validator.check_compatible(
                &property.ty,
                &getter.expression,
                property.range.start,
                None,
            );
            calls.insert(format!("get:{}", property.name), edges);
        }
        if let Some(setter) = property.setter() {
            let mut edges = HashSet::new();
            validator.check_body(
                &setter.body,
                property.range.start,
                Some(&property.ty),
                true,
                &mut edges,
            );
            calls.insert(format!("set:{}", property.name), edges);
        }
    }
    if let Some(constructor) = &file.constructor {
        validator.check_body(
            &constructor.body,
            constructor.range.start,
            None,
            true,
            &mut HashSet::new(),
        );
    }
    for chunk in &file.chunks {
        validator.check_body(
            &chunk.body,
            chunk.range.start,
            None,
            false,
            &mut HashSet::new(),
        );
    }
    for archive in file.archives.iter().filter(|a| a.name.is_none()) {
        validator.check_body(
            &archive.body,
            archive.range.start,
            None,
            false,
            &mut HashSet::new(),
        );
    }
    for start in calls.keys() {
        if reaches(start, start, &calls, &mut HashSet::new()) {
            validator.diagnostics.push(Diagnostic::error(
                format!("Recursive property accessor '{start}'"),
                validator.properties[&start[4..]].range.start,
            ));
        }
    }
    validator.properties_allowed = false;
    for archive in archives.iter().filter(|a| a.declaration.name.is_some()) {
        validator.fields = archive.fields.fields.iter().map(|f| (f.name, f)).collect();
        validator.check_body(
            &archive.declaration.body,
            archive.declaration.range.start,
            None,
            false,
            &mut HashSet::new(),
        );
    }
}

fn reaches(
    current: &str,
    target: &str,
    calls: &HashMap<String, HashSet<String>>,
    visited: &mut HashSet<String>,
) -> bool {
    if !visited.insert(current.to_owned()) {
        return false;
    }
    calls.get(current).is_some_and(|edges| {
        edges
            .iter()
            .any(|e| e == target || reaches(e, target, calls, visited))
    })
}

impl PropertyValidator<'_, '_> {
    fn check_body(
        &mut self,
        body: &[BodyStatement],
        position: SourcePosition,
        incoming_type: Option<&TypeReference>,
        require_declared: bool,
        edges: &mut HashSet<String>,
    ) {
        for statement in walk(body) {
            if let BodyStatement::Assignment(assignment) = statement {
                if let Some(field) = self.fields.get(assignment.target_name.as_str()) {
                    self.check_compatible(
                        field.ty,
                        &assignment.expression,
                        assignment.range.start,
                        incoming_type,
                    );
                } else if let Some(property) = self.properties.get(assignment.target_name.as_str())
                {
                    if !self.properties_allowed {
                        self.diagnostics.push(Diagnostic::error(
                            "Class properties cannot be used in named archives",
                            assignment.range.start,
                        ));
                    } else if property.setter().is_none() {
                        self.diagnostics.push(Diagnostic::error(
                            format!("Property '{}' is read-only", property.name),
                            assignment.range.start,
                        ));
                    } else {
                        edges.insert(format!("set:{}", property.name));
                    }
                    self.check_compatible(
                        &property.ty,
                        &assignment.expression,
                        assignment.range.start,
                        incoming_type,
                    );
                } else if require_declared && !self.has_inherited_class {
                    self.diagnostics.push(Diagnostic::error(
                        format!(
                            "Unknown class field or property '{}'",
                            assignment.target_name
                        ),
                        assignment.range.start,
                    ));
                }
            }
            for expression in statement_expressions(statement) {
                self.check_reads(expression, position, incoming_type, require_declared, edges);
            }
        }
    }

    fn check_reads(
        &mut self,
        expression: &Expression,
        position: SourcePosition,
        incoming_type: Option<&TypeReference>,
        require_declared: bool,
        edges: &mut HashSet<String>,
    ) {
        if let Expression::Identifier(name) = expression {
            if self.fields.contains_key(name.as_str()) || incoming_type.is_some() && name == "value"
            {
                return;
            }
            if let Some(property) = self.properties.get(name.as_str()) {
                if !self.properties_allowed {
                    self.diagnostics.push(Diagnostic::error(
                        "Class properties cannot be used in named archives",
                        position,
                    ));
                } else if property.getter().is_none() {
                    self.diagnostics.push(Diagnostic::error(
                        format!("Property '{name}' is write-only"),
                        position,
                    ));
                } else {
                    edges.insert(format!("get:{name}"));
                }
            } else if require_declared
                && !self.fields.contains_key(name.as_str())
                && !self.has_inherited_class
            {
                self.diagnostics.push(Diagnostic::error(
                    format!("Unknown class field or readable property '{name}'"),
                    position,
                ));
            }
        }
        if let Expression::PatternTest { value, pattern } = expression {
            let kind = self.infer_kind(value, incoming_type);
            if kind.is_some_and(|k| !matches!(k, "string" | "array" | "null"))
                && pattern_constants(pattern)
                    .iter()
                    .any(|c| matches!(c, Expression::Literal(Literal::Empty)))
            {
                self.diagnostics.push(Diagnostic::error(
                    "The empty pattern requires a string or array",
                    position,
                ));
            }
            if kind.is_some_and(|k| !matches!(k, "string" | "null"))
                && pattern_constants(pattern)
                    .iter()
                    .any(|c| matches!(c, Expression::Literal(Literal::String(s)) if s == "\"\""))
            {
                self.diagnostics.push(Diagnostic::error(
                    "The empty string pattern requires a string",
                    position,
                ));
            }
        }
        if let Expression::Binary {
            operator: BinaryOp::LogicalAnd | BinaryOp::LogicalOr,
            left,
            right,
        } = expression
        {
            if self
                .infer_kind(left, incoming_type)
                .is_some_and(|k| k != "bool")
                || self
                    .infer_kind(right, incoming_type)
                    .is_some_and(|k| k != "bool")
            {
                self.diagnostics.push(Diagnostic::error(
                    "Logical operators require boolean operands",
                    position,
                ));
            }
        }
        if let Expression::Unary {
            operator: UnaryOp::Not,
            operand,
        } = expression
        {
            if self
                .infer_kind(operand, incoming_type)
                .is_some_and(|k| k != "bool")
            {
                self.diagnostics.push(Diagnostic::error(
                    "Logical operators require boolean operands",
                    position,
                ));
            }
        }
        for child in expression_children(expression) {
            self.check_reads(child, position, incoming_type, require_declared, edges);
        }
    }

    fn infer_kind(
        &self,
        expression: &Expression,
        incoming_type: Option<&TypeReference>,
    ) -> Option<&'static str> {
        match expression {
            Expression::Identifier(name) if name == "value" && incoming_type.is_some() => {
                incoming_type.map(type_kind)
            }
            Expression::Identifier(name) => self
                .fields
                .get(name.as_str())
                .map(|f| type_kind(f.ty))
                .or_else(|| {
                    self.properties_allowed
                        .then(|| self.properties.get(name.as_str()).map(|p| type_kind(&p.ty)))
                        .flatten()
                }),
            Expression::Literal(literal) => match literal {
                Literal::Bool(_) => Some("bool"),
                Literal::String(_) => Some("string"),
                Literal::Null => Some("null"),
                Literal::Integer(_) | Literal::Hex(_) | Literal::Float(_) => Some("number"),
                _ => None,
            },
            Expression::Parenthesized(inner) => self.infer_kind(inner, incoming_type),
            Expression::PatternTest { .. }
            | Expression::Unary {
                operator: UnaryOp::Not,
                ..
            } => Some("bool"),
            Expression::ScopedIdentifier { .. } | Expression::Tuple(_) => Some("other"),
            Expression::Unary { operand, .. } => self.infer_kind(operand, incoming_type),
            Expression::Binary { left, operator, .. } => {
                if matches!(
                    operator,
                    BinaryOp::LogicalAnd
                        | BinaryOp::LogicalOr
                        | BinaryOp::Equal
                        | BinaryOp::NotEqual
                        | BinaryOp::LessThan
                        | BinaryOp::GreaterThan
                        | BinaryOp::LessOrEqual
                        | BinaryOp::GreaterOrEqual
                ) {
                    Some("bool")
                } else {
                    self.infer_kind(left, incoming_type)
                }
            }
        }
    }

    fn check_compatible(
        &mut self,
        target: &TypeReference,
        expression: &Expression,
        position: SourcePosition,
        incoming_type: Option<&TypeReference>,
    ) {
        let expected = type_kind(target);
        let Some(actual) = self.infer_kind(expression, incoming_type) else {
            return;
        };
        if expected == "other" || actual == "other" {
            return;
        }
        if actual == "null" && (target.is_nullable || matches!(expected, "string" | "array")) {
            return;
        }
        if expected != actual {
            self.diagnostics.push(Diagnostic::error(
                "Expression result is incompatible with the target type",
                position,
            ));
        }
    }
}

fn type_kind(ty: &TypeReference) -> &'static str {
    if ty.array_dimensions > 0 {
        return "array";
    }
    if ty.cast_target.is_some() {
        return "other";
    }
    match ty.name.as_str() {
        "bool" => "bool",
        "string" => "string",
        "float" | "double" | "timefloat" => "number",
        name if integer_info(name).is_some() || name == "timeint" => "number",
        _ => "other",
    }
}

fn statement_expressions(statement: &BodyStatement) -> Vec<&Expression> {
    match statement {
        BodyStatement::Field(f) => f.default_value.iter().collect(),
        BodyStatement::Assignment(a) => vec![&a.expression],
        BodyStatement::If(b) => std::iter::once(&b.condition)
            .chain(b.else_ifs.iter().map(|c| &c.condition))
            .collect(),
        BodyStatement::Switch(s) => std::iter::once(&s.expression)
            .chain(s.cases.iter().map(|c| &c.value))
            .collect(),
        BodyStatement::While(l) => vec![&l.condition],
        BodyStatement::Loop(l) => vec![&l.count_expression],
        BodyStatement::Assert(a) | BodyStatement::Skip(a) => vec![&a.expression],
        _ => Vec::new(),
    }
}

fn expression_children(expression: &Expression) -> Vec<&Expression> {
    match expression {
        Expression::Unary { operand, .. } | Expression::Parenthesized(operand) => vec![operand],
        Expression::Binary { left, right, .. } => vec![left, right],
        Expression::Tuple(elements) => elements.iter().collect(),
        Expression::PatternTest { value, pattern } => std::iter::once(value.as_ref())
            .chain(pattern_constants(pattern))
            .collect(),
        _ => Vec::new(),
    }
}

fn pattern_constants(pattern: &Pattern) -> Vec<&Expression> {
    match pattern {
        Pattern::Constant(c) => vec![c],
        Pattern::Not(p) | Pattern::Parenthesized(p) => pattern_constants(p),
        Pattern::And(a, b) | Pattern::Or(a, b) => pattern_constants(a)
            .into_iter()
            .chain(pattern_constants(b))
            .collect(),
    }
}
