lexer grammar CobraLexer;

// Keywords
INT : 'int';
FLOAT : 'float';
STRING : 'string';
BOOL : 'bool';
VOID : 'void';
IMPORT : 'import';
IF : 'if';
ELSE : 'else';
WHILE : 'while';
FOR : 'for';
IN : 'in';
RETURN : 'return';
NULL : 'null';
CLASS : 'class';
NEW : 'new';
THIS : 'this';

// Operators
PLUS : '+';
MINUS : '-';
MUL : '*';
DIV : '/';
MOD : '%';
AND : '&&';
OR : '||';
NOT : '!';
EQ : '==';
NEQ : '!=';
GT : '>';
LT : '<';
GTE : '>=';
LTE : '<=';
ASSIGN : '=';
PLUS_ASSIGN : '+=';
MINUS_ASSIGN : '-=';
MUL_ASSIGN : '*=';
DIV_ASSIGN : '/=';
INC : '++';
DEC : '--';
BITWISE_AND : '&';
BITWISE_OR : '|';
BITWISE_XOR : '^';
BITWISE_NOT : '~';
BITWISE_LEFT_SHIFT : '<<';
BITWISE_RIGHT_SHIFT : '>>';
DOT : '.';
ARROW : '->';

// Punctuation
LPAREN : '(';
RPAREN : ')';
LBRACE : '{';
RBRACE : '}';
LBRACKET : '[';
RBRACKET : ']';
SEMICOLON : ';';
COMMA : ',';
COLON : ':';
QUOTE : '"';
QUESTION_MARK : '?';

// Literals
INTEGER : [0-9]+;
FLOAT_LITERAL : [0-9]+ '.' [0-9]+;
STRING_LITERAL : '"' ( ESCAPED_QUOTE | . )*? '"'; // Improved with escaped quotes
fragment ESCAPED_QUOTE : '\\"';
BOOLEAN_LITERAL : 'true' | 'false';

// Identifiers
ID : [a-zA-Z_] [a-zA-Z0-9_]*;

// Whitespace and comments
WHITESPACE : [ \t\r\n]+ -> skip;
COMMENT : '//' .*? '\n' -> skip;
BLOCK_COMMENT : '/*' .*? '*/' -> skip;