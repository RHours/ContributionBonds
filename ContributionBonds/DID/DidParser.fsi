// Signature file for parser generated by fsyacc
module internal Did.Parser
type token = 
  | EOF
  | DID_COLON
  | COLON
  | PERIOD
  | DASH
  | UNDERSCORE
  | LOWER_AZ of (char)
  | UPPER_AZ of (char)
  | DIGIT of (char)
type tokenId = 
    | TOKEN_EOF
    | TOKEN_DID_COLON
    | TOKEN_COLON
    | TOKEN_PERIOD
    | TOKEN_DASH
    | TOKEN_UNDERSCORE
    | TOKEN_LOWER_AZ
    | TOKEN_UPPER_AZ
    | TOKEN_DIGIT
    | TOKEN_end_of_input
    | TOKEN_error
type nonTerminalId = 
    | NONTERM__startdid
    | NONTERM_did
    | NONTERM_methodName
    | NONTERM_methodChars
    | NONTERM_methodChar
    | NONTERM_methodSpecificIds
    | NONTERM_methodSpecificId
    | NONTERM_idChars
    | NONTERM_idChar
    | NONTERM_alphaChar
/// This function maps tokens to integer indexes
val tagOfToken: token -> int

/// This function maps integer indexes to symbolic token ids
val tokenTagToTokenId: int -> tokenId

/// This function maps production indexes returned in syntax errors to strings representing the non terminal that would be produced by that production
val prodIdxToNonTerminal: int -> nonTerminalId

/// This function gets the name of a token as a string
val token_to_string: token -> string
val did : (Internal.Utilities.Text.Lexing.LexBuffer<'cty> -> token) -> Internal.Utilities.Text.Lexing.LexBuffer<'cty> -> ( DecentralizedIdentifier ) 
