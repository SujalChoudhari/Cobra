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
  : CONST type ID ASSIGN assignmentExpression SEMICOLON
  ;

varDeclaration
  : type ID (ASSIGN assignmentExpression)? SEMICOLON
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
  | secondaryType (LBRACKET RBRACKET)*  
  ;
  
secondaryType
  : FUN
  | MARKUP
  | DICT
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
  : IF LPAREN assignmentExpression RPAREN statement (ELSE statement)?
  ;

whileStatement
  : WHILE LPAREN assignmentExpression RPAREN statement
  ;

doWhileStatement
  : DO statement WHILE LPAREN assignmentExpression RPAREN SEMICOLON
  ;

forStatement
  : FOR LPAREN (varDeclaration | expressionStatement | SEMICOLON)? assignmentExpression? SEMICOLON assignmentExpression? RPAREN statement
  ;

forEachStatement
  : FOR LPAREN type ID IN assignmentExpression RPAREN statement
  ;

switchStatement
  : SWITCH LPAREN assignmentExpression RPAREN LBRACE switchBlock* RBRACE
  ;

switchBlock
  : switchLabel+ statement*
  ;

switchLabel
  : CASE assignmentExpression COLON
  | DEFAULT COLON
  ;

tryStatement
  : TRY block (CATCH LPAREN parameter RPAREN block)? (FINALLY block)?
  ;

jumpStatement
  : RETURN assignmentExpression? SEMICOLON
  | BREAK SEMICOLON
  | CONTINUE SEMICOLON
  ;

expressionStatement
  : assignmentExpression SEMICOLON
  ;

printStatement
  : PRINT LPAREN assignmentExpression RPAREN SEMICOLON
  ;

// Expressions (precedence)
assignmentExpression
  : leftHandSide assignmentOperator assignmentExpression
  | binaryExpression (QUESTION assignmentExpression COLON assignmentExpression)?
  ;

binaryExpression
  : ( // logicalOr
      ( // logicalAnd
        ( // bitwiseOr
          ( // bitwiseXor
            ( // bitwiseAnd
              ( // equality
                ( // relational
                  ( // shift
                    ( // additive
                      ( // multiplicative
                        unaryOp* postfixExpression
                        ((MUL | DIV | MOD) unaryOp* postfixExpression)*
                      )
                      ((PLUS | MINUS) unaryOp* postfixExpression)*
                    )
                    ((SHL | SHR) unaryOp* postfixExpression)*
                  )
                  ((GT | LT | GTE | LTE) unaryOp* postfixExpression)*
                )
                ((EQ | NEQ) unaryOp* postfixExpression)*
              )
              (BITWISE_AND unaryOp* postfixExpression)*
            )
            (BITWISE_XOR unaryOp* postfixExpression)*
          )
          (BITWISE_OR unaryOp* postfixExpression)*
        )
        (AND unaryOp* postfixExpression)*
      )
      (OR unaryOp* postfixExpression)*
    )
  ;

unaryOp
  : PLUS | MINUS | NOT | BITWISE_NOT | INC | DEC
  ;

postfixExpression
  : primary ( LPAREN argumentList? RPAREN
            | LBRACKET assignmentExpression RBRACKET
            | DOT ID
            | INC
            | DEC
            )*
  ;

leftHandSide
  : primary ( LBRACKET assignmentExpression RBRACKET
            | DOT ID
            )*
  ;

primary
  : LPAREN assignmentExpression RPAREN
  | literal
  | ID
  | functionExpression
  | arrayLiteral
  | dictLiteral
  ;

argumentList
  : assignmentExpression (COMMA assignmentExpression)*
  ;

functionExpression
  : type LPAREN parameterList? RPAREN block
  ;

arrayLiteral
  : LBRACKET (assignmentExpression (COMMA assignmentExpression)*)? RBRACKET
  ;

dictLiteral
  : LBRACE (dictEntry (COMMA dictEntry)*)? RBRACE
  ;

dictEntry
  : (STRING_LITERAL | ID) COLON assignmentExpression
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

FUN:    'fun';
MARKUP: 'markup';
DICT: 'dict';

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

OPEN_FRAGMENT: '<>';
CLOSE_FRAGMENT: '</>';

ID: [a-zA-Z_] [a-zA-Z_0-9]*;

STRING_LITERAL
  : '"' ALL* '"'
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

fragment ALL
    : ( EscapeSequence | ~('\\'|'"') )
    ;

fragment EscapeSequence
  : '\\' [btnfr"'\\/]
  ;