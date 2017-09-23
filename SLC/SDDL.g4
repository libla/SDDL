grammar SDDL;

defines:
	(require)? ((message|typedef|rpc|constant) ';'?)* EOF
	;
require:
	K_REQUIRE '{' STRING* '}'
	;
constant:
	(K_AUTO|K_LOCAL|K_INTEGER|K_NUMBER|K_STRING|K_BOOLEAN) NAME '=' builtin
	;
message:
	NAME '{' (entry ';'?)+ '}'
	;
typedef:
	NAME '[' (alias ';'?)+ ']'
	;
rpc:
	NAME '(' (call ';'?)+ ')'
	;
entry:
	(K_INTEGER|K_NUMBER|K_STRING|K_BOOLEAN|NAME) NAME PLACE assign?
	;
alias:
	NAME PLACE '=' (K_DELETE|K_NULL|K_INTEGER|K_NUMBER|K_STRING|K_BOOLEAN|NAME)
	;
call:
	NAME PLACE '=' (K_DELETE|(K_INTEGER|K_NUMBER|K_STRING|K_BOOLEAN|NAME)? (RETURN (K_INTEGER|K_NUMBER|K_STRING|K_BOOLEAN|NAME))?)
	;
assign:
	'=' (K_DELETE|K_OPTION|K_ARRAY|K_TABLE|builtin)
	;
builtin:
	NAME|or|numeric|string
	;
or:
	and ('|' and)*
	;
and:
	boolean ('&' boolean)*
	;
boolean:
	NOT? (K_TRUE|K_FALSE|NAME|numeric (CMP|EQ) numeric|string EQ string|'(' or ')')
	;
numeric:
	mulexp (ADD mulexp)*
	;
mulexp:
	powexp (MUL powexp)*
	;
powexp:
	atom ('^' atom)*
	;
atom:
	HEX|FLOAT|INTEGER|NAME|'(' numeric ')'
	;
string:
	STRING|NAME|(STRING|NAME) '..' string
	;
COMMENT:
	'#' (~('\r'|'\n'|'\f'))* -> skip
	;
NEWLINE:
	('\r\n'|'\n'|'\r'|'\f')+ -> skip
	;
BLANK:
	(' '|'\t'|'\u000B')+ -> skip
	;
RETURN:
	'->'
	;
CMP:
	'<='|'>='|'<'|'>'
	;
EQ:
	'=='|'!='
	;
ADD:
	'+'|'-'
	;
MUL:
	'*'|'/'|'%'
	;
NOT:
	'!'
	;
K_REQUIRE:
	'require'
	;
K_AUTO:
	'auto'
	;
K_LOCAL:
	'local'
	;
K_OPTION:
	'option'
	;
K_ARRAY:
	'array'
	;
K_TABLE:
	'table'
	;
K_DELETE:
	'delete'
	;
K_NULL:
	'null'
	;
K_TRUE:
	'true'
	;
K_FALSE:
	'false'
	;
K_INTEGER:
	'integer'
	;
K_NUMBER:
	'number'
	;
K_STRING:
	'string'
	;
K_BOOLEAN:
	'boolean'
	;
NAME:
	('a'..'z'|'A'..'Z'|'_')('a'..'z'|'A'..'Z'|'_'|'0'..'9')*
	;
HEX:
	'0'('X'|'x')('a'..'f'|'A'..'F'|'0'..'9')+
	;
INTEGER:
	'0'|('-')? ('1'..'9') ('0'..'9')*
	;
FLOAT:
	DECIMAL EXP?|INTEGER EXP
	;
STRING:
	'"' (ESCAPE|~('\\'|'"'))* '"'|'\'' (ESCAPE|~('\\'|'\''))* '\''
	;
PLACE:
	'@'('0'..'9')+
	;

fragment DECIMAL:
	(INTEGER|('-''0'))'.'('0'..'9')+
	;
fragment EXP:
	('e'|'E')('+'|'-')?('0'..'9')+
	;
fragment ESCAPE:
	'\\'('t'|'r'|'n'|'f'|'\\'|'"'|'\'')|UNICODE
	;
fragment UNICODE:
	'\\''u'('a'..'f'|'A'..'F'|'0'..'9')('a'..'f'|'A'..'F'|'0'..'9')('a'..'f'|'A'..'F'|'0'..'9')('a'..'f'|'A'..'F'|'0'..'9')
	;