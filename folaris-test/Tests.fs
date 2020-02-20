module Tests

open System
open Xunit
open Xunit.Abstractions

open FSharp.Data
open FSharp.Data.HttpRequestHeaders

open Newtonsoft.Json

//types
type PwshCommand =
    {
        cmd: string
    }


let GetEnvVar varName defaultValue = 
    match Environment.GetEnvironmentVariable(varName) with
    | null -> defaultValue
    | value -> value
    
// set password
let folarisUsername = GetEnvVar "FOLARIS_USER" "folaris"
let folarisPasswd = GetEnvVar "FOLARIS_PASSWD" "folaris"

let runTest (psCommand: PwshCommand) (output:ITestOutputHelper) : HttpResponse =
    let payload = JsonConvert.SerializeObject psCommand
    let result = Http.Request(url = "http://localhost:8080/run", httpMethod=HttpMethod.Post, body = (TextRequest payload), headers=[BasicAuth folarisUsername folarisPasswd; Accept HttpContentTypes.Json] )
    match result.Body with
    | Text text -> output.WriteLine("reply: {0}",  text)
    | _ -> output.WriteLine("Unexpected: We are expecting text")
    result

type FolarisTest(output:ITestOutputHelper) =
    [<Fact>]
    member x.``Test Get-Date`` () =
        let psCommand = {cmd = "Get-Date"}
        let result = runTest psCommand output
        Assert.True(result.StatusCode = 200)

    [<Fact>]
    member x.``Error: Get-Command dur`` () =
        let psCommand = {cmd = "Get-Command dur"}
        let result = runTest psCommand output
        Assert.True(result.StatusCode = 200)

    [<Fact>]
    member x.``Error: with Write-Information`` () =
        let psCommand = {cmd = @"Write-Information 'Begin'; $PSVersionTable;  Write-Information 'End'"}
        let result = runTest psCommand output
        Assert.True(result.StatusCode = 200)

    [<Theory>]
    [<InlineData("gci env:")>]  
    [<InlineData("gci variable:")>]
    [<InlineData(@"[System.AppDomain]::CurrentDomain.GetAssemblies()|select -ExpandProperty Location |select -First 1")>]
    [<InlineData(@"Write-Information $(Get-Date)")>]
    member x.``Test a few few more commands `` (cmd : string) : unit = 
        let psCommand = {cmd = cmd}
        let result = runTest psCommand output
        Assert.True(result.StatusCode = 200)