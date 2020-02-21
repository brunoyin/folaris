
<#
	A simple performance test
	We will Powershell workflow to run in Parallel

	to test:

	1. start up folaris: dotnet run
	2. in Powershell, .\simple-perf-test.ps1
#>

param([int]$total = 1000)

workflow Test-PerfWorkflow
{
param( 
  [parameter(Mandatory = $true)]
		[string]$cmd,
		[int]$total = 1000
)
    Function f {
	<#
	.DESCRIPTION
		Folaris Powershell Command Line
	.PARAMETER cmd
		this can be any Powershell cmdlet, Command Line utilities and Powershell scripts
	.PARAMETER run_url
		default to 'http://localhost:8080/run'
	.EXAMPLE
		# list all environment variables
		Invoke-RemotePwsh -cmd 'gci env:'
		# using pipeline
		'gci env:' |Invoke-RemotePwsh
		# test error handling with an invalid Command
		'Get-Command dude' |Invoke-RemotePwsh
	#>
	[cmdletbinding()]
	param( 
		[parameter(Mandatory = $true, ValueFromPipeline = $true)]
		[string]$cmd,
		[string]$username='folaris',
		[string]$password='folaris',
		[string]$run_url = 'http://localhost:8080/run'
	)
	# encode password
	$base64AuthInfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(("{0}:{1}" -f $username,$password)))
	$authHeader = @{Authorization=("Basic {0}" -f $base64AuthInfo)}
	# 
	$payload = @{cmd = $cmd }|ConvertTo-Json
	# call with Basic Authentication
	$result = Invoke-RestMethod -Headers $authHeader -Uri $run_url -Method Post -Body $payload

	if ($result.status){
		if ($null -ne $result.output ){
			[System.Management.Automation.PSSerializer]::Deserialize($result.output)
		}else{
			$null
		}
		if ($null -ne $result.info ){
			$pwshInfoOut = [System.Management.Automation.PSSerializer]::Deserialize($result.info)
			if ($pwshInfoOut.Count -gt 0 ){
			    $pwshInfoOut | % { Write-Host $($_ |fl * -Force | Out-String ) }
			}
		}
	}else{
		Write-Error $result.outString
	}	
    }

    $numbers = 1 .. $total
    ForEach -Parallel ($i in $numbers)
    {
        $ret = f -cmd $cmd
    }
}

# 1000 in Parallel run, this is not 1000 concurrent
$w = [System.Diagnostics.Stopwatch]::StartNew()
$cmd = 'Get-date'
Test-PerfWorkflow -cmd $cmd -total $total
$w.Stop()
$w
"`n" + ('#*^' * 40) + "`n"
"`n`n{2:#,###} calls done in {0:#,###.##0} seconds, average {1:#.##0} calls per second`n" -f $w.Elapsed.TotalSeconds, $($total / $w.Elapsed.TotalSeconds),$total
"=" * 90

