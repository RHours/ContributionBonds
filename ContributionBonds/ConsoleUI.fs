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
                        eval: (Map<string, ArgumentBinding> -> CommandEvaluationResult)) =

    let paramRegex = System.Text.RegularExpressions.Regex("-{1,2}([^=]*)=(.*)")
    let ParseParam (arg: string) : (string * string) option =
        let result = paramRegex.Match(arg)
        if result.Success then
            Some(result.Groups.[1].Value, result.Groups.[2].Value)
        else
            None

    let rec BindParms (args: string array) (bindings: Map<string, ArgumentBinding>) (i: int) : Map<string, ArgumentBinding> =
        if i < args.Length then
            // parse the arg as a parameter (name=value)
            // look for a matching paramsDef
            match ParseParam args.[i] with
            | Some(n, v) -> 
                match paramDefs |> List.tryPick (fun p -> if p.Name = n then Some(p) else None) with
                | Some(p) ->
                    let thisBinding = 
                        match p.ParamType with
                        | ParamType.ParamString ->
                            ArgumentBinding.StringValue(v)
                        | ParamType.ParamDecimal ->
                            ArgumentBinding.DecimalValue(System.Convert.ToDecimal(v))
                        | ParamType.ParamBytes ->
                            ArgumentBinding.BytesValue(System.Convert.FromBase64String(v))

                    let newBindings = 
                        bindings |> Map.add (p.Name) (thisBinding)

                    BindParms args newBindings (i+1)
                | None ->
                    // couldn't find a param with this name, throw exception
                    failwith (sprintf "Parameter not defined: %s" n)
            | None ->
                failwith (sprintf "Invalid argument: %s" args.[i])
        else
            bindings

    let rec BindDefaults (bindings: Map<string, ArgumentBinding>) (pdl: ParamDefinition list) : Map<string, ArgumentBinding> =
        match pdl with
        | [ ] -> 
            bindings
        | h :: t ->
            // if there is NOT a binding for this param, run the default function
            // it's an error if there's no value for a param
            match (bindings |> Map.containsKey (h.Name)) with
            | false ->
                match h.Default with
                | Some(df) ->
                    let defaultBinding = df()
                    let newBindings = bindings |> Map.add (h.Name) defaultBinding
                    BindDefaults newBindings t
                | None ->
                    failwith (sprintf "Value required for parameter: %s" (h.Name))
            | true ->
                BindDefaults bindings t

    member this.Evaluate(args: string array) : CommandEvaluationResult =
        // return NotApplicable if command not equal arg 1
        // return NotApplicable if subCommand not equal to arg 2 - when subCommand is Some
        // bind the pairs to remaining args
        // perform the readline for any PEMPrompt
        // call the eval function

        let mutable bindings = Map.empty<string, ArgumentBinding>

        let paramStartIndex = 
            if args.Length > 0 then
                if command.StartsWith(args.[0]) then
                    bindings <- bindings |> Map.add "command" (ArgumentBinding.StringValue(command))

                    match subCommand with
                    | Some(c) -> 
                        if args.Length > 1 && c.StartsWith(args.[1]) then
                            bindings <- bindings |> Map.add "subCommand" (ArgumentBinding.StringValue(c))
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
            bindings <- BindParms args bindings paramStartIndex
            bindings <- BindDefaults bindings paramDefs

            match pemLabel with
            | Some(label) ->
                printfn "Entery PEM for %s" label
                let pemText = System.Console.ReadLine()
                let (_ , pemString) = Json.Api.ParsePEMString pemText
                bindings <- bindings |> Map.add "pem" (ArgumentBinding.BytesValue(System.Convert.FromBase64String(pemString)))
            | None -> ()

            // call the eval function
            eval bindings

type UIDefinition (cmdDefs: CommandDefinition list, help: unit -> unit) =
    member this.ProcessCommands(args: string array) =
        // Go through the commands
        // Evaluate each one until a success or failure is found
        // If none return success or failure, display the help string

        let fEval (cmd: CommandDefinition) =
            match cmd.Evaluate(args) with
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
        


    


