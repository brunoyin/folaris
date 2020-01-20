
Function Invoke-RemotePwsh {
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
		[parameter(
        Mandatory = $true, ValueFromPipeline = $true)]
		[string]$cmd,
		[string]$run_url = 'http://localhost:8080/run'
	)
	$payload = @{cmd = $cmd }|ConvertTo-Json
	$result = Invoke-RestMethod -Uri $run_url -Method Post -Body $payload

	if ($result.status){
		if ( $result.output.'$values'.clixml -is [array] ){
			$result.output.'$values'.Clixml | % { [System.Management.Automation.PSSerializer]::Deserialize($_) }
		}else{
			[System.Management.Automation.PSSerializer]::Deserialize($result.output.'$values'.Clixml)
		}
	}else{
		Write-Error $result.outString
	}	
}

Function Check-Folaris {
	<#
	.DESCRIPTION
		Check if Folaris is running
	.PARAMETER folaris_url
		default to 'http://localhost:8080/'
	#>
	[cmdletbinding()]
	param( 
		[parameter(Mandatory = $false)]
		[string]$folaris_url = 'http://localhost:8080'
	)
	$ErrorActionPreference = 'Stop'
	try {
		Invoke-RestMethod -Uri http://localhost:8080/ -UseBasicParsing
	}catch {
		Write-Host $($_|fl * -Force |Out-String)
	}
}

# Aliases
New-Alias -Name folaris -Value Check-Folaris
New-Alias -Name f -Value Invoke-RemotePwsh
