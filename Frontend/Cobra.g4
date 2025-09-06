grammar Cobra;

// Entry
program
  : (linkStatement
    | importStatement
    | namespaceDeclaration
    | topLevelDeclaration
    | statement
    )* EOF
  ;

// Modules / Linking / Import
linkStatement
  : LINK STRING_LITERAL SEMICOLON
  ;

importStatement
  : IMPORT STRING_LITERAL SEMICOLON
  ;

// Namespaces
namespaceDeclaration
  : NAMESPACE qualifiedName LBRACE (topLevelDeclaration | statement | externDeclaration)* RBRACE
  ;

qualifiedName
  : ID (DOT ID)*
  ;

// Top-level declarations
topLevelDeclaration
  : constDeclaration
  | varDeclaration
  | functionDeclaration
  | externDeclaration
  ;

constDeclaration
  : CONST type ID ASSIGN expression SEMICOLON
  ;

varDeclaration
  : type ID (ASSIGN expression)? SEMICOLON
  ;

functionDeclaration
  : type ID LPAREN parameterList? RPAREN block
  ;

externDeclaration
  : EXTERNAL type ID LPAREN parameterList? RPAREN SEMICOLON
  ;

// Types & Parameters
type
  : primitiveType (LBRACKET RBRACKET)*  
  | FUN                                 
  | ID                                  
  ;

primitiveType
  : INT
  | FLOAT
  | BOOL
  | STRING
  | VOID
  ;

parameterList
  : parameter (COMMA parameter)*
  ;

parameter
  : type ID
  ;

// Statements & Blocks
statement
  : block
  | declarationStatement
  | ifStatement
  | whileStatement
  | doWhileStatement
  | forStatement
  | forEachStatement
  | switchStatement
  | tryStatement
  | jumpStatement
  | expressionStatement
  | printStatement
  ;

declarationStatement
  : constDeclaration
  | varDeclaration
  ;

block
  : LBRACE (declarationStatement | statement)* RBRACE
  ;

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
  : FOR LPAREN (varDeclaration | expressionStatement | SEMICOLON)? expression? SEMICOLON expression? RPAREN statement
  ;

forEachStatement
  : FOR LPAREN type ID IN expression RPAREN statement
  ;

switchStatement
  : SWITCH LPAREN expression RPAREN LBRACE switchBlock* RBRACE
  ;

switchBlock
  : switchLabel+ statement*
  ;

switchLabel
  : CASE expression COLON
  | DEFAULT COLON
  ;

tryStatement
  : TRY block (CATCH LPAREN parameter RPAREN block)? (FINALLY block)?
  ;

jumpStatement
  : RETURN expression? SEMICOLON
  | BREAK SEMICOLON
  | CONTINUE SEMICOLON
  ;

expressionStatement
  : expression SEMICOLON
  ;

printStatement
  : PRINT LPAREN expression RPAREN SEMICOLON
  ;

// Expressions (precedence)
expression
  : assignmentExpression
  ;

assignmentExpression
  : conditionalExpression
  | leftHandSide assignmentOperator assignmentExpression
  ;

conditionalExpression
  : logicalOrExpression (QUESTION expression COLON expression)?
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
  : relationalExpression ((EQ | NEQ) relationalExpression)*
  ;

relationalExpression
  : shiftExpression ((GT | LT | GTE | LTE) shiftExpression)*
  ;

shiftExpression
  : additiveExpression ((SHL | SHR) additiveExpression)*
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

postfixExpression
  : primary ( LPAREN argumentList? RPAREN
            | LBRACKET expression RBRACKET
            | DOT ID
            | INC
            | DEC
            )*
  ;

leftHandSide
  : primary ( LBRACKET expression RBRACKET
            | DOT ID
            )*
  ;

primary
  : LPAREN expression RPAREN
  | literal
  | ID
  | functionExpression
  | arrayLiteral
  | dictLiteral
  ;

argumentList
  : expression (COMMA expression)*
  ;

functionExpression
  : type LPAREN parameterList? RPAREN block
  ;

arrayLiteral
  : LBRACKET (expression (COMMA expression)*)? RBRACKET
  ;

dictLiteral
  : LBRACE (dictEntry (COMMA dictEntry)*)? RBRACE
  ;

dictEntry
  : (STRING_LITERAL | ID) COLON expression
  ;

literal
  : INTEGER
  | FLOAT_LITERAL
  | STRING_LITERAL
  | BACKTICK_STRING
  | TRUE
  | FALSE
  | NULL
  ;

assignmentOperator
  : ASSIGN
  | PLUS_ASSIGN
  | MINUS_ASSIGN
  | MUL_ASSIGN
  | DIV_ASSIGN
  | MOD_ASSIGN
  ;

// Lexer
LINK:       'link';
IMPORT:     'import';
NAMESPACE:  'namespace';
EXTERNAL:   'external';
FUN:        'fun';
CONST:      'const';
IF:         'if';
ELSE:       'else';
WHILE:      'while';
DO:         'do';
FOR:        'for';
IN:         'in';
SWITCH:     'switch';
CASE:       'case';
DEFAULT:    'default';
TRY:        'try';
CATCH:      'catch';
FINALLY:    'finally';
RETURN:     'return';
BREAK:      'break';
CONTINUE:   'continue';
PRINT:      'print';

INT:    'int';
FLOAT:  'float';
STRING: 'string';
BOOL:   'bool';
VOID:   'void';
NULL:   'null';
TRUE:   'true';
FALSE:  'false';

PLUS:           '+';
MINUS:          '-';
MUL:            '*';
DIV:            '/';
MOD:            '%';
PLUS_ASSIGN:    '+=';
MINUS_ASSIGN:   '-=';
MUL_ASSIGN:     '*=';
DIV_ASSIGN:     '/=';
MOD_ASSIGN:     '%=';
INC:            '++';
DEC:            '--';
ASSIGN:         '=';
EQ:             '==';
NEQ:            '!=';
GT:             '>';
LT:             '<';
GTE:            '>=';
LTE:            '<=';
NOT:            '!';
AND:            '&&';
OR:             '||';
BITWISE_AND:    '&';
BITWISE_OR:     '|';
BITWISE_XOR:    '^';
BITWISE_NOT:    '~';
SHL:            '<<';
SHR:            '>>';
QUESTION:       '?';
SLASH:          '/';

LPAREN:     '(';
RPAREN:     ')';
LBRACE:     '{';
RBRACE:     '}';
LBRACKET:   '[';
RBRACKET:   ']';
SEMICOLON:  ';';
COMMA:      ',';
COLON:      ':';
DOT:        '.';

ID: [a-zA-Z_] [a-zA-Z_0-9]*;

STRING_LITERAL
  : '"' ( EscapeSequence | ~('\\'|'"') )* '"'
  ;

BACKTICK_STRING
  : '`' ( ~'`' | '``' )* '`'
  ;

INTEGER
  : [0-9]+
  ;

FLOAT_LITERAL
  : [0-9]+ '.' [0-9]* ([eE][+-]?[0-9]+)?
  | '.' [0-9]+ ([eE][+-]?[0-9]+)?
  | [0-9]+ [eE][+-]?[0-9]+
  ;

LINE_COMMENT: '//' ~[\r\n]* -> skip;
BLOCK_COMMENT: '/*' .*? '*/' -> skip;
WS: [ \t\r\n]+ -> skip;

fragment EscapeSequence
  : '\\' [btnfr"'\\/]
  ;