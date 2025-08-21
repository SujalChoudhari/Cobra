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
PRINT : 'print';

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

// Literals
INTEGER : [0-9]+;
FLOAT_LITERAL : [0-9]+ '.' [0-9]+;
STRING_LITERAL : '"' .*? '"';
BOOLEAN_LITERAL : 'true' | 'false';

// Identifiers
ID : [a-zA-Z_] [a-zA-Z0-9_]*;

// Whitespace and comments
WHITESPACE : [ \t\r\n]+ -> skip;
COMMENT : '//' .*? '\n' -> skip;
BLOCK_COMMENT : '/*' .*? '*/' -> skip;