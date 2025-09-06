grammar Cobra;

/*
  Cobra.g4
  Final grammar for Cobra language (strict-typed, no generics, dict{...}, no sets).
  - Arrays: type[]
  - Dict constructor: dict{ key: value, ... } with unquoted or string keys
  - Function refs use the keyword `fun` for declarations: fun f = int (int a) { ... };
  - Named functions: int name(...) { ... }
  - Markup: JSX-like within a <></> wrapper. Self-closing tags supported.
  - link "file"; import "mod"; external int foo(...);
  - Blocks { } are scopes. dict uses explicit ctor `dict{}` to avoid ambiguity.
*/

options { tokenVocab=LexerTokens; } // optional split lexer approach

/* =========================
   Parser rules
   ========================= */

program
  : (topLevelItem)* EOF
  ;

topLevelItem
  : linkStatement
  | importStatement
  | namespaceDeclaration
  | externDeclaration
  | constDeclaration
  | varDeclaration
  | functionDeclaration
  | statement
  ;

linkStatement
  : LINK STRING_LITERAL SEMICOLON
  ;

importStatement
  : IMPORT STRING_LITERAL SEMICOLON
  ;

/* Namespaces */
namespaceDeclaration
  : NAMESPACE qualifiedName LBRACE (topLevelItem)* RBRACE
  ;

/* Externals */
externDeclaration
  : EXTERNAL type ID LPAREN parameterList? RPAREN SEMICOLON
  ;

/* Declarations */
constDeclaration
  : CONST type ID (ASSIGN expression)? SEMICOLON
  ;

varDeclaration
  : type ID (ASSIGN expression)? SEMICOLON
  ;

/* Named functions */
functionDeclaration
  : type ID LPAREN parameterList? RPAREN block
  ;

/* Statements */
statement
  : block
  | ifStatement
  | whileStatement
  | doWhileStatement
  | forStatement
  | forEachStatement
  | switchStatement
  | tryStatement
  | jumpStatement
  | expressionStatement
  | SEMICOLON
  ;

block
  : LBRACE (declarationOrStatement)* RBRACE
  ;

declarationOrStatement
  : constDeclaration
  | varDeclaration
  | statement
  ;

/* Control flow */
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
  : FOR LPAREN (varDeclaration | expressionStatement | SEMICOLON) expression? SEMICOLON expression? RPAREN statement
  ;

forEachStatement
  : FOR LPAREN type ID IN expression RPAREN statement
  ;

switchStatement
  : SWITCH LPAREN expression RPAREN LBRACE switchGroup* RBRACE
  ;

switchGroup
  : switchLabel+ (statement)*
  ;

switchLabel
  : CASE expression COLON
  | DEFAULT COLON
  ;

tryStatement
  : TRY block (CATCH LPAREN type ID RPAREN block)? (FINALLY block)?
  ;

/* Jumps */
jumpStatement
  : RETURN expression? SEMICOLON
  | BREAK SEMICOLON
  | CONTINUE SEMICOLON
  ;

/* Expressions */
expressionStatement
  : expression SEMICOLON
  ;

expression
  : assignmentExpression
  ;

assignmentExpression
  : conditionalExpression
  | lhs assignmentOperator expression
  ;

/* left-hand side for assignments */
lhs
  : primary ( (DOT ID) | (LBRACKET expression RBRACKET) )*
  ;

/* conditional (ternary) */
conditionalExpression
  : logicalOrExpression (QUESTION expression COLON expression)?
  ;

/* boolean/logical/bitwise/arithmetic precedence */
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
  : shiftExpression ((LT | GT | LTE | GTE) shiftExpression)*
  ;

shiftExpression
  : additiveExpression ((LSHIFT | RSHIFT) additiveExpression)*
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
  : primary ( (LPAREN argumentList? RPAREN) | (DOT ID) | (LBRACKET expression RBRACKET) | INC | DEC )*
  ;

/* Primary expressions (includes literals, identifiers, calls, dict/array/tuple/markup, and function literals) */
primary
  : LPAREN expression RPAREN
  | literal
  | ID
  | functionLiteral
  | arrayLiteral
  | tupleLiteral
  | dictConstructor
  | markupLiteral
  ;

/* Function literal used as expression or as value in declarations:
   - When used as a value, must be assigned to `fun` typed variable.
   - Syntax: type ( paramList? ) block
   Example: int (int a, int b) { return a + b; }
*/
functionLiteral
  : type LPAREN parameterList? RPAREN block
  ;

/* Containers */
arrayLiteral
  : LBRACKET (expression (COMMA expression)*)? RBRACKET
  ;

tupleLiteral
  : LPAREN expression (COMMA expression)+ RPAREN
  ;

/* Explicit dict constructor to avoid clash with blocks */
dictConstructor
  : DICT LBRACE (dictEntry (COMMA dictEntry)*)? RBRACE
  ;

dictEntry
  : (STRING_LITERAL | ID) COLON expression
  ;

/* Markup (JSX-like). Top-level wrapper required: <></> */
markupLiteral
  : LT GT markupNodes LT SLASH GT
  ;

markupNodes
  : (markupElement | markupText | markupExpr)*
  ;

markupText
  : MARKUP_TEXT
  ;

markupExpr
  : LBRACE expression RBRACE
  ;

markupElement
  : LT ID markupAttrs? SLASH GT                       # markupSelfClose
  | LT ID markupAttrs? GT markupNodes LT SLASH ID GT  # markupNormal
  ;

markupAttrs
  : (markupAttr)*
  ;

markupAttr
  : ID ASSIGN (STRING_LITERAL | markupAttrExpr)
  ;

markupAttrExpr
  : LBRACE expression RBRACE
  ;

/* Arguments and parameters */
argumentList
  : expression (COMMA expression)*
  ;

parameterList
  : parameter (COMMA parameter)*
  ;

parameter
  : type ID
  ;

/* Types */
type
  : primitiveType (LBRACKET RBRACKET)*      # arrayStyle
  | FUN                                    # funType        // the special 'fun' keyword as a type for function refs
  | MARKUP_T                               # markupType     // markup type keyword
  ;

// primitive types
primitiveType
  : INT_T
  | FLOAT_T
  | STRING_T
  | BOOL_T
  | VOID_T
  ;

/* Assignment operators */
assignmentOperator
  : ASSIGN | PLUS_ASSIGN | MINUS_ASSIGN | MUL_ASSIGN | DIV_ASSIGN | MOD_ASSIGN
  ;

/* Literals */
literal
  : INTEGER
  | FLOAT_LITERAL
  | STRING_LITERAL
  | TRUE
  | FALSE
  | NULL
  ;

/* Qualified name */
qualifiedName
  : ID (DOT ID)*
  ;

/* =========================
   Lexer rules (inline for portability)
   ========================= */

/* Keywords and type tokens */
LINK:       'link';
IMPORT:     'import';
NAMESPACE:  'namespace';
EXTERNAL:   'external';
CONST:      'const';
FUN:        'fun';
MARKUP_T:   'markup';

INT_T:      'int';
FLOAT_T:    'float';
STRING_T:   'string';
BOOL_T:     'bool';
VOID_T:     'void';

TRUE:       'true';
FALSE:      'false';
NULL:       'null';

TRY:        'try';
CATCH:      'catch';
FINALLY:    'finally';

IF:         'if';
ELSE:       'else';
WHILE:      'while';
DO:         'do';
FOR:        'for';
IN:         'in';
SWITCH:     'switch';
CASE:       'case';
DEFAULT:    'default';
RETURN:     'return';
BREAK:      'break';
CONTINUE:   'continue';

/* Constructors and container keywords */
DICT:       'dict';

/* Operators */
PLUS:       '+';
MINUS:      '-';
MUL:        '*';
DIV:        '/';
MOD:        '%';
PLUS_ASSIGN:'+=';
MINUS_ASSIGN:'-=';
MUL_ASSIGN: '*=';
DIV_ASSIGN: '/=';
MOD_ASSIGN: '%=';
INC:        '++';
DEC:        '--';
ASSIGN:     '=';
EQ:         '==';
NEQ:        '!=';
LTE:        '<=';
GTE:        '>=';
NOT:        '!';
AND:        '&&';
OR:         '||';
BITWISE_AND:'&';
BITWISE_OR: '|';
BITWISE_XOR:'^';
BITWISE_NOT:'~';
LSHIFT:     '<<';
RSHIFT:     '>>';
QUESTION:   '?';
COLON:      ':';

/* Punctuation */
LPAREN:     '(';
RPAREN:     ')';
LBRACE:     '{';
RBRACE:     '}';
LBRACKET:   '[';
RBRACKET:   ']';
SEMICOLON:  ';';
COMMA:      ',';
DOT:        '.';
SLASH:      '/';
LT:         '<';
GT:         '>';
SLASH:      '/';
SLASH_GT:   '/>';

/* Strings and numbers */
STRING_LITERAL
  : '"' ( EscapeSequence | ~('\\'|'"') )* '"'
  ;

fragment EscapeSequence
  : '\\' [btnfr"\\/']
  ;

FLOAT_LITERAL
  : [0-9]+ '.' [0-9]* ([eE] [+-]? [0-9]+)?
  | '.' [0-9]+ ([eE] [+-]? [0-9]+)?
  | [0-9]+ [eE] [+-]? [0-9]+
  ;

INTEGER
  : [0-9]+
  ;

/* Markup text: any run of chars not starting a tag or expr */
MARKUP_TEXT
  : (~[<>{}])+ 
  ;

/* Identifiers */
ID
  : [a-zA-Z_] [a-zA-Z_0-9]*
  ;

/* String literal for import/link */
STRING_LITERAL_RAW
  : '"' ( EscapeSequence | ~('\\'|'"') )* '"'
  ;

/* For import/link we reuse STRING_LITERAL token above; STRING_LITERAL_RAW kept for clarity if needed */

/* Whitespace and comments */
WHITESPACE
  : [ \t\r\n]+ -> skip
  ;

LINE_COMMENT
  : '//' ~[\r\n]* -> skip
  ;

BLOCK_COMMENT
  : '/*' .*? '*/' -> skip
  ;
