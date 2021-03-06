﻿
(*  TO DO

Hi @glenbraun,

You recently used a password to access an endpoint through the GitHub API using git-credential-manager 
(Microsoft Windows NT 6.2.9200.0; Win32NT; x64) CLR/4.0.30319 git-tools/1.15.2. We will deprecate 
basic authentication using password to this endpoint soon:

https://api.github.com/user/subscriptions

We recommend using a personal access token (PAT) with the appropriate scope to access this endpoint 
instead. Visit https://github.com/settings/tokens for more information.

Thanks,
The GitHub Team

*)


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

let BondPaymentEval = 
    fun (context: UIContext) ->
        (*
        bond        payment         --id            The DID string for the bond.
                                    --amount        The amount of payment to apply to the bond. This number may be
                                                    adjusted downward to avoid overpayment.
        *)
        let root = context.GetBinding<string> "Root" "Argument 'root' is required."
        let bondDID = context.GetBinding<string> "Id" "Argument 'id' is required."
        let amount = context.GetBinding<decimal> "Amount" "Argument 'amount' is required."
        
        let bondFile = System.IO.Path.Combine(root, "Bonds", bondDID.Replace(":", "_") + ".json")

        // MakeBondPayment (bondFile: string) (amount: decimal)
        let remainder = MakeBondPayment bondFile amount
        printfn "Amount Paid: %f" (amount - remainder)

        CommandEvaluationResult.Success(1)

let BondVerifyEval = 
    fun (context: UIContext) ->
        (*
        bond       verify           --id            The DID string for the bond.
        *)

        let root = context.GetBinding<string> "Root" "Argument 'root' is required."
        let bondDID = context.GetBinding<string> "Id" "Argument 'id' is required."
        
        let rootDir = System.IO.DirectoryInfo(root)
        let bondFile = System.IO.Path.Combine(root, "Bonds", bondDID.Replace(":", "_") + ".json")

        // VerifyBond (path: System.IO.DirectoryInfo) (bondFile: string) : (DateTime * DateTime) option =
        match VerifyBond rootDir bondFile with
        | Some(companyProofDate, contributorProofDate) ->
            printfn "Company proof date: %s" (companyProofDate.ToString())
            printfn "Contributor proof date: %s" (contributorProofDate.ToString())
            CommandEvaluationResult.Success(1)
        | None ->
            printfn "Bond does not validate."
            CommandEvaluationResult.Failure(-1, false)
        

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
    //Internal.Utilities.Text.Parsing.Flags.debug <- false

    let dataDir = System.IO.DirectoryInfo("..\\..\\..\\Data")

    let root = sprintf "--root=%s" (dataDir.FullName)
    let terms = sprintf "--terms=%s" "C:\\Projects\\RHours\\ContributionBonds\\README.md"
    let contributor = "--contributor=did:rhours:21meXxGB8Gk8KcGrDtrnoUUL5UrL"
    let amount = sprintf "--amount=%f" 100M
    let rate = sprintf "--rate=%f" 0.25M
    let maxamt = sprintf "--max=%f" 1000M
    let bondid = "--id=did:rhours:iGyFKp5zwo84NX4jXzJc1fyMnDh"
    let signatoryContributor = sprintf "--signatory=did:rhours:21meXxGB8Gk8KcGrDtrnoUUL5UrL"
    let signatoryCompany = sprintf "--signatory=did:rhours:BV8hFzyprAj1M5ex327pWMJE9N1"
    let paymentAmount = sprintf "--amount=%f" 20M
    
    let testCompanyInitializeArgs = [| "company"; "initialize"; root; |]
    let testContributorCreateArgs = [| "contributor"; "create"; root; |]
    let testBondCreateArgs = [| "bond"; "create"; root; terms; contributor; amount; rate; maxamt; |]
    let testBondSignContributorArgs = [| "bond"; "sign"; root; bondid; signatoryContributor; |]
    let testBondSignCompanyArgs = [| "bond"; "sign"; root; bondid; signatoryCompany; |]
    let testBondPaymentArgs = [| "bond"; "payment"; root; bondid; paymentAmount; |]
    let testBondVerifyArgs = [| "bond"; "verify"; root; bondid; |]

    let mode = 0

    let testArgs = 
        match mode with
        | 0 -> argv
        | 1 -> testCompanyInitializeArgs
        | 2 -> testContributorCreateArgs
        | 3 -> testBondCreateArgs
        | 4 -> testBondSignContributorArgs
        | 5 -> testBondSignCompanyArgs
        | 6 -> testBondPaymentArgs
        | 7 -> testBondVerifyArgs
        | _ -> failwith "bad"


    let ui = UIContext (UICommands, (fun () -> printfn "help"))
    let result = ui.ProcessCommands(testArgs)
    printfn "Result %d" result


    0 // return an integer exit code

