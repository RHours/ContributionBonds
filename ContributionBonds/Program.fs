(*
To do: 
    Generate ID/Key pair
            cb --id

    Sign a revenue bond
            cb --sign ( --company=key | --contributor=key )

    Make a payment to a revenue bond
            cb --pay --amount=999.99 <bond-file>

    Verify revenue bond
            cb --verify <bond-file>

    Canonical format for JSON signing
        use my json parser
        sort the labels
        use json-ld signing?

*)

open System
open System.Security.Cryptography
open System.Text
open Json.Parser
open Json.Api


let (|CommandCreateIdentifier|_|) (argv:string[]) =
    // Generate ID/Key pair
    //      cb --id
    if argv.Length = 2 then
        match argv.[1] with
        | "--id" | "-id" | "id" ->
            Some()
        | _ ->
            None
    else
        None

let (|CommandSignBond|_|) (argv:string[]) =
    // Sign a revenue bond
    //      cb --sign ( --company | --contributor ) --bond=bond-file
    if argv.Length = 4 then
        match argv.[1] with
        | "--sign" | "-sign" | "sign"->
            let mutable companyIsSigning : bool = false
            let mutable contributorIsSigning : bool = false
            let mutable bondFile : string option = None

            for i = 2 to 3 do
                match argv.[i] with
                | "--company" ->
                    companyIsSigning <- true
                | "--contributor" ->
                    contributorIsSigning <- true
                | "--bond=" ->
                    let bondFI = System.IO.FileInfo(argv.[i].Substring(7))
                    bondFile <- 
                        if bondFI.Exists then
                            Some(bondFI.FullName)
                        else
                            None
                | _ ->
                    ()

            if (companyIsSigning || contributorIsSigning) && 
                not(companyIsSigning && contributorIsSigning) && 
                bondFile.IsSome then

                Some(None, companyIsSigning, contributorIsSigning, bondFile)
            else
                Some(Some("error"), false, false, None)
        | _ ->
            None
    else
        None


let DateTimeToString (date: DateTime) =
    date.ToString("yyyy-MM-ddThh:mm:ss")

let MakePEMString (data: byte array) (label: string) =
    // label example: PRIVATE KEY
    let sb = System.Text.StringBuilder()
    sb.AppendLine(sprintf "-----BEGIN %s-----" label)
        .AppendLine(System.Convert.ToBase64String(data))
        .AppendLine(sprintf "-----END %s-----" label)
        .ToString()

let ParsePEMString (pem: string) =
    let pemRegex = System.Text.RegularExpressions.Regex("""[\-]+BEGIN (?<label>[^\-]+)[\-]+\r?\n?(?<key>[A-Za-z0-9\+\/\=]*)\r?\n?[\-]+END [^\-]+[\-]+\r?\n?""")
    let result = pemRegex.Match(pem)
    if result.Success then
        (result.Groups.[1].Value, result.Groups.[2].Value)
    else
        failwith "could not parse pem"

let DIDFileName (did: string) = did.Replace(":", "_")

let CreateRhoursDID () = 
    use rng = RandomNumberGenerator.Create()
    let idBytes = Array.create<byte> 16 0uy
    rng.GetBytes(idBytes)
    let didMethodSpecifcId = ToBase58WithCheckSum idBytes
    sprintf "did:rhours:%s" didMethodSpecifcId
    
let CreateIdentityDID (path: System.IO.DirectoryInfo) = 
    // generate a random id of 32 bytes
    // generate a key pair
    // create a PEM string with the private key
    // create a DID string with the public key
    // create a DID document

    let didString = CreateRhoursDID()

    use rsa = new RSACng()
    let keyPrivate = rsa.Key.Export(CngKeyBlobFormat.GenericPrivateBlob)
    let keyPublic = rsa.Key.Export(CngKeyBlobFormat.GenericPublicBlob)

    // TO DO: Look into .net secure strings
    let privatePEM = MakePEMString keyPrivate "PRIVATE KEY"
    let publicPEM = MakePEMString keyPublic "PUBLIC KEY"

    let didAuthentication = 
        JsonValue.JsonObject(
            [|
                ("id", JsonValue.JsonString(didString + "#keys-1"));
                ("type", JsonValue.JsonString("RsaVerificationKey2018"));
                ("controller", JsonValue.JsonString(didString));
                ("publicKeyPem", JsonValue.JsonString(publicPEM));
            |]
        )

    let didDocument = 
        JsonValue.JsonObject(
            [|
                ("@context", JsonValue.JsonString("https://www.w3.org/2019/did/v1"));
                ("id", JsonValue.JsonString(didString));
                ("authentication", didAuthentication);
            |]
        )

    let didFileNameBase = didString.Replace(":", "_")
    let pemFullFileName = System.IO.Path.Combine(path.FullName, didFileNameBase + ".pem")
    let didFullFileName = System.IO.Path.Combine(path.FullName, didFileNameBase + ".json")

    use didPrivatePemFile = System.IO.File.CreateText(pemFullFileName)
    didPrivatePemFile.Write(privatePEM)
    didPrivatePemFile.Flush()
    didPrivatePemFile.Close()
    
    use didDocFile = System.IO.File.CreateText(didFullFileName)
    WriteJson didDocument didDocFile
    didDocFile.Flush()
    didDocFile.Close()

    // return the DID string
    didString

let CreateBond  (path: System.IO.DirectoryInfo)
                (terms_file: string) 
                (company_did: string) 
                (contributor_did: string) 
                (amount: decimal)
                (rate: decimal)
                (max: decimal) =
    (*
{
    "@context": "https://github.com/RHours/ContributionBonds",
    "id": "BOND_DID",
    "terms": "TERMS_HASH_STRING",
    "company": "COMPANY_DID",
    "contributor": "CONTRIBUTOR_DID",
    "created": "yyyy-MM-ddThh:mm:ss",
    "amount": 100.00,
    "interest-rate": 0.25,
    "max": 1000.00,
    "unit": "USD",
    "payments": 
        [
            {
                "date": "yyyy-MM-ddThh:mm:ss",
                "interest": 16.52,
                "amount": 40.00,
                "balance": 76.52
            }
        ]
}
    *)

    let bondDidString = CreateRhoursDID()
    let createdString = DateTimeToString (DateTime.UtcNow)

    let termsBytes = 
        if System.IO.File.Exists(terms_file) then
            use tf = System.IO.File.OpenText(terms_file)
            let terms = tf.ReadToEnd()
            System.Text.UTF8Encoding.UTF8.GetBytes(terms)
        else
            failwith "terms file not found."

    use sha256 = SHA256.Create()
    let terms_hash = sha256.ComputeHash(termsBytes)

    let jsonBond = 
        JsonValue.JsonObject(
            [|
                ("@context", JsonValue.JsonString("https://github.com/RHours/ContributionBonds"));
                ("id", JsonValue.JsonString(bondDidString));
                ("terms", JsonValue.JsonString(System.Convert.ToBase64String(terms_hash)));
                ("company", JsonValue.JsonString(company_did));
                ("contributor", JsonValue.JsonString(contributor_did));
                ("created", JsonValue.JsonString(createdString));
                ("amount", JsonValue.JsonString(amount.ToString()));
                ("interest-rate", JsonValue.JsonString(rate.ToString()));
                ("max", JsonValue.JsonString(max.ToString()));
                ("unit", JsonValue.JsonString("USD"));
                ("payments", JsonValue.JsonArray([||]));
            |]
        )

    use bondFile = System.IO.File.CreateText(System.IO.Path.Combine(path.FullName, bondDidString.Replace(":", "_") + ".json"))
    WriteJson jsonBond bondFile
    bondFile.Flush()
    bondFile.Close()

    // Return the bond DID string
    bondDidString

let SignBond (bondFile: string) (didFile: string) (privateKeyFile: string) =
    // Parse bond file
    // Parse did document, this is the DID document of the person signing
    //          it must match either the company or contributor DID of the bond
    // Parse private key file
    // Create RSA with private key
    // Call SignJsonEmbedded (rsa: RSA) (json: Json.Parser.JsonValue) (creator: string)
    // Write updated json bond

    use bondTR = System.IO.File.OpenText(bondFile)
    let bondJsonObject = 
        match ReadJson bondTR with
        | JsonValue.JsonObject(o) -> o
        | _ -> failwith "bond must be a json object."
    bondTR.Close()

    // get the company did of the bond
    let companyDID = 
        match bondJsonObject |> Array.tryPick (fun (n, v) -> if n = "company" then Some(v) else None) with
        | Some(JsonValue.JsonString(s)) -> s
        | _ -> failwith "unable to get company DID from bond."

    // get the contributor did of the bond
    let contributorDID = 
        match bondJsonObject |> Array.tryPick (fun (n, v) -> if n = "contributor" then Some(v) else None) with
        | Some(JsonValue.JsonString(s)) -> s
        | _ -> failwith "unable to get contributor DID from bond."

    use didTR = System.IO.File.OpenText(didFile)
    let didJsonObject = 
        match ReadJson didTR with
        | JsonValue.JsonObject(o) -> o
        | _ -> failwith "did document must be json object."

    // get the signing did from the did document "id" property
    let signingDID = 
        match didJsonObject |> Array.tryPick (fun (n, v) -> if n = "id" then Some(v) else None) with
        | Some(JsonValue.JsonString(s)) -> s
        | _ -> failwith "unable to get id from did document."

    // Validate, the id of the DID document must match the DID of either the company or the contributor
    if not(signingDID = companyDID || signingDID = contributorDID) then
        failwith "signing DID must match one of either the bond company or contributor DIDs."

    // get the keyType, controller and public key from the did document
    let authenticationObject = 
        match didJsonObject |> Array.tryPick (fun (n, v) -> if n = "authentication" then Some(v) else None) with
        | Some(JsonValue.JsonObject(o)) -> o
        | _ -> failwith "unable to get 'authentication' property from did document."

    let keyType =
           match authenticationObject |> Array.tryPick (fun (n, v) -> if n = "type" then Some(v) else None) with
           | Some(JsonValue.JsonString(s)) -> s
           | _ -> failwith "unable to get 'publicKeyPem' property from did document."

    if keyType <> "RsaVerificationKey2018" then
        failwith "key type must be 'RsaVerificationKey2018'."

    let controllerDID =
        match authenticationObject |> Array.tryPick (fun (n, v) -> if n = "controller" then Some(v) else None) with
        | Some(JsonValue.JsonString(s)) -> s
        | _ -> failwith "unable to get 'controller' property from did document."

    let authenticationId =
        match authenticationObject |> Array.tryPick (fun (n, v) -> if n = "id" then Some(v) else None) with
        | Some(JsonValue.JsonString(s)) -> s
        | _ -> failwith "unable to get 'authentication.id' property from did document."

    // Validate, the controller DID must match the signing DID
    if not(signingDID = controllerDID) then
        failwith "the signing DID must match the controller DID."

    let publicKeyPem =
           match authenticationObject |> Array.tryPick (fun (n, v) -> if n = "publicKeyPem" then Some(v) else None) with
           | Some(JsonValue.JsonString(s)) -> s
           | _ -> failwith "unable to get 'publicKeyPem' property from did document."

    let (_, pubKeyString) = ParsePEMString publicKeyPem
    let pubKeyBytes = System.Convert.FromBase64String(pubKeyString)

    let pemString = System.IO.File.ReadAllText(privateKeyFile)
    let (_, keyString) = ParsePEMString pemString

    let key = CngKey.Import(System.Convert.FromBase64String(keyString), CngKeyBlobFormat.GenericPrivateBlob)

    // public key in the did document must match the key derived from the private key
    if pubKeyBytes <> key.Export(CngKeyBlobFormat.GenericPublicBlob) then
        failwith "Public key does not match public key in did document."
    
    use rsa = new RSACng(key)
    // Call SignJsonEmbedded (rsa: RSA) (json: Json.Parser.JsonValue) (creator: string)
    let signedBondJson = SignJsonEmbedded rsa (JsonValue.JsonObject(bondJsonObject)) authenticationId

    // Write updated json bond
    use bondTW = System.IO.File.CreateText(bondFile)
    WriteJson signedBondJson bondTW
    bondTW.Flush()
    bondTW.Close()

let VerifyBond (path: System.IO.DirectoryInfo) (bondFile: string) : (DateTime * DateTime) option =
    // VerifyJsonSignature (json: JsonValue) (resolver: string -> byte[]) (proof: JsonValue option) : JsonValue option
    // needs to be signed by company
    // needs to be signed by contributor
    // date of signature needs to be later than bond created
    // date of signature needs to be later than all payments

    // open bond file
    // get company and contributor ids from the bond
    // get a sequence of proofs from the bond where the proof.creator = company (and then do same for contributor)
    //      order these by created, decending
    // run VerifyJsonSignature bondJson resolver proof
    //      resolver takes the creator DID string and returns the bytes of the public key

    // for the first proof which returns Some, then verify the dates
    // return (Some(date-company signed), Some(date-contributor signed))

    use bondTR = System.IO.File.OpenText(bondFile)
    let bondJson = ReadJson bondTR
    let bondJsonObject = 
        match bondJson with
        | JsonValue.JsonObject(o) -> o
        | _ -> failwith "bond must be a json object."
    bondTR.Close()

    // get the company did of the bond
    let companyDID = 
        match bondJsonObject |> Array.tryPick (fun (n, v) -> if n = "company" then Some(v) else None) with
        | Some(JsonValue.JsonString(s)) -> s
        | _ -> failwith "unable to get company DID from bond."

    // get the contributor did of the bond
    let contributorDID = 
        match bondJsonObject |> Array.tryPick (fun (n, v) -> if n = "contributor" then Some(v) else None) with
        | Some(JsonValue.JsonString(s)) -> s
        | _ -> failwith "unable to get contributor DID from bond."

    let bondCreatedDate =
        match bondJsonObject |> Array.tryPick (fun (n, v) -> if n = "created" then Some(v) else None) with
        | Some(JsonValue.JsonString(s)) -> DateTime.ParseExact(s, "s", System.Globalization.CultureInfo.InvariantCulture)
        | _ -> failwith "unable to get created date from bond."

    let latestPaymentDate = 
        match bondJsonObject |> Array.tryPick (fun (n, v) -> if n = "payments" then Some(v) else None) with
        | Some(JsonValue.JsonArray(paymentsArray)) ->
            paymentsArray |> 
                Array.fold
                    (
                        fun maxDate p -> 
                            match p with 
                            | JsonValue.JsonObject(paymentObj) -> 
                                match paymentObj |> Array.tryPick (fun (n, v) -> if n = "date" then Some(v) else None) with
                                | Some(JsonValue.JsonString(s)) -> DateTime.ParseExact(s, "s", System.Globalization.CultureInfo.InvariantCulture)
                                | _ -> failwith "unable to get date from payment"
                            | _ -> failwith "payment element must be an object."
                    )
                    DateTime.MinValue
        | _ -> failwith "unable to get payments array from bond."

    // get the proof array of the bond
    let proofArray = 
        match bondJsonObject |> Array.tryPick (fun (n, v) -> if n = "proof" then Some(v) else None) with
        | Some(JsonValue.JsonArray(a)) -> a
        | _ -> failwith "unable to get proof array from bond."

    let ProofsByCreatorDecendingByCreated (creator: string) : JsonValue seq =
        proofArray |>
            Seq.filter 
                (
                    fun e -> 
                        match e with
                        | JsonValue.JsonObject(proofObj) -> 
                            // get the creator property
                            let proofCreator = 
                                match proofObj |> Array.tryPick (fun (n, v) -> if n = "creator" then Some(v) else None) with
                                | Some(JsonValue.JsonString(s)) -> s
                                | _ -> failwith "creator must be a string."
                            // TODO: This is a bug. We don't have proper DID URL handling yet
                            // what we need to verify is that the DID URL at bond.proof.creator is a valid key for the creator
                            proofCreator.StartsWith(creator)
                        | _ -> failwith "proof must be an object."
                ) |>
            Seq.sortByDescending
                (
                    fun e -> 
                        match e with
                        | JsonValue.JsonObject(proofObj) -> 
                            // get the creator property
                            let proofCreated = 
                                match proofObj |> Array.tryPick (fun (n, v) -> if n = "created" then Some(v) else None) with
                                | Some(JsonValue.JsonString(s)) -> s
                                | _ -> failwith "creator must be a string."
                            proofCreated
                        | _ -> failwith "proof must be an object."
                )

    let resolver : (string -> byte array) = 
        // takes a DID URL, returns a public key associated with that id
        fun (didUrl: string) -> 
            let didDocFile = 
                if didUrl.StartsWith(companyDID) then
                    System.IO.Path.Combine(path.FullName, (DIDFileName companyDID) + ".json")
                elif didUrl.StartsWith(contributorDID) then
                    System.IO.Path.Combine(path.FullName, (DIDFileName contributorDID) + ".json")
                else
                    failwith "cannot resolve DID"
            
            // parse the DID document
            use tr = System.IO.File.OpenText(didDocFile)
            let jsonDid = ReadJson tr
            tr.Close()

            // get the public key in this file that matches the didUrl
            match jsonDid with
            | JsonValue.JsonObject(jsonDidObject) ->
                match jsonDidObject |> Array.tryPick ( fun (n, v) -> if n = "id" then Some(v) else None) with
                | Some(JsonValue.JsonString(didId)) ->
                    // TODO: bug, we need proper DID URL handling
                    if didUrl.StartsWith(didId) then
                        // get the authentication value
                        match jsonDidObject |> Array.tryPick ( fun (n, v) -> if n = "authentication" then Some(v) else None) with
                        | Some(JsonValue.JsonObject(authObj)) ->
                            // check the authentication.id = didUrl
                            match authObj |> Array.tryPick ( fun (n, v) -> if n = "id" then Some(v) else None) with
                            | Some(JsonValue.JsonString(id)) when id = didUrl ->
                                // get the publicKeyPem value
                                match authObj |> Array.tryPick ( fun (n, v) -> if n = "publicKeyPem" then Some(v) else None) with
                                | Some(JsonValue.JsonString(publicKeyPem)) ->
                                    let (_, key) = ParsePEMString publicKeyPem
                                    System.Convert.FromBase64String(key)
                                | _ -> failwith "can't get publicKeyPem from DID"
                            | _ -> failwith "can't confirm authentication.id value"
                        | _ -> failwith "unable to get authentication value"
                    else 
                        failwith "DID document id doesn't match."

                | _ -> failwith "problem with DID document, couldn't get id property"
            | _ -> failwith "DID document must be an object."
            
    let companySignatureProof = 
        (ProofsByCreatorDecendingByCreated companyDID) |>
        Seq.tryPick 
            (
                fun proof -> VerifyJsonSignature bondJson resolver (Some(proof))
            )

    let contributorSignatureProof = 
        (ProofsByCreatorDecendingByCreated contributorDID) |>
        Seq.tryPick 
            (
                fun proof -> VerifyJsonSignature bondJson resolver (Some(proof))
            )

    match (companySignatureProof, contributorSignatureProof) with
    | (Some(JsonValue.JsonObject(companyObj)), Some(JsonValue.JsonObject(contributorObj))) ->
        let companyProofDate = 
            match companyObj |> Array.tryPick (fun (n, v) -> if n = "created" then Some(v) else None) with
            | Some(JsonValue.JsonString(s)) -> DateTime.ParseExact(s, "s", System.Globalization.CultureInfo.InvariantCulture)
            | _ -> failwith "proof.created must be string."

        let contributorProofDate = 
            match contributorObj |> Array.tryPick (fun (n, v) -> if n = "created" then Some(v) else None) with
            | Some(JsonValue.JsonString(s)) -> DateTime.ParseExact(s, "s", System.Globalization.CultureInfo.InvariantCulture)
            | _ -> failwith "proof.created must be string."

        if not(companyProofDate >= bondCreatedDate) then
            // Company signature must be later than bond creation date
            None
        elif not(contributorProofDate >= bondCreatedDate) then
            // Contributor signature must be later than bond creation date
            None
        elif companyProofDate = DateTime.MinValue then
            // Company proof date cannot be date minvalue
            failwith "invalid company proof date"
        elif contributorProofDate = DateTime.MinValue then
            // Contributor proof date cannot be date minvalue
            failwith "invalid contributor proof date"
        elif not(companyProofDate >= latestPaymentDate) then
            // Company signature must be later than all payments date
            None
        elif not(contributorProofDate >= latestPaymentDate) then
            // Contributor signature must be later than all payments date
            None
        else
            Some(companyProofDate, contributorProofDate)
    | _ ->
        None

[<EntryPoint>]
let main argv = 
    Internal.Utilities.Text.Parsing.Flags.debug <- false

    let dataDir = System.IO.DirectoryInfo("..\\..\\..\\Data")

    let mode = 2

    match mode with
    | 1 -> 
        // Create a company DID
        let companyDID = CreateIdentityDID(dataDir)

        // Create a contributor DID
        let contributorDID = CreateIdentityDID(dataDir)

        // Create a bond
        let bondDID = CreateBond 
                            dataDir                 // (path: System.IO.DirectoryInfo)
                            "..\\..\\..\\README.md" // (terms_file: string) 
                            companyDID              // (company_did: string) 
                            contributorDID          // (contributor_did: string) 
                            (decimal(100.00))       // (amount: decimal)
                            (decimal(0.25))         // (rate: decimal)
                            (decimal(1000.00))      // (max: decimal)

        let bondFile = System.IO.Path.Combine(dataDir.FullName, (DIDFileName bondDID) + ".json")
        let companyPemFile = System.IO.Path.Combine(dataDir.FullName, (DIDFileName companyDID)  + ".pem")
        let companyDidFile = System.IO.Path.Combine(dataDir.FullName, (DIDFileName companyDID)  + ".json")
        let contributorPemFile = System.IO.Path.Combine(dataDir.FullName, (DIDFileName contributorDID) + ".pem")
        let contributorDidFile = System.IO.Path.Combine(dataDir.FullName, (DIDFileName contributorDID) + ".json")

        // company signs the bond
        SignBond bondFile companyDidFile companyPemFile

        // contributor signs the bond
        SignBond bondFile contributorDidFile contributorPemFile
    | 2 -> 
        let bondFile = System.IO.Path.Combine(dataDir.FullName, "did_rhours_4FWQibuQKrWVnF1A2e23HSotLbDw.json")
    
        // Verify the bond
        let result = VerifyBond dataDir bondFile
        printf "%A" result

    0 // return an integer exit code

