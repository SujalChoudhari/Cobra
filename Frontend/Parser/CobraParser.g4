parser grammar CobraParser;

options { tokenVocab=CobraLexer; }

// The main entry point for a Cobra program
program : (importStatement | statement)* EOF;

// 1. Basic Statements (Arithmetic, Assignment, etc.)
statement : declarationStatement
          | assignmentStatement
          | expressionStatement
          | returnStatement
          | controlStatement
          | block
          | functionDeclaration
          ;

declarationStatement : type ID (ASSIGN expression)? SEMICOLON;
assignmentStatement : ID ASSIGN expression SEMICOLON;
expressionStatement : expression SEMICOLON;

// Expressions (for arithmetic, logical, and comparison)
expression : logicalOrExpression;

logicalOrExpression : logicalAndExpression (OR logicalAndExpression)*;
logicalAndExpression : comparisonExpression (AND comparisonExpression)*;

comparisonExpression : arithmeticExpression ((EQ | NEQ | GT | LT | GTE | LTE) arithmeticExpression)?;

arithmeticExpression : multiplicationExpression ((PLUS | MINUS) multiplicationExpression)*;

multiplicationExpression : unaryExpression ((MUL | DIV | MOD) unaryExpression)*;

unaryExpression : (MINUS | NOT)? primary;

primary : INTEGER
        | FLOAT_LITERAL
        | STRING_LITERAL
        | BOOLEAN_LITERAL
        | ID
        | LPAREN expression RPAREN
        | functionCall
        ;

// 2. Control Structures
controlStatement : ifStatement | whileLoop | forLoop;

ifStatement : IF LPAREN expression RPAREN block (ELSE block)?;
whileLoop : WHILE LPAREN expression RPAREN block;
forLoop : FOR LPAREN ID IN rangeExpression RPAREN block;
rangeExpression : expression COLON expression;

block : LBRACE statement* RBRACE;

// 3. Function Definition and Call
functionDeclaration : type ID LPAREN parameterList? RPAREN block;
returnStatement : RETURN expression? SEMICOLON;
functionCall : ID LPAREN argumentList? RPAREN;

parameterList : parameter (COMMA parameter)*;
parameter : type ID;
argumentList : expression (COMMA expression)*;

// 4. Types
type : INT | FLOAT | STRING | BOOL | VOID;

// 5. Import Statement
importStatement : IMPORT ID SEMICOLON;