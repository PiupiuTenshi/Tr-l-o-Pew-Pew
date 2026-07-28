[CmdletBinding()]
param(
    [switch]$SimulateFailure
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-DotnetCommand {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

if ($SimulateFailure) {
    throw 'Simulated CI failure: this verifies that the workflow command exits non-zero.'
}

Invoke-DotnetCommand -Arguments @('restore', 'PewPew.sln')
Invoke-DotnetCommand -Arguments @('build', 'PewPew.sln', '--configuration', 'Release', '--no-restore')
& "$PSScriptRoot/Test-ArchitectureRules.ps1" -NoBuild
if ($LASTEXITCODE -ne 0) {
    throw "Architecture rule gate failed with exit code $LASTEXITCODE."
}

& "$PSScriptRoot/Test-SecretHygiene.ps1"
if ($LASTEXITCODE -ne 0) {
    throw "Secret hygiene gate failed with exit code $LASTEXITCODE."
}

Invoke-DotnetCommand -Arguments @('test', 'PewPew.sln', '--configuration', 'Release', '--no-build', '--nologo')
