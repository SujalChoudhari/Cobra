parser grammar CobraParser;

options { tokenVocab=CobraLexer; }

// Main entry point for a Cobra program
program : (importStatement | classDeclaration | functionDeclaration | statement)* EOF;

// 1. Statements and Declarations
statement : declarationStatement
          | assignmentStatement
          | expressionStatement
          | functionCallStatement
          | returnStatement
          | controlStatement
          | block
          ;

// Global/Local variable declaration
declarationStatement : type ID (ASSIGN expression)? SEMICOLON;

// Assignment statements, now supporting compound operators and member access
assignmentStatement : (ID | memberAccess) (ASSIGN | PLUS_ASSIGN | MINUS_ASSIGN | MUL_ASSIGN | DIV_ASSIGN) expression SEMICOLON;

// Expressions as a standalone statement
expressionStatement : expression SEMICOLON;

// Function call as a statement
functionCallStatement : functionCall SEMICOLON;

// 2. Expressions (including new operations)
expression : logicalOrExpression;

logicalOrExpression : logicalAndExpression (OR logicalAndExpression)*;
logicalAndExpression : bitwiseOrExpression (AND bitwiseOrExpression)*;

// New: Bitwise operations
bitwiseOrExpression : bitwiseXorExpression (BITWISE_OR bitwiseXorExpression)*;
bitwiseXorExpression : bitwiseAndExpression (BITWISE_XOR bitwiseAndExpression)*;
bitwiseAndExpression : equalityExpression (BITWISE_AND equalityExpression)*;

// New: Equality and Comparison are now separate levels
equalityExpression : comparisonExpression ((EQ | NEQ) comparisonExpression)?;
comparisonExpression : bitwiseShiftExpression ((GT | LT | GTE | LTE) bitwiseShiftExpression)?;

// New: Bitwise shift operations
bitwiseShiftExpression : arithmeticExpression ((BITWISE_LEFT_SHIFT | BITWISE_RIGHT_SHIFT) arithmeticExpression)*;

arithmeticExpression : multiplicationExpression ((PLUS | MINUS) multiplicationExpression)*;
multiplicationExpression : unaryExpression ((MUL | DIV | MOD) unaryExpression)*;

// Unary operators including increment/decrement
unaryExpression : (PLUS | MINUS | NOT | BITWISE_NOT | INC | DEC)? primary;

primary : literal
        | ID
        | LPAREN expression RPAREN
        | functionCall
        | memberAccess
        | arrayAccess
        | NEW type LPAREN argumentList? RPAREN // New: Object instantiation
        | THIS // New: 'this' keyword
        ;

// New: Helper rule for all literals
literal : INTEGER
        | FLOAT_LITERAL
        | STRING_LITERAL
        | BOOLEAN_LITERAL
        | NULL
        ;

// New: Member access (object.member or object->member)
memberAccess : (ID | functionCall) (DOT ID | ARROW ID)*;

// New: Array access (array[index])
arrayAccess : (ID | functionCall | memberAccess) LBRACKET expression RBRACKET;

// 3. Control Structures
controlStatement : ifStatement | whileLoop | forLoop;

ifStatement : IF LPAREN expression RPAREN block (ELSE block)?;
whileLoop : WHILE LPAREN expression RPAREN block;
forLoop : FOR LPAREN ID IN expression RPAREN block; // Expression for iterable collection
block : LBRACE statement* RBRACE;

// 4. Function and Class Definitions
functionDeclaration : type ID LPAREN parameterList? RPAREN block;
returnStatement : RETURN expression? SEMICOLON;
functionCall : ID LPAREN argumentList? RPAREN;

parameterList : parameter (COMMA parameter)*;
parameter : type ID;
argumentList : expression (COMMA expression)*;

// New: Class declaration
classDeclaration : CLASS ID LBRACE (declarationStatement | functionDeclaration)* RBRACE;

// 5. Types (now with arrays)
type : (INT | FLOAT | STRING | BOOL | VOID) (LBRACKET RBRACKET)?; // New: Array types

// 6. Import statement remains the same
importStatement : IMPORT ID SEMICOLON;