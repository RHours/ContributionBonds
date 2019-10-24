module internal Json.Api

open Json.Parser

open System
open System.IO
open System.Security.Cryptography

let private Base58chars = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz"
let private FiftyEight = bigint 58
let private CheckSumSizeInBytes = 4

// https://gist.github.com/CodesInChaos/3175971
let private GetCheckSum(data: byte array) : byte array =
    use sha256 = SHA256.Create()
    let hash1 = sha256.ComputeHash(data)
    let hash2 = sha256.ComputeHash(hash1)
    let result = Array.create<byte> CheckSumSizeInBytes 0uy
    Array.blit hash2 0 result 0 CheckSumSizeInBytes
    result

let private VerifyAndRemoveCheckSum (data: byte array) : byte array option =
    let dataLength = (Array.length data)
    let result =  Array.sub data 0 (dataLength - CheckSumSizeInBytes)

    let start = dataLength - CheckSumSizeInBytes
    let givenCheckSum = Array.sub data start (dataLength - start)

    let correctCheckSum = GetCheckSum result

    if givenCheckSum = correctCheckSum then
        Some(result)
    else
        None

let ToBase58 (data: byte array) : string =
    let mutable dataInt = bigint 0

    for i = 0 to data.Length - 1 do
        dataInt <- dataInt * (bigint 256) + (bigint (int (data.[i])))

    let sb = System.Text.StringBuilder()
    while dataInt > (bigint 0) do
        let remainder = int (dataInt % FiftyEight)
        dataInt <- dataInt / FiftyEight
        sb.Append(Base58chars.[remainder]) |> ignore

    let mutable i0 = 0
    while i0 < data.Length && data.[i0] = 0uy do
        sb.Append('1') |> ignore

    String(Array.rev ((sb.ToString()).ToCharArray()))

let ToBase58WithCheckSum (data: byte array) : string =
    let checkSum = GetCheckSum(data)
    let dataWithCheckSum = Array.append data checkSum
    ToBase58 dataWithCheckSum

let FromBase58 (s: string) =
    // Decode Base58 string to BigInteger 
    let mutable intData = bigint 0

    for i = 0 to s.Length - 1 do
        let digit = Base58chars.IndexOf(s.[i])
        if (digit < 0) then
            failwith (String.Format("Invalid Base58 character `{0}` at position {1}", s.[i], i))
        else
            intData <- intData * FiftyEight + (bigint digit)

    // Encode BigInteger to byte[]
    // Leading zero bytes get encoded as leading `1` characters
    let leadingZeroCount = s.ToCharArray() |> Array.takeWhile (fun c -> c = '1') |> Array.length
    let leadingZeros = Array.create<byte> leadingZeroCount 0uy
    let bytesWithoutLeadingZeros =
        intData.ToByteArray() |>
        Array.rev |>
        Array.skipWhile (fun b -> b = 0uy)
    
    Array.append leadingZeros bytesWithoutLeadingZeros
    
let FromBase58WithCheckSum (s: string) = 
    let dataWithCheckSum = FromBase58 s
    let dataWithoutCheckSum = VerifyAndRemoveCheckSum(dataWithCheckSum);
    
    match dataWithoutCheckSum with
    | None -> 
        raise (FormatException("Base58 checksum is invalid"))
    | Some(_) ->
        dataWithoutCheckSum

let rec private WriteCanonicalJson (json: Json.Parser.JsonValue) (tw: TextWriter) (isRoot: bool) =
    match json with
    | JsonValue.JsonObject(o) -> 
        let properties = 
            (
                if isRoot then
                    o |>
                    Array.filter (fun (n, _) -> n <> "proof")
                else
                    o
            ) |> Array.sortBy (fun (n, _) -> n)

        tw.Write('{')
        if properties.Length > 0 then
            for i = 0 to properties.Length - 1 do
                let (n, v) = properties.[i]
                tw.Write('\"')
                tw.Write(n)
                tw.Write('\"')
                tw.Write(':')
                WriteCanonicalJson v tw false

                if i < properties.Length - 2 then
                    tw.Write(',')
        tw.Write('}')
    | _ when isRoot ->
        failwith "Only a root object can be written in canonical format."
    | JsonValue.JsonArray(a) -> 
        tw.Write('[')
        if a.Length > 0 then
            for i = 0 to a.Length - 2 do
                WriteCanonicalJson (a.[i]) tw false
                tw.Write(',')
            WriteCanonicalJson (a.[a.Length - 1]) tw false
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
        
let private tab = "    "
let private WriteIndent (tw: TextWriter) (indent: int) = 
    for i = 1 to indent do
        tw.Write(tab)

let rec private WriteFormattedJson (json: Json.Parser.JsonValue) (tw: TextWriter) (indent: int) =
    match json with
    | JsonValue.JsonObject(properties) -> 
        match properties.Length with
        | 0 -> 
            tw.Write("{ }")

        | length ->
            tw.WriteLine('{')
            for i = 0 to length - 1 do
                let (n, v) = properties.[i]
                WriteIndent tw (indent + 1)
                tw.Write('\"')
                tw.Write(n)
                tw.Write('\"')
                tw.Write(": ")
                WriteFormattedJson v tw (indent + 1)
                
                if i < length - 1 then
                    tw.WriteLine(',')
                else
                    tw.WriteLine()

            WriteIndent tw indent
            tw.Write('}')

    | JsonValue.JsonArray(elements) -> 
        match elements.Length with
        | 0 -> 
            tw.Write("[ ]")

        | length ->
            tw.WriteLine('[')

            for i = 0 to length - 2 do
                WriteIndent tw (indent + 1)
                WriteFormattedJson (elements.[i]) tw (indent + 1)
                tw.WriteLine(',')

            WriteIndent tw (indent + 1)
            WriteFormattedJson (elements.[length - 1]) tw (indent + 1)
            tw.WriteLine()
            WriteIndent tw indent
            tw.Write(']')

    | JsonValue.JsonString(s) -> 
        tw.Write("\"")
        tw.Write(s.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\"", "\\"))
        tw.Write("\"")
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

let WriteJson (json: JsonValue) (tw: TextWriter) =
    WriteFormattedJson json tw 0

let ReadJson (tr: TextReader) =
    let lexbuf = Internal.Utilities.Text.Lexing.LexBuffer<_>.FromTextReader(tr)
    Json.Parser.json Json.Lexer.json lexbuf

let SignJsonEmbedded (rsa: RSA) (json: Json.Parser.JsonValue) (creator: string) =
    // Get the canonical bytes
    // Prepend with a nonce
    // Hash them 
    // Sign them
    // Update the document with a "proof" element

    let jsonArray = 
        match json with
        | JsonValue.JsonObject(o) -> o
        | _ -> failwith "Can only sign a JsonObject."

    use sw = new StringWriter()
    WriteCanonicalJson json sw true
    let canonicalBytes = System.Text.UTF8Encoding.UTF8.GetBytes(sw.ToString())

    use rng = RandomNumberGenerator.Create()
    let nonceBytes = Array.create<byte> 32 0uy
    rng.GetBytes(nonceBytes)

    let data = Array.append nonceBytes canonicalBytes

    use sha256 = SHA256.Create()
    let hashBytes = sha256.ComputeHash(data)

    let rsaFormatter = RSAPKCS1SignatureFormatter(rsa);
    rsaFormatter.SetHashAlgorithm("SHA256")

    let signatureBytes = rsaFormatter.CreateSignature(hashBytes)

    let proofObject = 
        let proofArray = 
            [|
                ("type", (JsonValue.JsonString("RsaSignature2018")));
                ("creator", (JsonValue.JsonString(creator)));
                ("created", (JsonValue.JsonString(DateTime.UtcNow.ToString("s", System.Globalization.CultureInfo.InvariantCulture))));
                ("nonce", (JsonValue.JsonString(System.Convert.ToBase64String(nonceBytes))));
                ("proofValue", (JsonValue.JsonString(System.Convert.ToBase64String(signatureBytes))));
            |]

        JsonValue.JsonObject(proofArray)

    match jsonArray |> Array.tryFindIndex (fun (n, _) -> n = "proof") with
    | None ->
        // no proof property, add one with the new proofObject as the value
        JsonValue.JsonObject(Array.append jsonArray [| ("proof", proofObject) |])
    | Some(index) ->
        match jsonArray.[index] with
        | (_, JsonValue.JsonObject(existingProofObject) ) ->
            // existing proof property is object, replace it with an array of proofs with this new one at the end
            let proofArray = JsonValue.JsonArray([| JsonValue.JsonObject(existingProofObject); proofObject; |])
            jsonArray.[index] <- ("proof", proofArray)
            json
        | (_, JsonValue.JsonArray(proofElements)) ->
            // existing proof property is an array, append this proofObject to it
            let proofArray = JsonValue.JsonArray(Array.append proofElements [| proofObject; |])        
            jsonArray.[index] <- ("proof", proofArray)
            json
        | _ ->
            failwith "proof property must be object or array."

let VerifyJsonSignature (json: JsonValue) (resolver: string -> byte[]) (proof: JsonValue option) : JsonValue option =
    // get canonical bytes
    // for each proof, either the one sent or the proof property
    //      construct new bytes with nonce in front
    //      resolve the creator to get the public key
    //      verify the signature
    //      return the proof which verifies or none

    let proofSeq =
        seq {
            match proof with
            | Some(JsonValue.JsonObject(_) as proofObject) -> 
                // Caller sent explicit proof object, use that
                yield proofObject
            | Some(JsonValue.JsonArray(a)) ->
                // Caller sent an array of proof objects, use these in order provided
                for v in a do
                    match v with
                    | JsonValue.JsonObject(_) as proofObject ->
                        yield proofObject
                    | _ ->
                        failwith "all elements of proof set must be objects."
            | Some(_) ->
                failwith "proof value must be object or array of objects."
            | None ->
                // Caller did not provide proof, look in json data for proof property
                match json with
                | JsonValue.JsonObject(jsonProperties) ->
                    match jsonProperties |> Array.tryPick (fun (n, v) -> if n = "proof" then Some(v) else None) with
                    | Some(JsonValue.JsonObject(_) as v) ->
                        // There is a proof property and it is an object
                        yield v
                    | Some(JsonValue.JsonArray(jsonProofArray)) ->
                        // There is a proof property and it is an array, use these in reverse order
                        for i = (Array.length jsonProofArray) - 1 downto 0 do
                            match jsonProofArray.[i] with
                            | JsonValue.JsonObject(_) as jsonProofObject ->
                                yield jsonProofObject
                            | _ ->
                                failwith "all elements of proof set must be objects."
                    | Some(_) ->
                        failwith "proof value must be object or array of objects."
                    | None ->
                        failwith "no proof to verify signature with."
                | _ ->
                    failwith "only signed json objects may be verified."
        }

    use sw = new StringWriter()
    WriteCanonicalJson json sw true
    let canonicalBytes = System.Text.UTF8Encoding.UTF8.GetBytes(sw.ToString())

    use sha256 = SHA256.Create()

    let VerifyProof (proof: JsonValue) = 
        let proofProperties =
            match proof with
            | JsonValue.JsonObject(o) -> o
            | _ -> failwith "proof value must be of type object."

        //      construct new bytes with nonce in front
        let bytesWithNonce = 
            match proofProperties |> Array.tryPick (fun (n, v) -> if n = "nonce" then Some(v) else None) with
            | Some(JsonValue.JsonString(nonceString)) ->
                let nonceBytes = System.Convert.FromBase64String(nonceString)
                Array.append nonceBytes canonicalBytes
            | Some(_) ->
                failwith "nonce must be a string."
            | None ->
                canonicalBytes

        let hashBytes = sha256.ComputeHash(bytesWithNonce)

        let signatureBytes = 
            match proofProperties |> Array.tryPick (fun (n, v) -> if n = "proofValue" then Some(v) else None) with
            | Some(JsonValue.JsonString(signatureString)) ->
                System.Convert.FromBase64String(signatureString)
            | Some(_) ->
                failwith "proofValue must be a string."
            | None ->
                failwith "no proofValue found."

        //      resolve the creator to get the public key
        let creatorPublicKey =
            match proofProperties |> Array.tryPick (fun (n, v) -> if n = "creator" then Some(v) else None) with
            | Some(JsonValue.JsonString(creatorString)) ->
                resolver creatorString
            | Some(_) ->
                failwith "creator must be a string."
            | None ->
                failwith "unable to get public key to verify with."

        //      verify the signature
        use key = CngKey.Import(creatorPublicKey, CngKeyBlobFormat.GenericPublicBlob)
        use rsa = new RSACng(key)
        let deformatter = RSAPKCS1SignatureDeformatter(rsa)
        deformatter.SetHashAlgorithm("SHA256")
        deformatter.VerifySignature(hashBytes, signatureBytes)

    // Return the first proof which verifies, or None
    proofSeq |>
    Seq.tryFind VerifyProof

    
    