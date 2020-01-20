
## Folaris CLI 

This a Powershell module that wraps Folaris remote execution calls. 

## How it works

Folaris web server expose /run HTTP POST for executing a Powershell script or a simple commnand. The server requires the command to be formatted in JSON.

For example, to execute cmdlet Get-Date, it expects in the HTTP POST payload to be:

```json
{
    "cmd":  "Get-Date"
}
```

We can do it in Powershell like:
```powershell
$payload = @{cmd='Get-Date'} |ConvertTo-Json
$ret = Invoke-RestMethod -Uri http://localhost:8080/run -Method Post -Body $payload
#
```

The return of a successful call stores the actual command return in $ret.output.'$values'.CliXml, we can use System.Management.Automation.PSSerializer to deserialize back to dotnet objects.

## Provided cmdlets and aliases

Check-Folaris/alias folaris: Check-Folaris [[-folaris_url] <String>]

Invoke-RemotePwsh/alias f: Invoke-RemotePwsh [-cmd] <String> [[-run_url] <String>]

## examples:

```powershell
$folaris_url = http://my-host-or-ip:8080
$run_url = '{0}/run' -f $folaris_url
#
folaris http://my-host-or-ip:8080
# 
f -cmd 'gci env:' -run_url $run_url
# or using pipeline
'Get-Date' | f -run_url $run_url
# produce an error
'Get-Command dude' | f -run_url $run_url
# running a script 
$text = '
Get-Date
foreach($x in $(1 .. 5)){ $x }
'
$text | f -run_url $run_url




