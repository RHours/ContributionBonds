module BondApi

open System
open System.Security.Cryptography

open Json.Parser
open Json.Api


let private CreateSubdirectory (rootDir: System.IO.DirectoryInfo) (name: string) =
    match rootDir.GetDirectories(name) with
    | [| |] ->
        rootDir.CreateSubdirectory(name)
    | [| d; |] -> 
        d
    | _ ->
        failwith "Error creating subdirectory"

let private TryParseRhoursDID (did:string) =
    let regex = System.Text.RegularExpressions.Regex("(did:rhours:.*)")
    let result = regex.Match(did)
    if result.Success then
        Some(result.Groups.[0].Value)
    else
        None    

let private TryParseRhoursDIDFileName (filename: string) =
    let regex = System.Text.RegularExpressions.Regex("did_rhours_(.*)\.json")
    let result = regex.Match(filename)
    if result.Success then
        Some("did:rhours:" + result.Groups.[1].Value)
    else
        None

let private CreateRhoursDID (idBytes: byte array) = 
    let didMethodSpecifcId = ToBase58WithCheckSum idBytes
    sprintf "did:rhours:%s" didMethodSpecifcId
    
let private CreateIdentityDID (path: System.IO.DirectoryInfo) = 
    // generate a random id of 16 bytes
    // generate a key pair
    // create a PEM string with the private key
    // create a DID string with the public key
    // create a DID document

    use rsa = new RSACng()
    let keyPrivate = rsa.Key.Export(CngKeyBlobFormat.GenericPrivateBlob)
    let keyPublic = rsa.Key.Export(CngKeyBlobFormat.GenericPublicBlob)
    
    let _, idBytes = keyPublic |> Array.splitAt (keyPublic.Length - 16)
    let didString = CreateRhoursDID(idBytes)

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


let private GetBondLastBalance (bondJson: JsonValue) =
    // last balance is either the value of the balance property of the last payment element
    // or the bond.amount value if there are no payments
    match bondJson with
    | JsonValue.JsonObject(bondObj) ->
        // get the payments array
        match bondObj |> Array.tryPick ( fun (n, v) -> if n = "payments" then Some(v) else None) with
        | Some(JsonValue.JsonArray(paymentsArray)) ->
            match Array.length paymentsArray with
            | 0 ->
                // last balance is the bond.amount
                let bondAmount = 
                    match bondObj |> Array.tryPick ( fun (n, v) -> if n = "amount" then Some(v) else None) with
                    | Some(JsonValue.JsonNumber(JsonNumber.JsonInteger(amount))) -> decimal(amount)
                    | Some(JsonValue.JsonNumber(JsonNumber.JsonFloat(amount))) -> decimal(amount)
                    | _ -> failwith "unable to get bond amount."

                // last balance date is bond.created
                let bondCreated = 
                    match bondObj |> Array.tryPick ( fun (n, v) -> if n = "created" then Some(v) else None) with
                    | Some(JsonValue.JsonString(s)) -> DateTime.ParseExact(s, "s", System.Globalization.CultureInfo.InvariantCulture)
                    | _ -> failwith "unable to get bond created."

                (bondAmount, bondCreated)
            | length ->
                // last balance is the payment.balance 
                // Assumeing here that validation of the bond will ensure that the payments are sorted by date
                match paymentsArray.[length - 1] with
                | JsonValue.JsonObject(paymentObj) ->
                    let paymentBalance = 
                        match paymentObj |> Array.tryPick ( fun (n, v) -> if n = "balance" then Some(v) else None) with
                        | Some(JsonValue.JsonNumber(JsonNumber.JsonInteger(balance))) -> decimal(balance)
                        | Some(JsonValue.JsonNumber(JsonNumber.JsonFloat(balance))) -> decimal(balance)
                        | _ -> failwith "unable to get payment balance."
                    let paymentDate = 
                        match paymentObj |> Array.tryPick ( fun (n, v) -> if n = "date" then Some(v) else None) with
                        | Some(JsonValue.JsonString(s)) -> DateTime.ParseExact(s, "s", System.Globalization.CultureInfo.InvariantCulture)
                        | _ -> failwith "unable to get payment date."

                    (paymentBalance, paymentDate)
                | _ -> failwith "payment must be an object."
        | _ -> failwith "couldn't get bond payments array."
        
    | _ -> failwith "bond must be an object."

let private CalculateInterest (pv: decimal) (rate: decimal) (days: int) : decimal =
    // rate is expressed as an annual rate
    // compunding is continuous
    
    // daily rate to yearly rate divided by 365
    let dailyRate = rate/(decimal(365))

    let fv = System.Math.Round(pv * (decimal((exp (float((dailyRate * (decimal(days)))))))), 2)
    fv - pv

let GetCompanyDID (root: string) =
    // requires root exists
    // requires root/Company exists
    // requires one company DID file exists

    let rootDir = System.IO.DirectoryInfo(root)
    if rootDir.Exists then
        match rootDir.GetDirectories("Company") with
        | [| d; |] ->
            match d.GetFiles("did_rhours_*.json") with
            | [| f; |] ->
                match TryParseRhoursDIDFileName(f.Name) with
                | Some(did) -> 
                    did
                | None ->
                    failwith "Unable to determine DID from file."
            | _ ->
                failwith "Unable to find company DID."
        | _ ->
            failwith "Company folder does not exist."
    else
        failwith "Root does not exist."

let InitializeCompanyFolder (root: string) =
    // Creates these sub-folders, if they don't exist
    // ./Company
    // ./Contributors
    // ./Bonds

    // Creates a company DID
    // Sets the Company binding in the context

    // Returns the Company DID

    let rootDir = System.IO.DirectoryInfo(root)
    if rootDir.Exists then
        let companyDir = CreateSubdirectory rootDir "Company"
        CreateSubdirectory rootDir "Contributors" |> ignore
        CreateSubdirectory rootDir "Bonds" |> ignore

        let companyDID = 
            match companyDir.GetFiles("did_rhours_*.json") with
            | [| |] ->
                // no company DID, create one
                CreateIdentityDID companyDir
            | [| f; |] ->
                // One company DID, use this
                match TryParseRhoursDIDFileName(f.Name) with
                | Some(did) -> 
                    did
                | None ->
                    failwith "Unable to determine DID from file."
            | _ ->
                // Don't know what to do with multiple DIDs in the company folder.
                failwith "Company folder has multiple DIDs, don't know what to do."

        // return the Company DID
        companyDID
    else
        failwith "Root folder does not exist."


let CreateContributor (root: string) =
    // requires root exists
    // requires root/Contributors exists
    // Creates a new contributor DID, and returns the DID string

    let rootDir = System.IO.DirectoryInfo(root)
    if rootDir.Exists then
        match rootDir.GetDirectories("Contributors") with
        | [| contributorsDir; |] ->
            let contributorDID = CreateIdentityDID contributorsDir

            // return the contributor DID
            contributorDID
        | _ ->
            failwith "Contributors directory does not exist."
    else
        failwith "Root folder does not exist."

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

    use rng = RandomNumberGenerator.Create()
    let idBytes = Array.create<byte> 16 0uy
    rng.GetBytes(idBytes)
    let bondDidString = CreateRhoursDID(idBytes)
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
                ("amount", JsonValue.JsonNumber(JsonNumber.JsonFloat(float(amount))));
                ("interest-rate", JsonValue.JsonNumber(JsonNumber.JsonFloat(float(rate))));
                ("max", JsonValue.JsonNumber(JsonNumber.JsonFloat(float(max))));
                ("unit", JsonValue.JsonString("USD"));
                ("payments", JsonValue.JsonArray([||]));
            |]
        )

    let bondsDir = System.IO.Path.Combine(path.FullName, "Bonds")

    use bondFile = System.IO.File.CreateText(System.IO.Path.Combine(bondsDir, bondDidString.Replace(":", "_") + ".json"))
    WriteJson jsonBond bondFile
    bondFile.Flush()
    bondFile.Close()

    // Return the bond DID string
    bondDidString

let SignBond (bondFile: string) (didFile: string) (pemBytes: byte array) =
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

    let key = CngKey.Import(pemBytes, CngKeyBlobFormat.GenericPrivateBlob)

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

let MakeBondPayment (bondFile: string) (amount: decimal) =
    // get the previous balance
    // calculate the interest
    // create the payment object
    // add it to the payments array
    // total payments is bounded by bond.max
    
    use bondTR = System.IO.File.OpenText(bondFile)
    let bondJson = ReadJson bondTR
    bondTR.Close()
    
    let bondJsonObj = 
        match bondJson with
        | JsonValue.JsonObject(o) -> o
        | _ -> failwith "bond must be a json object."
        
    let (lastBalance, lastBalanceDate) = GetBondLastBalance bondJson
    
    // get the of the bond interest rate
    let rate = 
        match bondJsonObj |> Array.tryPick ( fun (n, v) -> if n = "interest-rate" then Some(v) else None) with
        | Some(JsonValue.JsonNumber(JsonNumber.JsonInteger(i))) -> decimal(i)
        | Some(JsonValue.JsonNumber(JsonNumber.JsonFloat(f))) -> decimal(f)
        | _ -> failwith "couldn't get bond interest rate."
    
    // get the of the bond max payments
    let maxPayments = 
        match bondJsonObj |> Array.tryPick ( fun (n, v) -> if n = "max" then Some(v) else None) with
        | Some(JsonValue.JsonNumber(JsonNumber.JsonInteger(i))) -> decimal(i)
        | Some(JsonValue.JsonNumber(JsonNumber.JsonFloat(f))) -> decimal(f)
        | _ -> failwith "couldn't get bond max payments."
    
    let paymentsIndex = bondJsonObj |> Array.findIndex ( fun (n, v) -> n = "payments")
    
    // get total bond payments
    let totalPayments = 
        match bondJsonObj.[paymentsIndex] with
        | ("payments", JsonValue.JsonArray(paymentsArray)) ->
            paymentsArray |> Array.sumBy 
                (
                    fun p -> 
                        match p with 
                        | JsonValue.JsonObject(pObj) ->
                            match pObj |> Array.tryPick ( fun (n, v) -> if n = "amount" then Some(v) else None) with
                            | Some(JsonValue.JsonNumber(JsonNumber.JsonInteger(i))) -> decimal(i)
                            | Some(JsonValue.JsonNumber(JsonNumber.JsonFloat(f))) -> decimal(f)
                            | _ -> failwith "Can't access payment amount."
                        | _ -> failwith "payment element must be an object."
                )
        | _ -> failwith "unable to access bond.payments array."        
            
    let now = DateTime.UtcNow
    let days = int((now - lastBalanceDate).TotalDays)
    let interest = CalculateInterest lastBalance rate days
    let adjustedAmount = 
        // don't pay more than bond.max allows, cap amount to pay
        min amount (maxPayments - totalPayments)
    
    // new balance cannot be more than max - totalPayments
    let balance = 
        min
            (maxPayments - (totalPayments + adjustedAmount)) // bond.max - new total payments
            (max
                (lastBalance + interest - adjustedAmount)
                0M
            )        
    
    // amount in minus the amount of this payment
    let remainder = amount - adjustedAmount
    
    let paymentJson = 
        JsonValue.JsonObject (
            [|
                ("date", JsonValue.JsonString(DateTimeToString now));
                ("interest", JsonValue.JsonNumber(JsonNumber.JsonFloat(float(interest))));
                ("amount", JsonValue.JsonNumber(JsonNumber.JsonFloat(float(adjustedAmount))));
                ("balance", JsonValue.JsonNumber(JsonNumber.JsonFloat(float(balance))));
            |]
        )
    (*
        {
            "date": "UTC payment date, format is yyyy-MM-ddThh:mm:ss",
            "interest": 16.52,
            "amount": 40.00,
            "balance": 76.52
        }
    *)
    
    // append this payments object to the payments array
    match bondJsonObj.[paymentsIndex] with
    | ("payments", JsonValue.JsonArray(paymentsArray)) ->
        bondJsonObj.[paymentsIndex] <- ("payments", JsonValue.JsonArray(Array.append paymentsArray [| paymentJson; |]))
    | _ -> failwith "Unable to update bond payments array."
    
    // Write updated json bond
    use bondTW = System.IO.File.CreateText(bondFile)
    WriteJson bondJson bondTW
    bondTW.Flush()
    bondTW.Close()
    
    // return the remainder
    remainder
    
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
                    System.IO.Path.Combine(path.FullName, "Company", (DIDFileName companyDID) + ".json")
                elif didUrl.StartsWith(contributorDID) then
                    System.IO.Path.Combine(path.FullName, "Contributors", (DIDFileName contributorDID) + ".json")
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
