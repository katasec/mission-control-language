grammar FmsGrammar;

// Parser rules

program
    : useDecl* (letBinding | declaration | outputDecl)* EOF
    ;

useDecl
    : USE STRING
    ;

letBinding
    : LET LOWER_ID EQUALS value
    ;

declaration
    : mission
    | expert
    ;

outputDecl
    : OUTPUT LPAREN UPPER_ID (COMMA STRING)? RPAREN
    ;

mission
    : MISSION UPPER_ID params? EQUALS pipeline
    ;

expert
    : EXPERT UPPER_ID params? EQUALS pipeline
    ;

params
    : LPAREN LOWER_ID (COMMA LOWER_ID)* RPAREN
    ;

pipeline
    : step (PIPE step)*
    ;

step
    : UPPER_ID withClause?
    ;

withClause
    : WITH LBRACE binding (COMMA binding)* RBRACE
    ;

binding
    : LOWER_ID EQUALS value
    ;

value
    : STRING
    | LOWER_ID
    | envCall
    ;

envCall
    : ENV LPAREN STRING (COMMA STRING)? RPAREN
    ;

// Lexer rules — keywords before LOWER_ID so they take priority

USE     : 'use'     ;
LET     : 'let'     ;
MISSION : 'mission' ;
EXPERT  : 'expert'  ;
WITH    : 'with'    ;
ENV     : 'env'     ;
OUTPUT  : 'output'  ;
PIPE    : '|>'      ;
EQUALS  : '='       ;
LPAREN  : '('       ;
RPAREN  : ')'       ;
LBRACE  : '{'       ;
RBRACE  : '}'       ;
COMMA   : ','       ;

UPPER_ID
    : [A-Z][a-zA-Z0-9]*
    ;

LOWER_ID
    : [a-z][a-zA-Z0-9]*
    ;

STRING
    : '"' (~["\r\n])* '"'
    ;

WS
    : [ \t\r\n]+ -> skip
    ;
