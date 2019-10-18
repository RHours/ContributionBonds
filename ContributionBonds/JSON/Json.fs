module internal Json.Api

open System
open System.IO

open Json.Parser

let rec private PrivateWriteCanonicalJson (json: Json.Parser.JsonValue) (tw: TextWriter) (isRoot: bool) =
    match json with
    | JsonValue.JsonObject(o) -> 
        // Note: toArray orders the keys
        let properties = 
            if isRoot then
                Map.toArray o |> 
                Array.filter (fun (n, _) -> n <> "proof")
            else
                Map.toArray o

        tw.Write('{')
        if properties.Length > 0 then
            for i = 0 to properties.Length - 1 do
                let (n, v) = properties.[i]
                tw.Write('\"')
                tw.Write(n)
                tw.Write('\"')
                tw.Write(':')
                PrivateWriteCanonicalJson v tw false

                if i < properties.Length - 2 then
                    tw.Write(',')
        tw.Write('}')
    | _ when isRoot ->
        failwith "Only a root object can be written in canonical format."
    | JsonValue.JsonArray(a) -> 
        tw.Write('[')
        if a.Length > 0 then
            for i = 0 to a.Length - 2 do
                PrivateWriteCanonicalJson (a.[i]) tw false
                tw.Write(',')
            PrivateWriteCanonicalJson (a.[a.Length - 1]) tw false
        tw.Write(']')
    | JsonValue.JsonString(s) -> 
        tw.Write(s.Replace("\\", "\\\\"))
    | JsonValue.JsonNumber(n) ->
        match n with
        | JsonNumber.JsonInteger(i) -> 
            tw.Write(Convert.ToString(i))
        | JsonNumber.JsonFloat(f) -> 
            tw.Write(Convert.ToString(f))
    | JsonValue.JsonBool(b) -> 
        if b then tw.Write("true") else tw.Write("false")
    | JsonValue.JsonNull -> 
        tw.Write("null")
        
let WriteCanonicalJsonBytes (json: Json.Parser.JsonValue) =
    use sw = new StringWriter()
    PrivateWriteCanonicalJson json sw true
    System.Text.UTF8Encoding.UTF8.GetBytes(sw.ToString())

let ReadJson (tr: TextReader) =
    let lexbuf = Internal.Utilities.Text.Lexing.LexBuffer<_>.FromTextReader(tr)
    Json.Parser.json Json.Lexer.json lexbuf
    
    