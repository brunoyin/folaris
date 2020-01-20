open System
open System.IO
open System.Text.RegularExpressions
//
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.ServerErrors
open Suave.Writers
//
//open Hopac
//open Logary
//open Logary.Message
//open Logary.Targets
//open Logary.Adapters.Facade
//open Logary.Configuration
//open Logary.Prometheus.Exporter
//
open Newtonsoft.Json
open Newtonsoft.Json.Converters
//
open System.Management.Automation
open System.Management.Automation.Runspaces

let folarisVersion = "0.0.1"

//types
type PwshCommand =
    {
        cmd: string
    }
type PwshResult = 
    {
        cmd: string
        output: PSDataCollection<PSObject>
        outstring: string
        status: bool
        timeUsed: float
        timeBegin: DateTime
    }

type PwshExecution =
    | SuccessResult of PwshResult
    | FailureResult of Exception

let GetEnvVar varName defaultValue = 
    match Environment.GetEnvironmentVariable(varName) with
    | null -> defaultValue
    | value -> value

// setting up upload directory
let uploadDir = GetEnvVar "FOLARIS_UPLOAD" (Path.Combine(Environment.CurrentDirectory, "folaris_upload" ))
if  not ( Directory.Exists(uploadDir)) then Directory.CreateDirectory(uploadDir) |> ignore
// remove invalid path characters: accept only: 0-9, A-Z, a-z, -_.
let goodones = List.concat [ [ 47 .. 57 ]; [ 65 .. 90 ]; [ 97 .. 122 ]; [ 126; 45; 46; ] ] |> List.map (fun x -> (x |> char))
let fixLetter inputChar =
    if List.exists (fun x -> x = inputChar) goodones then
        inputChar
    else
        '_' // replace any other characters with underscore '_'
//
let safeFilename filename =
    new String( (filename |> Seq.map (fun x -> (fixLetter x)) |>Array.ofSeq ) )
//
let uniqueFile filePath =
    if File.Exists(filePath) then
        let dt = DateTime.Now
        let filename = Path.GetFileNameWithoutExtension(filePath)
        let fileExt = Path.GetExtension(filePath)
        let dirPath = Path.GetDirectoryName(filePath)
        match fileExt with
        | null -> Path.Combine(dirPath, String.Format("{0}-{1:yyyyMMdd-HHmmss-ffff}", filename, dt))
        | _ -> Path.Combine(dirPath, String.Format("{0}-{2:yyyyMMdd-HHmmss-ffff}{1}", filename, fileExt, dt))
    else
        filePath

// create Powershell runspace pool and create an initial state 
// by creating variables, environment variables, functions available to Powershell sessions that we are going to create
let sessionState = InitialSessionState.CreateDefault()
SessionStateVariableEntry("folaris_version",folarisVersion, "Folaris version in every powershell session in $folaris_version variable") |> sessionState.Variables.Add
SessionStateVariableEntry("ErrorActionPreference","Stop", "Making sure execution stop on error") |> sessionState.Variables.Add
// create a RunspacePool to speed up execution
let runSpacePool = RunspaceFactory.CreateRunspacePool(sessionState)
runSpacePool.SetMinRunspaces(1) |>ignore
runSpacePool.SetMaxRunspaces(25) |>ignore

// set up serialization    
let jsonSettings = new JsonSerializerSettings()
jsonSettings.MaxDepth <- new System.Nullable<int>(1024)
jsonSettings.TypeNameHandling <- TypeNameHandling.All
//jsonSettings.StringEscapeHandling <- StringEscapeHandling.Default
new StringEnumConverter() |> jsonSettings.Converters.Add

//low level functions
let getString (rawForm: byte[]) =
    System.Text.Encoding.UTF8.GetString(rawForm)

let fromJson<'a> json =
    JsonConvert.DeserializeObject(json, typeof<'a>) :?> 'a

let toJson psobject =
    JsonConvert.SerializeObject(psobject, jsonSettings)

// To-do: needs improvement
let fromPSobject (psObject: PSObject): string =
    match psObject with
    | null -> "PSObject is empty"
    | _ -> psObject.ToString()

// execute a powershell script using "Invoke-Expression"
let executeCmd (cmd: PwshCommand) =
    async{
        let t1 = DateTime.Now
        use pwsh = PowerShell.Create()
        pwsh.RunspacePool <- runSpacePool
        // Run powershll command, expecting a command or script: Invoke-Expression [-Command] <String>
        let! ret = pwsh.AddCommand("Invoke-Expression").AddParameter("Command", cmd.cmd).InvokeAsync()|>Async.AwaitTask
        // this is the string output
        let outString = 
            if ret.Count > 0 then seq{ 0 .. (ret.Count - 1) } |>Seq.map (fun x -> ret.[x] |> fromPSobject)|> String.concat "\n"
            else "{}"
        // concatenate output object/string with new line
        let ts = (DateTime.Now - t1).TotalSeconds
        return { cmd = cmd.cmd; output=ret; outstring=outString; status=true; timeUsed=ts; timeBegin = t1; }
    } |> Async.Catch
   
// safe size to 3K, reject long script size
let isSafeSize ( ctx: HttpContext) =
    let inner ( ctx:HttpContext) = 
        async {
            // let contextType = Headers.getFirstHeader "content-length" ctx
            let result = 
                if ctx.request.rawForm.Length < 3072 then
                    Some(ctx)
                else
                    None
            return result
        }
    inner

// run cmd
let runPwsh =
    request (fun r ->
        let t1 = DateTime.Now
        let result =
            r.rawForm
            |> getString
            |> fromJson<PwshCommand>
            |> executeCmd |> Async.RunSynchronously
        (match result with
        | Choice1Of2 psReturn -> psReturn
        | Choice2Of2 ex -> { cmd =""; output=null; outstring=(sprintf "Error: %s\nStackTrace: %s" ex.Message ex.StackTrace); status=false; timeUsed=( DateTime.Now - t1).TotalSeconds; timeBegin = t1; }
        )
        |> toJson
        |> OK )
        >=> setMimeType "application/json"

let upload = 
    request( fun r ->
        r.files
        |> Seq.map (fun y -> 
            let destFilename = Path.Combine(uploadDir, (y.fileName |>safeFilename) ) |> uniqueFile
            File.Move(y.tempFilePath, destFilename)
            sprintf "(%s, %s, %s)" y.fileName y.mimeType y.tempFilePath)
        |> String.concat "\n"
        |> OK
    )
    >=> setMimeType "text/plain"

//setup app routes
let app =
    choose
        [ GET >=> choose
            [ path "/" >=> OK ( sprintf "Welcome to Folaris: %s!" folarisVersion) ]

          POST >=> choose
            [ path "/run" >=> warbler isSafeSize >=> runPwsh ]

          POST >=> choose
            [ path "/upload" >=> upload ]
        ]

[<EntryPoint>]
let main argv =
    async {
        do! Async.Sleep 3000
        printfn "Folaris Version %s started: upload directory is %s" folarisVersion uploadDir
      } |> Async.Start
    runSpacePool.Open()
    let local = Suave.Http.HttpBinding.createSimple HTTP "0.0.0.0" 8080 // listen on all net interfaces
    // to-do: this does not work
    //init logger
    //let logary =
    //    Config.create "Folaris" "0.0.0.0"
    //    |> Config.ilogger (ILogger.LiterateConsole Verbose)
    //    |> Config.targets [
    //      LiterateConsole.create LiterateConsole.empty "console"
    //      // Jaeger.create { Jaeger.empty with jaegerPort = 30831us } "jaeger"
    //    ]
    //    |> Config.build
    //    |> run
    
    //LogaryFacadeAdapter.initialise<Suave.Logging.Logger> logary
    
    // let logger = Logary.Log.create "Folaris"
    
    let webConfig =
      { defaultConfig with
          bindings = [local];
          maxContentLength=2*1024*1024; // limit the size to 2 MB
          //logger = logger;
      }
    // let logger = Suave.Logging.Logger. Logging.LogLevel.Error
    startWebServer webConfig app //(withTracing logger app)
    0
