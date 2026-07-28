[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$workflowPath = Join-Path $PSScriptRoot '..\.github\workflows\ci.yml'
$workflow = Get-Content -Raw -LiteralPath $workflowPath

$requiredFragments = @(
    'name: CI',
    'push:',
    'pull_request:',
    'workflow_dispatch:',
    'contents: read',
    'runs-on: windows-latest',
    'actions/checkout@v6',
    'actions/setup-dotnet@v5',
    'dotnet-version: 10.0.204',
    'run: ./scripts/Invoke-Ci.ps1'
)

foreach ($fragment in $requiredFragments) {
    if ($workflow.IndexOf($fragment, [StringComparison]::Ordinal) -lt 0) {
        throw "CI workflow is missing required fragment: $fragment"
    }
}

$invokeCiPath = Join-Path $PSScriptRoot 'Invoke-Ci.ps1'
$invokeCi = Get-Content -Raw -LiteralPath $invokeCiPath
foreach ($fragment in @('Test-ArchitectureRules.ps1', 'Test-SecretHygiene.ps1')) {
    if ($invokeCi.IndexOf($fragment, [StringComparison]::Ordinal) -lt 0) {
        throw "Invoke-Ci.ps1 is missing required gate: $fragment"
    }
}

if ($workflow -match '(?im)^\s*(environment|deploy|deployment)\s*:' -or $workflow -match '\$\{\{\s*secrets\.') {
    throw 'P00-T03 CI must not include deployment or secret usage.'
}

Write-Output 'CI_WORKFLOW=PASS'
