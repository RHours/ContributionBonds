module ConsoleUI

type CommandEvaluationResult =
    | NotApplicable
    | Success of int
    | Failure of int * bool     // return value, true/false to display help text

type ParamType =
    | ParamString
    | ParamDecimal
    | ParamBytes

type ArgumentBinding = 
    | StringValue of string
    | DecimalValue of decimal
    | BytesValue of byte array

type ParamDefinition = 
    {
        Name: string;
        ParamType: ParamType;
        Default: (unit -> ArgumentBinding) option;
    }

type CommandDefinition (command: string, 
                        subCommand: string option, 
                        paramDefs: ParamDefinition list, 
                        pemLabel: string option,
                        eval: (UIContext -> CommandEvaluationResult)) =

    let paramRegex = System.Text.RegularExpressions.Regex("-{1,2}([^=]*)=(.*)")
    let ParseParam (arg: string) : (string * string) option =
        let result = paramRegex.Match(arg)
        if result.Success then
            Some(result.Groups.[1].Value, result.Groups.[2].Value)
        else
            None

    let rec BindParms (context: UIContext) (args: string array) (i: int) =
        if i < args.Length then
            // parse the arg as a parameter (name=value)
            // look for a matching paramsDef
            match ParseParam args.[i] with
            | Some(n, v) -> 
                match paramDefs |> List.tryPick (fun p -> if p.Name.ToLower() = n.ToLower() then Some(p) else None) with
                | Some(p) ->
                    let thisBinding = 
                        match p.ParamType with
                        | ParamType.ParamString ->
                            ArgumentBinding.StringValue(v)
                        | ParamType.ParamDecimal ->
                            ArgumentBinding.DecimalValue(System.Convert.ToDecimal(v))
                        | ParamType.ParamBytes ->
                            ArgumentBinding.BytesValue(System.Convert.FromBase64String(v))

                    context.AddBinding (p.Name) thisBinding

                    BindParms context args (i+1)
                | None ->
                    // couldn't find a param with this name, throw exception
                    failwith (sprintf "Parameter not defined: %s" n)
            | None ->
                failwith (sprintf "Invalid argument: %s" args.[i])
        
    let rec BindDefaults (context: UIContext) (pdl: ParamDefinition list) =
        match pdl with
        | [ ] -> 
            ()
        | h :: t ->
            // if there is NOT a binding for this param, run the default function
            // it's an error if there's no value for a param
            match context.TryFindBinding (h.Name) with
            | None ->
                match h.Default with
                | Some(df) ->
                    let defaultBinding = df()
                    context.AddBinding (h.Name) defaultBinding
                    BindDefaults context t
                | None ->
                    failwith (sprintf "Value required for parameter: %s" (h.Name))
            | Some(_) ->
                BindDefaults context t

    member this.Evaluate(context: UIContext, args: string array) : CommandEvaluationResult =
        // return NotApplicable if command not equal arg 1
        // return NotApplicable if subCommand not equal to arg 2 - when subCommand is Some
        // bind the pairs to remaining args
        // perform the readline for any PEMPrompt
        // call the eval function

        let paramStartIndex = 
            if args.Length > 0 then
                if command.StartsWith(args.[0]) then
                    context.AddBinding "Command" (ArgumentBinding.StringValue(command))

                    match subCommand with
                    | Some(c) -> 
                        if args.Length > 1 && c.StartsWith(args.[1]) then
                            context.AddBinding "SubCommand" (ArgumentBinding.StringValue(c))
                            2
                        else
                            -1
                    | None ->
                        1
                else
                    -1
            else
                -1

        if paramStartIndex = -1 then
            CommandEvaluationResult.NotApplicable
        else
            BindParms context args paramStartIndex
            BindDefaults context paramDefs

            match pemLabel with
            | Some(label) ->
                printfn "Entery PEM for %s" label
                let pemText = System.Console.ReadLine()
                let (_ , pemString) = Json.Api.ParsePEMString pemText
                context.AddBinding "PEM" (ArgumentBinding.BytesValue(System.Convert.FromBase64String(pemString)))
            | None -> ()

            // call the eval function
            eval context

and UIContext (cmdDefs: CommandDefinition list, help: unit -> unit) =
    let mutable bindings = Map.empty<string, ArgumentBinding>

    member this.AddBinding (name: string) (value: ArgumentBinding) =
        bindings <- bindings |> Map.add name value

    member this.TryFindBinding (name: string) = 
        bindings |> Map.tryFind name

    member this.ProcessCommands(args: string array) =
        // Go through the commands
        // Evaluate each one until a success or failure is found
        // If none return success or failure, display the help string

        let fEval (cmd: CommandDefinition) =
            match cmd.Evaluate(this, args) with
            | CommandEvaluationResult.NotApplicable -> None
            | r -> Some(r)

        match cmdDefs |> List.tryPick fEval with
        | Some(CommandEvaluationResult.Success(r)) -> r
        | Some(CommandEvaluationResult.Failure(r, h)) ->
            if h then
                help()
            r
        | _ ->
            // No commands resulted in success or failure
            // show the help and return -1
            help()
            -1
