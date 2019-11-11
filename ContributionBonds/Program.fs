
open Json.Api
open ConsoleUI
open BondApi

// need hash of public key of identities in bond
let NotImplementedEval = 
    fun (context: UIContext) -> 
        printfn "Command Not implemented"
        CommandEvaluationResult.Success(1)

let CompanyInitializeEval = 
    fun (context: UIContext) ->
        match context.TryFindBinding("Root") with
        | Some(ArgumentBinding.StringValue(root)) -> 
            let companyDID = InitializeCompanyFolder root
            context.AddBinding "CompanyDID" (ArgumentBinding.StringValue(companyDID))
            CommandEvaluationResult.Success(1)
        | _ ->
            // Error, Must have a root param value.
            CommandEvaluationResult.Failure(-1, false)

let ContributorCreateEval = NotImplementedEval
let BondCreateEval = NotImplementedEval
let BondSignEval = NotImplementedEval
let BondPaymentEval = NotImplementedEval
let BondVerifyEval = NotImplementedEval

let RootParamDefault = 
    fun () -> 
        let curdir = System.IO.DirectoryInfo(System.IO.Directory.GetCurrentDirectory())
        match curdir.GetDirectories(".git") with
        | [| _; |] -> 
            ArgumentBinding.StringValue(curdir.FullName)
        | _ -> 
            ArgumentBinding.StringValue(null)

let RootParam =
    {
        Name = "Root";
        ParamType = ParamType.ParamString;
        Default = Some(RootParamDefault);        
    }

let CompanyInitializeCommand = 
    CommandDefinition(
        "company",
        Some("initialize"),
        [ RootParam; ],
        None,
        CompanyInitializeEval
    )

let ContributorCreateCommand = 
    CommandDefinition(
        "contributor",
        Some("create"),
        [ RootParam; ],
        None,
        ContributorCreateEval
    )

(*
bond        create          
    --terms         A path to a file containing the written terms of the bond agreement.
    --contributor   DID string of the contributor the bond is being issued to.
    --amount        The dollar amount of the bond.
    --rate          The interest rate, expressed as a decimal, e.g., 0.25 is 25%.
    --max           The maximum total payments for the bond.
*)

let BondTermsParam = { Name = "BondTerms"; ParamType = ParamType.ParamString; Default = None; }
let BondContrbutorParam = { Name = "BondContributor"; ParamType = ParamType.ParamString; Default = None; }
let BondAmountParam = { Name = "BondAmount"; ParamType = ParamType.ParamDecimal; Default = None; }
let BondRateParam = { Name = "BondRate"; ParamType = ParamType.ParamDecimal; Default = None; }
let BondMaxParam = { Name = "BondMax"; ParamType = ParamType.ParamDecimal; Default = None; }


let BondCreateCommand = 
    CommandDefinition(
        "bond",
        Some("create"),
        [ RootParam; BondTermsParam; BondContrbutorParam; BondAmountParam; BondRateParam; BondMaxParam; ],
        None,
        BondCreateEval
    )

(*
bond        sign            --bond          The DID string for the bond.
                            --signatory     The DID string of the identity signing the bond. This
                                            needs to be either the contributor or the company.
*)
let BondIdParam = { Name = "bond"; ParamType = ParamType.ParamString; Default = None; }
let BondSignatoryParam = { Name = "signatory"; ParamType = ParamType.ParamString; Default = None; }

let BondSignCommand = 
    CommandDefinition(
        "bond",
        Some("sign"),
        [ RootParam; BondIdParam; BondSignatoryParam; ],
        None,
        BondSignEval
    )

(*
bond        payment         --bond          The DID string for the bond.
                            --amount        The amount of payment to apply to the bond. This number may be
                                            adjusted downward to avoid overpayment.
*)
let BondPaymentCommand = 
    CommandDefinition(
        "bond",
        Some("payment"),
        [ RootParam; BondIdParam; BondAmountParam; ],
        None,
        BondPaymentEval
    )

(*
bond       verify           --bond          The DID string for the bond.
*)
let BondVerifyCommand = 
    CommandDefinition(
        "bond",
        Some("verify"),
        [ RootParam; BondIdParam; ],
        None,
        BondVerifyEval
    )

let UICommands = 
    [
        CompanyInitializeCommand;
        ContributorCreateCommand;
        BondCreateCommand;
        BondSignCommand;
        BondPaymentCommand;
        BondVerifyCommand
    ]

[<EntryPoint>]
let main argv = 
    Internal.Utilities.Text.Parsing.Flags.debug <- false

    let testCompanyInitializeArgs = [| "company"; "initialize"; "--root=C:\\Projects\\RHours\\ContributionBonds\\Data"; |]


    let ui = UIContext (UICommands, (fun () -> printfn "help"))
    let result = ui.ProcessCommands(testCompanyInitializeArgs)
    printfn "Result %d" result


    let dataDir = System.IO.DirectoryInfo("..\\..\\..\\Data")


    (*
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
        let bondFile = System.IO.Path.Combine(dataDir.FullName, "did_rhours_2wG71RimEzFQHoKNRTRsRSakcX3Q.json")
    
        // Verify the bond
        let result = VerifyBond dataDir bondFile
        printf "%A" result
    | 3 ->
        let bondFile = System.IO.Path.Combine(dataDir.FullName, "did_rhours_2wG71RimEzFQHoKNRTRsRSakcX3Q.json")
        let remainder = MakeBondPayment bondFile (decimal(20))
        printf "Remainder = %f" remainder

    | _ -> failwith "bad mode"
    *)


    0 // return an integer exit code

