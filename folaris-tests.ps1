# Intended to run under Powershell 5/core or later
# start the container
docker run -ti --rm `
	-p 8080:8080 `
	-v $PWD/build:/folaris:ro `
	-w /folaris `
	-e DOTNET_CLI_TELEMETRY_OPTOUT=1 `
	mcr.microsoft.com/dotnet/core/runtime:3.1 `
	dotnet folaris.dll

# $run_url = 'http://localhost:8080/run'
$run_url = 'http://192.168.1.250:8080/run'
# $upload_url = http://localhost:8080/upload
$upload_url = http://192.168.1.250:8080/upload

# load folarisCli module 
Import-Module ./folaris-cli/folarisCli.psd1 -Verbose
# 
Get-Command -Module folarisCli
<#
CommandType     Name                                               Version    Source
-----------     ----                                               -------    ------
Function        Check-Folaris                                      0.0.1      folarisCli
Function        Invoke-RemotePwsh                                  0.0.1      folarisCli

Get-Alias f

CommandType     Name                                               Version    Source
-----------     ----                                               -------    ------
Alias           f -> Invoke-RemotePwsh                             0.0.1      folarisCli

Get-Alias folaris

CommandType     Name                                               Version    Source
-----------     ----                                               -------    ------
Alias           folaris -> Check-Folaris                           0.0.1      folarisCli
#>

# run some tests
'gci env:' |f # same as: 'gci env:' | Invoke-RemotePwsh
f -cmd 'Get-Date'
folaris
# Folaris running on a remote computer
Check-Folaris -folaris_url http://192.168.1.4:8080
# Test error handling
'Get-Command dude' |f -run_url http://192.168.1.4:8080/run

# upload: curl is easier if you have it, Powershell file uploading takes a little more effort
# running on Windows using curl
curl.exe -F 'file=@.\README.md' $upload_url


