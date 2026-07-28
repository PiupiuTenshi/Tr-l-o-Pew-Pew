[CmdletBinding()]
param(
    [switch]$NoBuild,
    [switch]$SimulateFailure
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($SimulateFailure) {
    throw 'Simulated architecture violation: Application references Infrastructure.'
}

$arguments = @(
    'test',
    'tests/PewPew.Architecture.Tests/PewPew.Architecture.Tests.csproj',
    '--configuration', 'Release',
    '--nologo',
    '--filter', 'FullyQualifiedName~ProjectReferenceArchitectureTests'
)

if ($NoBuild) {
    $arguments += '--no-build'
}

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Architecture rule gate failed with exit code $LASTEXITCODE."
}

Write-Output 'ARCHITECTURE_RULES=PASS'
