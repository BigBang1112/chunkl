use crate::ast::*;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct WriterOptions {
    pub indent: String,
    pub newline: String,
    pub preserve_comments: bool,
}

impl Default for WriterOptions {
    fn default() -> Self {
        Self {
            indent: "  ".to_owned(),
            newline: "\n".to_owned(),
            preserve_comments: true,
        }
    }
}

pub fn write(file: &ChunkLFile) -> String {
    write_with_options(file, &WriterOptions::default())
}

pub fn write_with_options(file: &ChunkLFile, options: &WriterOptions) -> String {
    Writer {
        output: String::new(),
        options,
    }
    .write_file(file)
}

pub fn write_expression(expr: &Expression) -> String {
    let mut writer = Writer {
        output: String::new(),
        options: &WriterOptions::default(),
    };
    writer.expr(expr);
    writer.output
}

struct Writer<'a> {
    output: String,
    options: &'a WriterOptions,
}

impl Writer<'_> {
    fn write_file(mut self, file: &ChunkLFile) -> String {
        self.output.push_str(&file.header.class_name);
        self.output.push(' ');
        self.output.push_str(&file.header.class_id);
        self.comment(file.header.trailing_comment.as_ref());
        self.newline();

        for attribute in &file.class_attributes {
            self.output.push_str("- ");
            self.output.push_str(&attribute.name);
            if let Some(value) = &attribute.value {
                self.output.push_str(": ");
                self.output.push_str(value);
            }
            self.comment(attribute.trailing_comment.as_ref());
            self.newline();
        }

        for comment in &file.top_level_comments {
            self.standalone_comment(comment, 0);
        }
        for chunk in &file.chunks {
            self.newline();
            self.chunk(chunk);
        }
        for archive in &file.archives {
            self.newline();
            self.archive(archive);
        }
        for declaration in &file.enums {
            self.newline();
            self.enum_declaration(declaration);
        }
        for declaration in &file.flags {
            self.newline();
            self.flags_declaration(declaration);
        }
        self.output
    }

    fn chunk(&mut self, chunk: &ChunkDeclaration) {
        self.output.push_str(&chunk.offset.hex_value);
        if let Some(attributes) = &chunk.attributes {
            self.output.push(' ');
            self.attributes(attributes);
        }
        if !chunk.version_qualifiers.is_empty() {
            self.output.push_str(" [");
            for (index, qualifier) in chunk.version_qualifiers.iter().enumerate() {
                if index > 0 {
                    self.output.push_str(", ");
                }
                self.output.push_str(&qualifier.label);
                if let Some(version) = qualifier.max_version {
                    self.output.push_str(&format!(".v{version}"));
                }
            }
            self.output.push(']');
        }
        self.comment(chunk.trailing_comment.as_ref());
        self.newline();
        self.body(&chunk.body, 1);
    }

    fn archive(&mut self, archive: &ArchiveDeclaration) {
        self.output.push_str("archive");
        if let Some(name) = &archive.name {
            self.output.push(' ');
            self.output.push_str(name);
        }
        if let Some(attributes) = &archive.attributes {
            self.output.push(' ');
            self.attributes(attributes);
        }
        self.comment(archive.trailing_comment.as_ref());
        self.newline();
        self.body(&archive.body, 1);
    }

    fn enum_declaration(&mut self, declaration: &EnumDeclaration) {
        self.output.push_str("enum ");
        self.output.push_str(&declaration.name);
        self.comment(declaration.trailing_comment.as_ref());
        self.newline();
        for member in &declaration.members {
            self.indent(1);
            self.output.push_str(&member.name);
            if let Some(value) = &member.explicit_value {
                self.output.push_str(" = ");
                self.output.push_str(value);
            }
            self.comment(member.trailing_comment.as_ref());
            self.newline();
        }
    }

    fn flags_declaration(&mut self, declaration: &FlagsDeclaration) {
        self.output.push_str("flags ");
        self.output.push_str(&declaration.name);
        self.comment(declaration.trailing_comment.as_ref());
        self.newline();
        for member in &declaration.members {
            self.indent(1);
            self.output.push_str(&member.name);
            self.output.push('[');
            self.output.push_str(&member.bits.start.to_string());
            if let Some(end) = member.bits.end {
                self.output.push_str("..");
                self.output.push_str(&end.to_string());
            }
            self.output.push(']');
            self.comment(member.trailing_comment.as_ref());
            self.newline();
        }
    }

    fn body(&mut self, body: &[BodyStatement], level: usize) {
        for statement in body {
            self.statement(statement, level);
        }
    }

    fn statement(&mut self, statement: &BodyStatement, level: usize) {
        if let BodyStatement::Comment(comment) = statement {
            self.standalone_comment(comment, level);
            return;
        }
        self.indent(level);
        match statement {
            BodyStatement::Field(field) => {
                self.type_reference(&field.ty);
                if let Some(name) = &field.name {
                    self.output.push(' ');
                    self.output.push_str(name);
                }
                if let Some(value) = &field.default_value {
                    self.output.push_str(" = ");
                    self.expr(value);
                }
                if let Some(attributes) = &field.attributes {
                    self.output.push(' ');
                    self.attributes(attributes);
                }
                self.comment(field.trailing_comment.as_ref());
                self.newline();
            }
            BodyStatement::VersionCondition(condition) => {
                self.output.push('v');
                self.output.push_str(&condition.version.to_string());
                match condition.kind {
                    VersionConditionKind::GreaterOrEqual => self.output.push('+'),
                    VersionConditionKind::LessOrEqual => self.output.push('-'),
                    VersionConditionKind::Exact => self.output.push('='),
                    VersionConditionKind::Range => {
                        self.output.push_str("..");
                        if let Some(end) = condition.version_end {
                            self.output.push_str(&end.to_string());
                        }
                    }
                }
                self.comment(condition.trailing_comment.as_ref());
                self.newline();
                self.body(&condition.body, level + 1);
            }
            BodyStatement::If(statement) => self.if_statement(statement, level),
            BodyStatement::Return(statement) => self.simple("return", statement),
            BodyStatement::Throw(statement) => self.simple("throw", statement),
            BodyStatement::Skip(statement) => self.expression("skip", statement),
            BodyStatement::Assert(statement) => self.expression("assert", statement),
            BodyStatement::Block(statement) => {
                self.output.push_str("block");
                if let Some(attributes) = &statement.attributes {
                    self.output.push(' ');
                    self.attributes(attributes);
                }
                self.comment(statement.trailing_comment.as_ref());
                self.newline();
                self.body(&statement.body, level + 1);
            }
            BodyStatement::Loop(statement) => {
                self.output.push_str("loop ");
                self.expr(&statement.count_expression);
                self.comment(statement.trailing_comment.as_ref());
                self.newline();
                self.body(&statement.body, level + 1);
            }
            BodyStatement::Switch(statement) => self.switch(statement, level),
            BodyStatement::Assignment(statement) => {
                self.output.push_str(&statement.target_name);
                self.output.push_str(" = ");
                self.expr(&statement.expression);
                self.comment(statement.trailing_comment.as_ref());
                self.newline();
            }
            BodyStatement::Comment(_) => unreachable!(),
        }
    }

    fn if_statement(&mut self, statement: &IfStatement, level: usize) {
        self.output.push_str("if ");
        self.expr(&statement.condition);
        self.comment(statement.trailing_comment.as_ref());
        self.newline();
        self.body(&statement.body, level + 1);
        for clause in &statement.else_ifs {
            self.indent(level);
            self.output.push_str("else if ");
            self.expr(&clause.condition);
            self.comment(clause.trailing_comment.as_ref());
            self.newline();
            self.body(&clause.body, level + 1);
        }
        if let Some(clause) = &statement.else_clause {
            self.indent(level);
            self.output.push_str("else");
            self.comment(clause.trailing_comment.as_ref());
            self.newline();
            self.body(&clause.body, level + 1);
        }
    }

    fn switch(&mut self, statement: &SwitchStatement, level: usize) {
        self.output.push_str("switch ");
        self.expr(&statement.expression);
        self.comment(statement.trailing_comment.as_ref());
        self.newline();
        for case in &statement.cases {
            self.indent(level + 1);
            self.output.push_str("case ");
            self.expr(&case.value);
            self.comment(case.trailing_comment.as_ref());
            self.newline();
            self.body(&case.body, level + 2);
        }
        if let Some(default) = &statement.default {
            self.indent(level + 1);
            self.output.push_str("default");
            self.comment(default.trailing_comment.as_ref());
            self.newline();
            self.body(&default.body, level + 2);
        }
    }

    fn simple(&mut self, keyword: &str, statement: &SimpleStatement) {
        self.output.push_str(keyword);
        if let Some(attributes) = &statement.attributes {
            self.output.push(' ');
            self.attributes(attributes);
        }
        self.comment(statement.trailing_comment.as_ref());
        self.newline();
    }

    fn expression(&mut self, keyword: &str, statement: &ExpressionStatement) {
        self.output.push_str(keyword);
        self.output.push(' ');
        self.expr(&statement.expression);
        if let Some(attributes) = &statement.attributes {
            self.output.push(' ');
            self.attributes(attributes);
        }
        self.comment(statement.trailing_comment.as_ref());
        self.newline();
    }

    fn type_reference(&mut self, ty: &TypeReference) {
        self.output.push_str(&ty.name);
        if let Some(cast) = &ty.cast_target {
            self.output.push('<');
            if let Some(qualifier) = &cast.qualifying_type {
                self.output.push_str(qualifier);
                self.output.push('.');
            }
            self.output.push_str(&cast.name);
            self.output.push('>');
        }
        if ty.chunk_preference {
            self.output.push('*');
        }
        if ty.is_nullable {
            self.output.push('?');
        }
        for dimension in 0..ty.array_dimensions {
            self.output.push('[');
            if dimension == 0 {
                if let Some(count) = &ty.fixed_array_count {
                    self.output.push_str(count);
                }
            }
            self.output.push(']');
        }
    }

    fn attributes(&mut self, attributes: &AttributeList) {
        self.output.push('(');
        for (index, entry) in attributes.entries.iter().enumerate() {
            if index > 0 {
                self.output.push_str(", ");
            }
            self.output.push_str(&entry.name);
            if let Some(value) = &entry.value {
                self.output.push_str(": ");
                self.output.push_str(value);
            }
        }
        self.output.push(')');
    }

    fn standalone_comment(&mut self, comment: &Comment, level: usize) {
        if !self.options.preserve_comments {
            return;
        }
        self.indent(level);
        self.output.push_str(match comment.style {
            CommentStyle::DoubleSlash => "// ",
            CommentStyle::Hash => "# ",
        });
        self.output.push_str(&comment.text);
        self.newline();
    }

    fn comment(&mut self, comment: Option<&Comment>) {
        if !self.options.preserve_comments {
            return;
        }
        if let Some(comment) = comment {
            self.output.push(' ');
            self.output.push_str(match comment.style {
                CommentStyle::DoubleSlash => "// ",
                CommentStyle::Hash => "# ",
            });
            self.output.push_str(&comment.text);
        }
    }

    fn indent(&mut self, level: usize) {
        for _ in 0..level {
            self.output.push_str(&self.options.indent);
        }
    }

    fn newline(&mut self) {
        self.output.push_str(&self.options.newline);
    }

    fn expr(&mut self, expression: &Expression) {
        match expression {
            Expression::Literal(lit) => match lit {
                Literal::Integer(s) | Literal::Hex(s) | Literal::Float(s) => {
                    self.output.push_str(s);
                }
                Literal::String(s) => self.output.push_str(s),
                Literal::Bool(b) => self.output.push_str(if *b { "true" } else { "false" }),
                Literal::Null => self.output.push_str("null"),
                Literal::Empty => self.output.push_str("empty"),
            },
            Expression::Identifier(name) => self.output.push_str(name),
            Expression::ScopedIdentifier { qualifier, name } => {
                self.output.push_str(qualifier);
                self.output.push_str("::");
                self.output.push_str(name);
            }
            Expression::Unary { operator, operand } => {
                self.output.push_str(match operator {
                    UnaryOp::Not => "!",
                    UnaryOp::BitwiseNot => "~",
                    UnaryOp::Negate => "-",
                });
                self.expr(operand);
            }
            Expression::Binary { left, operator, right } => {
                self.expr(left);
                self.output.push_str(match operator {
                    BinaryOp::LogicalOr => " || ",
                    BinaryOp::LogicalAnd => " && ",
                    BinaryOp::Equal => " == ",
                    BinaryOp::NotEqual => " != ",
                    BinaryOp::LessThan => " < ",
                    BinaryOp::GreaterThan => " > ",
                    BinaryOp::LessOrEqual => " <= ",
                    BinaryOp::GreaterOrEqual => " >= ",
                    BinaryOp::BitwiseOr => " | ",
                    BinaryOp::BitwiseXor => " ^ ",
                    BinaryOp::BitwiseAnd => " & ",
                    BinaryOp::ShiftLeft => " << ",
                    BinaryOp::ShiftRight => " >> ",
                    BinaryOp::Add => " + ",
                    BinaryOp::Subtract => " - ",
                    BinaryOp::Multiply => " * ",
                    BinaryOp::Divide => " / ",
                });
                self.expr(right);
            }
            Expression::Parenthesized(inner) => {
                self.output.push('(');
                self.expr(inner);
                self.output.push(')');
            }
            Expression::Tuple(elements) => {
                self.output.push('(');
                for (i, element) in elements.iter().enumerate() {
                    if i > 0 {
                        self.output.push_str(", ");
                    }
                    self.expr(element);
                }
                self.output.push(')');
            }
        }
    }
}
