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


    let props = {1, 2, 3}
    const { *, y, z } = props


    //x = 1
    //y = 2
    //z = 3

*)

open System
open System.Security.Cryptography
open System.Text
open Json


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

type DecentralizedIdentifier =
    {
        MethodName: string;
        MethodSpecificIds: string array
    }

[<EntryPoint>]
let main argv = 
    Internal.Utilities.Text.Parsing.Flags.debug <- false

     // let lexbuf = Internal.Utilities.Text.Lexing.LexBuffer<_>.FromString("{ \"x\":5, \"y\":[1,2,false, null, {}, [0]] }")
    // "did:example:123456789abcdefghi"
    let lexbuf = Internal.Utilities.Text.Lexing.LexBuffer<_>.FromString("did:example:::")

    //let parseresult = Parser.json Lexer.json lexbuf
    let parseresult = Did.Parser.did Did.Lexer.did lexbuf

    
    //let parseresult = System.Convert.ToString("\u0042") :> obj
    printfn "%A" parseresult


    match argv with
    | CommandCreateIdentifier -> 
        printfn "id"
    | CommandSignBond (None, companyIsSigning, contributorIsSigning, bondFile) ->
        
        ()
    | CommandSignBond (Some(err), _, _, _) ->
        printfn "%s" err
    | _ ->
        printfn "bad command"



    use dsaCompany = new System.Security.Cryptography.ECDsaCng()
    use dsaContributor = new System.Security.Cryptography.ECDsaCng()

    dsaCompany.HashAlgorithm <- CngAlgorithm.Sha256
    dsaContributor.HashAlgorithm <- CngAlgorithm.Sha256

    let pubkeyCompany = dsaCompany.Key.Export(CngKeyBlobFormat.EccPublicBlob)
    let privkeyCompany = dsaCompany.Key.Export(CngKeyBlobFormat.EccPrivateBlob)

    let pubkeyContributor = dsaContributor.Key.Export(CngKeyBlobFormat.EccPublicBlob)
    let privkeyContributor = dsaContributor.Key.Export(CngKeyBlobFormat.EccPrivateBlob)

    let sCompanyKey = System.Convert.ToBase64String(pubkeyCompany)
    let sContributorKey = System.Convert.ToBase64String(pubkeyContributor)
    
    let jsonContributionBond = sprintf """
{
    "company": "COMPANY_KEY",
    "contributor": "CONTRIBUTOR_KEY",
    "amount": 100.00,
    "interest": 0.25,
    "max": 1000.00,
    "unit": "USD",
    "payments": [ ]
}
    """

    printfn "%s" jsonContributionBond

    let jsonContributionBond1 = jsonContributionBond.Replace("COMPANY_KEY", sCompanyKey)
    let jsonContributionBond1 = jsonContributionBond1.Replace("CONTRIBUTOR_KEY", sContributorKey)

    printfn "%s" jsonContributionBond1

    // FYI - We know this is not a reliable way to convert string to bytes
    let utf8 = Encoding.UTF8
    let dataContributionBond = utf8.GetBytes(jsonContributionBond1)

    let signatureCompany = dsaCompany.SignData(dataContributionBond)
    let signatureContributor = dsaContributor.SignData(dataContributionBond)

    let jsonSignature = """
{
    "issuance": {
        "company": "COMPANY_SIGNATURE",
        "contributor": "CONTRIBUTOR_SIGNATURE"
    },
    "payments": [ ]
}    
    """

    let jsonSignature1 = jsonSignature.Replace("COMPANY_SIGNATURE", System.Convert.ToBase64String(signatureCompany))
    let jsonSignature1 = jsonSignature1.Replace("CONTRIBUTOR_SIGNATURE", System.Convert.ToBase64String(signatureContributor))

    printfn "%s" jsonSignature1

    let jsonPayments = """
        [
            {
                "date": "2019-11-04",
                "interest": 16.52,
                "amount": 40.00,
                "balance": 76.52
            }
        ]
"""
    
    let jsonContributionBond2 = jsonContributionBond1.Replace("[ ]", jsonPayments)
    let dataContributionBond2 = utf8.GetBytes(jsonContributionBond2)

    printfn "%s" jsonContributionBond2

    let signatureCompany2 = dsaCompany.SignData(dataContributionBond2)
    let signatureContributor2 = dsaContributor.SignData(dataContributionBond2)


    let jsonPaymentsSignature = """
        [
            { 
                "company": "COMPANY_SIGNATURE"
                "contributor": "CONTRIBUTOR_SIGNATURE",
            }
        ]
    """

    let jsonPaymentsSignature = jsonPaymentsSignature.Replace("COMPANY_SIGNATURE", System.Convert.ToBase64String(signatureCompany2))
    let jsonPaymentsSignature = jsonPaymentsSignature.Replace("CONTRIBUTOR_SIGNATURE", System.Convert.ToBase64String(signatureContributor2))

    let jsonSignature2 = jsonSignature1.Replace("[ ]", jsonPaymentsSignature)

    printfn "%s" jsonSignature2
(*
    use ecsdKey = new ECDsaCng(CngKey.Import(pubkeyCompany, CngKeyBlobFormat.EccPublicBlob))
    if (ecsdKey.VerifyData(data, signature)) then
        printfn "Data is good"
    else
        printfn "Data is bad"
 *)  
    
    0 // return an integer exit code

