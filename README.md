
## Folaris

Folaris is a simple web app with an embedded Powershell and functions just like WinRM without Windows integreted authentication. It allows you to run it on any servers where you can host a dotnet core app.

It's experimental, not for use in production. I recommend running in Docker first.

Inspired by [Polaris: A cross-platform, minimalist web framework for PowerShell](https://github.com/PowerShell/Polaris) and Powershell WinRM remote execution using Invoke-Command.

Because it's written in F#, I named it Folaris after Polaris.

Folaris is based on [SUAVE: a simple web development F# library](https://github.com/SuaveIO/suave) with an embedded Powershell.

## features

* Run shell and Powershell commands on a hosting server via HTTP similar to WinRM without the integreted authentication.
* Works on any servers where a dotnet core app can run. That means Windows, Linux, MacOS.
* The POST must be in correct JSON format, the JSON payload must be less than 3KB.
* Upload files to the hosting server

## How it works

* Folaris web app has an embedded Powershell that executes Powershell cmdlets as well as shell commands available on the hosting server
* Powershell output is a dotnet object or PSDataCollection<PSObject> and Folaris serialize it and send it to client, the client uses the same to deserialize.

### Warning: Folaris is not safe

* This is a proof of concept
* It executes any commands without authentication
* Run it in Docker with read-only volume mounting option

## running from the source

* dotnet core sdk 3.1 required
* clone this repo
* run: dotnet run
* use folarisCli Powershell module to see it in action
```powershell
# use the default 8080
dotnet run
# use a different port
$env:FOLARIS_PORT = '8087'
# or in Bash: export FOLARIS_PORT='8087'
dotnet run
```

### Build/Publish as platform dependent to be used with a dotnet runtime Docker image

```bash
# the default target is dotnet core 3.1 app
# publish to run in Docker with Microsoft runtime Docker image: mcr.microsoft.com/dotnet/core/runtime:3.1
dotnet publish -c Release -r linux-x64 --no-self-contained  -o ./build
```

### Running in Docker

* docker pull mcr.microsoft.com/dotnet/core/runtime:3.1
* Run in Docker

```bash
# executing an arbitrary command is dangerous, Docker containers are perfect for testing
# mark the volume to be read-only
# this a Bash version of docker run. Powershell use ` for line continuation
docker run -ti --rm \
	-p 8080:8080 \
	-v $PWD/build:/folaris:ro \
	-w /folaris \
	-e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
	mcr.microsoft.com/dotnet/core/runtime:3.1 \
	dotnet folaris.dll

# To run on different port, use FOLARIS_PORT variable to set the your port
# Of cause docker can map to any different port. But if you have another container already using port 8080 internally
# you can do something like:
export FOLARIS_PORT='8087'
docker run -ti --rm \
	-p $FOLARIS_PORT:$FOLARIS_PORT \
	-v $PWD/build:/folaris:ro \
	-w /folaris \
	-e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
	-e FOLARIS_PORT=$FOLARIS_PORT \
	mcr.microsoft.com/dotnet/core/runtime:3.1 \
	dotnet folaris.dll
```

### Defined HTTP routes

* GET / 
* POST /run
```powershell
# expect JSON {"cmd" : ""}. 
# send your commamd
$body = @{cmd='gci env:'}
$payload = $body | ConvertTo-Json
$ret = Invoke-RestMethod -Uri http://localhost:8080/run -Method Post -Body $payload
```
* POST /upload
```powershell
# using curl is the easiest way to upload
curl.exe -F 'file=@.\README.md' http://localhost:8080/upload
```

## Testing with folarisCli Powershell module

* This module provides a Powershell cmdlet similar to Invoke-Command. For example, Invoke-Command -ComputerName hostname -ScriptBlock { gci env: }

```powershell
# currently folarisCli has 2 functions to simplify executing the a remote command
import-module ./folaris-cli/folarisCli.psd1 -Verbose
# use Invoke-RemotePwsh or its alias f
help -full Invoke-RemotePwsh
help -full  f
# use Check-Folaris or its alias folaris
help -full Check-Folaris
help -full folaris
```
* See it in action

```powershell
# Invoke-RemotePwsh take an optiona run_url parameter, it defaults to http://localhost:8080/run
# example using pipeline: list files/direcories showing only short names
'gci . |select -exp Name' | Invoke-RemotePwsh
# example using parameter: Get-Date
Invoke-RemotePwsh -cmd 'get-date'
# example using run_url parameter on a different computer, default is localhost
Invoke-RemotePwsh -cmd 'get-date' -run_url http://192.168.1.250:8080/run
# or 
f -cmd 'get-date' -run_url http://192.168.1.250:8080/run
# or
'get-date' |f -run_url http://192.168.1.250:8080/run
# Let's produce and error: assume dude is not a valid command
'Get-Command dude' |f -run_url http://192.168.1.250:8080/run
```

More details in [folaris-tests.ps1 ](folaris-tests.ps1)


### Thanks to

* [Polaris: A cross-platform, minimalist web framework for PowerShell](https://github.com/PowerShell/Polaris) 
* [SUAVE: a simple web development F# library](https://github.com/SuaveIO/suave)
* [MorganW09: Basic Suave API Example](https://github.com/MorganW09/SuaveAPI) as my startup template
