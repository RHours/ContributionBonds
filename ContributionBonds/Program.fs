
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

let ContributorCreateEval = 
    fun (context: UIContext) ->
        match context.TryFindBinding("Root") with
        | Some(ArgumentBinding.StringValue(root)) -> 
            let contributorDID = CreateContributor root
            context.AddBinding "ContributorDID" (ArgumentBinding.StringValue(contributorDID))
            CommandEvaluationResult.Success(1)
        | _ ->
            // Error, Must have a root param value.
            CommandEvaluationResult.Failure(-1, false)

let BondCreateEval = 
    fun (context: UIContext) ->

        (*
        bond        create          --terms         A path to a file containing the written terms of the bond agreement.
                                    --contributor   DID string of the contributor the bond is being issued to.
                                    --amount        The dollar amount of the bond.
                                    --rate          The interest rate, expressed as a decimal, e.g., 0.25 is 25%.
                                    --max           The maximum total payments for the bond.
        *)

        let root = context.GetBinding<string> "Root" "Argument 'root' is required."
        let terms = context.GetBinding<string> "Terms" "Argument 'terms' is required."
        let contributorDID = context.GetBinding<string> "Contributor" "Argument 'contributor' is required."
        let amount = context.GetBinding<decimal> "Amount" "Argument 'amount' is required."
        let rate = context.GetBinding<decimal> "Rate" "Argument 'rate' is required."
        let max = context.GetBinding<decimal> "Max" "Argument 'max' is required."

        let companyDID = GetCompanyDID root

        let bondDID = CreateBond (System.IO.DirectoryInfo(root)) terms companyDID contributorDID amount rate max
        
        context.AddBinding "BondDID" (ArgumentBinding.StringValue(bondDID))
        CommandEvaluationResult.Success(1)
        
let BondSignEval = 
    fun (context: UIContext) ->
        (*
        bond        sign            --bond          The DID string for the bond.
                                    --signatory     The DID string of the identity signing the bond. This
                                                    needs to be either the contributor or the company.
        *)

        let root = context.GetBinding<string> "Root" "Argument 'root' is required."
        let bondDID = context.GetBinding<string> "Id" "Argument 'id' is required."
        let signatoryDID = context.GetBinding<string> "Signatory" "Argument 'signatory' is required."
        let pemBytes = context.GetBinding<byte array> "PEM" "PEM is required."

        // SignBond (bondFile: string) (didFile: string) (pemString: string)
        let bondFile = System.IO.Path.Combine(root, "Bonds", bondDID.Replace(":", "_") + ".json")

        let signatoryFile =
            let companyDID = GetCompanyDID root
            if signatoryDID = companyDID then
                // company is signing
                System.IO.Path.Combine(root, "Company", signatoryDID.Replace(":", "_") + ".json")
            else
                System.IO.Path.Combine(root, "Contributors", signatoryDID.Replace(":", "_") + ".json")

        SignBond bondFile signatoryFile pemBytes
        CommandEvaluationResult.Success(1)

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

let BondTermsParam = { Name = "Terms"; ParamType = ParamType.ParamString; Default = None; }
let BondContrbutorParam = { Name = "Contributor"; ParamType = ParamType.ParamString; Default = None; }
let BondAmountParam = { Name = "Amount"; ParamType = ParamType.ParamDecimal; Default = None; }
let BondRateParam = { Name = "Rate"; ParamType = ParamType.ParamDecimal; Default = None; }
let BondMaxParam = { Name = "Max"; ParamType = ParamType.ParamDecimal; Default = None; }


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
let BondIdParam = { Name = "Id"; ParamType = ParamType.ParamString; Default = None; }
let BondSignatoryParam = { Name = "Signatory"; ParamType = ParamType.ParamString; Default = None; }

let BondSignCommand = 
    CommandDefinition(
        "bond",
        Some("sign"),
        [ RootParam; BondIdParam; BondSignatoryParam; ],
        Some("PRIVATE KEY"),
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


    let dataDir = System.IO.DirectoryInfo("..\\..\\..\\Data")

    let root = sprintf "--root=%s" (dataDir.FullName)
    let terms = sprintf "--terms=%s" "C:\\Projects\\RHours\\ContributionBonds\\README.md"
    let contributor = "--contributor=did:rhours:2KN8YfD5Zh9rmebfuHbH9HUVPSy4"
    let amount = sprintf "--amount=%f" 100M
    let rate = sprintf "--rate=%f" 0.25M
    let maxamt = sprintf "--max=%f" 1000M
    let bondid = "--id=did:rhours:TN6rhGcXTaFqcaGAXsQ5BqaaFTn"
    let signatoryContributor = sprintf "--signatory=did:rhours:2KN8YfD5Zh9rmebfuHbH9HUVPSy4"
    let signatoryCompany = sprintf "--signatory=did:rhours:4Kdk9wzaCMo1hqG7Xb1HqvwzKxFy"
    
    let testCompanyInitializeArgs = [| "company"; "initialize"; root; |]
    let testContributorCreateArgs = [| "contributor"; "create"; root; |]
    let testBondCreateArgs = [| "bond"; "create"; root; terms; contributor; amount; rate; maxamt; |]
    let testBondSignContributorArgs = [| "bond"; "sign"; root; bondid; signatoryContributor; |]
    let testBondSignCompanyArgs = [| "bond"; "sign"; root; bondid; signatoryCompany; |]

    let mode = 5

    let testArgs = 
        match mode with
        | 0 -> argv
        | 1 -> testCompanyInitializeArgs
        | 2 -> testContributorCreateArgs
        | 3 -> testBondCreateArgs
        | 4 -> testBondSignContributorArgs
        | 5 -> testBondSignCompanyArgs
        | _ -> failwith "bad"


    let ui = UIContext (UICommands, (fun () -> printfn "help"))
    let result = ui.ProcessCommands(testArgs)
    printfn "Result %d" result

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

