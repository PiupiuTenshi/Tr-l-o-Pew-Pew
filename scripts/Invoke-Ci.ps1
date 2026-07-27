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
Invoke-DotnetCommand -Arguments @('test', 'PewPew.sln', '--configuration', 'Release', '--no-build', '--nologo')
