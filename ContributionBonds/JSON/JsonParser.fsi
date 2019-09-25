// Signature file for parser generated by fsyacc
module internal Json.Parser
type token = 
  | EOF
  | TRUE
  | FALSE
  | NULL
  | LBRACE
  | RBRACE
  | LBRACKET
  | RBRACKET
  | COMMA
  | COLON
  | STRING of (string)
  | INTEGER of (string)
  | FRACTION of (string)
  | EXPONENT of (string)
type tokenId = 
    | TOKEN_EOF
    | TOKEN_TRUE
    | TOKEN_FALSE
    | TOKEN_NULL
    | TOKEN_LBRACE
    | TOKEN_RBRACE
    | TOKEN_LBRACKET
    | TOKEN_RBRACKET
    | TOKEN_COMMA
    | TOKEN_COLON
    | TOKEN_STRING
    | TOKEN_INTEGER
    | TOKEN_FRACTION
    | TOKEN_EXPONENT
    | TOKEN_end_of_input
    | TOKEN_error
type nonTerminalId = 
    | NONTERM__startjson
    | NONTERM_json
    | NONTERM_value
    | NONTERM_object
    | NONTERM_members
    | NONTERM_jsonmember
    | NONTERM_array
    | NONTERM_elements
    | NONTERM_element
    | NONTERM_string
    | NONTERM_number
/// This function maps tokens to integer indexes
val tagOfToken: token -> int

/// This function maps integer indexes to symbolic token ids
val tokenTagToTokenId: int -> tokenId

/// This function maps production indexes returned in syntax errors to strings representing the non terminal that would be produced by that production
val prodIdxToNonTerminal: int -> nonTerminalId

/// This function gets the name of a token as a string
val token_to_string: token -> string
val json : (Internal.Utilities.Text.Lexing.LexBuffer<'cty> -> token) -> Internal.Utilities.Text.Lexing.LexBuffer<'cty> -> ( obj ) 
