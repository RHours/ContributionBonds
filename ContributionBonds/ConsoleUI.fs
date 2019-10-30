module ConsoleUI

type CommandEvaluationResult =
    | NotApplicable
    | Success of int
    | Failure of int * bool     // return value, true/false to display help text

type ArgumentBinding = 
    | StringValue of string
    | DecimalValue of decimal

type ParamDefinition = 
    {
        Name: string;
        TypeName: string;
        Default: (unit -> ArgumentBinding) option;
    }

type CommandDefinition (command: string, 
                        subCommand: string option, 
                        paramDefs: ParamDefinition list, 
                        pemLabel: string option,
                        eval: (Map<string, ArgumentBinding> -> CommandEvaluationResult)) =

    let ParseParam (arg: string) : (string * string) option =
        None

    let rec BindParms (args: string array) (bindings: Map<string, ArgumentBinding>) (i: int) : unit =
        if i < args.Length then
            // parse the arg as a parameter (name=value)
            // look for a matching paramsDef
            ()
        else
            ()

    member this.Evaluate(args: string array) : CommandEvaluationResult =
        // return NotApplicable if command not equal arg 1
        // return NotApplicable if subCommand not equal to arg 2 - when subCommand is Some
        // bind the pairs to remaining args
        // perform the readline for any PEMPrompt
        // call the 

        let mutable bindings = Map.empty<string, ArgumentBinding>

        if args.[1] = command then
            bindings <- bindings |> Map.add "command" (ArgumentBinding.StringValue(command))

            match subCommand with
            | Some(c) -> 
                if args.[2] = c then
                    bindings <- bindings |> Map.add "subCommand" (ArgumentBinding.StringValue(c))
                    BindParms args bindings 3

                    CommandEvaluationResult.NotApplicable
                else
                    BindParms args bindings 2
                    CommandEvaluationResult.NotApplicable
            | None ->
                CommandEvaluationResult.NotApplicable
        else
            CommandEvaluationResult.NotApplicable

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
        


    


