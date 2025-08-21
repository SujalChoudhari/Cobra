grammar Cobra;

// The starting point of a program file
program
    : (importStatement | functionDeclaration | classDeclaration | declarationStatement | statement)* EOF
    ;

/*
 * =============================================================================
 * 1. Declarations
 * =============================================================================
 */

// Class declaration with inheritance and a body
classDeclaration
    : CLASS ID (EXTENDS typeSpecifier)? LBRACE classBody* RBRACE
    ;

// Body of a class can contain fields, methods, or constructors
classBody
    : memberDeclaration
    ;

memberDeclaration
    : accessModifier? (fieldDeclaration | methodDeclaration | constructorDeclaration)
    ;

fieldDeclaration
    : type ID (ASSIGN expression)? SEMICOLON
    ;

methodDeclaration
    : type ID LPAREN parameterList? RPAREN block
    ;

constructorDeclaration
    : ID LPAREN parameterList? RPAREN block // Constructor name must match class name
    ;

// Function (non-method) declaration
functionDeclaration
    : type ID LPAREN parameterList? RPAREN block
    ;

// Variable declaration statement (can be used inside blocks)
declarationStatement
    : type ID (ASSIGN expression)? SEMICOLON
    ;


/*
 * =============================================================================
 * 2. Statements
 * =============================================================================
 */

statement
    : block
    | declarationStatement
    | assignmentStatement
    | ifStatement
    | whileStatement
    | doWhileStatement
    | forStatement
    | switchStatement
    | jumpStatement
    | expressionStatement
    ;

// A block is a sequence of statements
block
    : LBRACE statement* RBRACE
    ;

// Assignment using various operators to a valid l-value (postfixExpression)
assignmentStatement
    : postfixExpression assignmentOperator expression SEMICOLON
    ;

assignmentOperator
    : ASSIGN | PLUS_ASSIGN | MINUS_ASSIGN | MUL_ASSIGN | DIV_ASSIGN
    ;

expressionStatement
    : expression SEMICOLON
    ;

// Control flow statements
ifStatement
    : IF LPAREN expression RPAREN statement (ELSE statement)?
    ;

whileStatement
    : WHILE LPAREN expression RPAREN statement
    ;

doWhileStatement
    : DO statement WHILE LPAREN expression RPAREN SEMICOLON
    ;

forStatement
    : FOR LPAREN forControl RPAREN statement
    ;

// C-style for loop and for-each loop
forControl
    : declarationStatement expression? SEMICOLON expression? // for(int i=0; i<10; i++)
    | type ID IN expression                           // for(int item in collection)
    ;

// Switch/Case statement
switchStatement
    : SWITCH LPAREN expression RPAREN LBRACE switchBlockStatementGroup* RBRACE
    ;

switchBlockStatementGroup
    : switchLabel+ statement*
    ;

switchLabel
    : CASE expression COLON
    | DEFAULT COLON
    ;

// Return, break, continue
jumpStatement
    : RETURN expression? SEMICOLON
    | BREAK SEMICOLON
    | CONTINUE SEMICOLON
    ;


/*
 * =============================================================================
 * 3. Expressions (ordered by precedence)
 * =============================================================================
 */

expression
    : conditionalExpression
    ;

conditionalExpression // Ternary operator
    : logicalOrExpression (QUESTION_MARK expression COLON expression)?
    ;

logicalOrExpression
    : logicalAndExpression (OR logicalAndExpression)*
    ;

logicalAndExpression
    : bitwiseOrExpression (AND bitwiseOrExpression)*
    ;

bitwiseOrExpression
    : bitwiseXorExpression (BITWISE_OR bitwiseXorExpression)*
    ;

bitwiseXorExpression
    : bitwiseAndExpression (BITWISE_XOR bitwiseAndExpression)*
    ;

bitwiseAndExpression
    : equalityExpression (BITWISE_AND equalityExpression)*
    ;

equalityExpression
    : comparisonExpression ((EQ | NEQ) comparisonExpression)*
    ;

comparisonExpression
    : bitwiseShiftExpression ((GT | LT | GTE | LTE) bitwiseShiftExpression)*
    ;

bitwiseShiftExpression
    : additiveExpression ((BITWISE_LEFT_SHIFT | BITWISE_RIGHT_SHIFT) additiveExpression)*
    ;

additiveExpression
    : multiplicativeExpression ((PLUS | MINUS) multiplicativeExpression)*
    ;

multiplicativeExpression
    : unaryExpression ((MUL | DIV | MOD) unaryExpression)*
    ;

unaryExpression
    : (PLUS | MINUS | NOT | BITWISE_NOT | INC | DEC) unaryExpression
    | postfixExpression
    ;

// Handles function calls, member access, array access, and postfix inc/dec
postfixExpression
    : primary (
        LPAREN argumentList? RPAREN        // Function call
      | LBRACKET expression RBRACKET      // Array access
      | (DOT | ARROW) ID                  // Member access
      | INC | DEC                         // Postfix inc/dec
    )*
    ;

primary
    : LPAREN expression RPAREN
    | literal
    | ID
    | THIS
    | NEW typeSpecifier (LPAREN argumentList? RPAREN | LBRACKET expression RBRACKET) // Object or Array instantiation
    ;

// List of expressions for function calls
argumentList
    : expression (COMMA expression)*
    ;


/*
 * =============================================================================
 * 4. Types, Parameters, and Misc
 * =============================================================================
 */

// A type can be a primitive or a class name, with multiple array dimensions
type
    : typeSpecifier (LBRACKET RBRACKET)*
    ;

// The base name of a type
typeSpecifier
    : primitiveType
    | ID // For class types
    ;

primitiveType
    : INT | FLOAT | STRING | BOOL | VOID
    ;

// Function parameters
parameterList
    : parameter (COMMA parameter)*
    ;

parameter
    : type ID
    ;

accessModifier
    : PUBLIC | PRIVATE | PROTECTED
    ;

importStatement
    : IMPORT ID SEMICOLON
    ;

literal
    : INTEGER
    | FLOAT_LITERAL
    | STRING_LITERAL
    | BOOLEAN_LITERAL
    | NULL
    ;

/*
 * =============================================================================
 * Lexer Tokens
 * =============================================================================
 */

// Keywords
IMPORT: 'import';
CLASS: 'class';
EXTENDS: 'extends';
NEW: 'new';
THIS: 'this';
IF: 'if';
ELSE: 'else';
WHILE: 'while';
DO: 'do';
FOR: 'for';
IN: 'in';
SWITCH: 'switch';
CASE: 'case';
DEFAULT: 'default';
RETURN: 'return';
BREAK: 'break';
CONTINUE: 'continue';
PUBLIC: 'public';
PRIVATE: 'private';
PROTECTED: 'protected';
NULL: 'null';

// Type Keywords
INT: 'int';
FLOAT: 'float';
STRING: 'string';
BOOL: 'bool';
VOID: 'void';

// Literals
INTEGER: [0-9]+;
FLOAT_LITERAL: [0-9]+ '.' [0-9]* | '.' [0-9]+ | [0-9]+ ('e'|'E') ('-'|'+')? [0-9]+;
STRING_LITERAL: '"' ( EscapeSequence | ~('\\'|'"') )* '"';
BOOLEAN_LITERAL: 'true' | 'false';

fragment EscapeSequence
    : '\\' ('b'|'t'|'n'|'f'|'r'|'"'|'\''|'\\') // Standard escape sequences
    ;

// Operators
PLUS: '+';
MINUS: '-';
MUL: '*';
DIV: '/';
MOD: '%';
AND: '&&';
OR: '||';
NOT: '!';
EQ: '==';
NEQ: '!=';
GT: '>';
LT: '<';
GTE: '>=';
LTE: '<=';
ASSIGN: '=';
PLUS_ASSIGN: '+=';
MINUS_ASSIGN: '-=';
MUL_ASSIGN: '*=';
DIV_ASSIGN: '/=';
INC: '++';
DEC: '--';
BITWISE_AND: '&';
BITWISE_OR: '|';
BITWISE_XOR: '^';
BITWISE_NOT: '~';
BITWISE_LEFT_SHIFT: '<<';
BITWISE_RIGHT_SHIFT: '>>';
DOT: '.';
ARROW: '->';
QUESTION_MARK: '?';

// Punctuation
LPAREN: '(';
RPAREN: ')';
LBRACE: '{';
RBRACE: '}';
LBRACKET: '[';
RBRACKET: ']';
SEMICOLON: ';';
COMMA: ',';
COLON: ':';

// Identifiers
ID: [a-zA-Z_] [a-zA-Z_0-9]*;

// Ignored tokens
WHITESPACE: [ \t\r\n]+ -> skip;
COMMENT: '//' ~[\r\n]* -> skip;
BLOCK_COMMENT: '/*' .*? '*/' -> skip;