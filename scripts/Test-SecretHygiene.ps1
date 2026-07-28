[CmdletBinding()]
param(
    [switch]$SimulateFailure
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$sensitiveFilePattern = '(?i)(^|/)(\.env(\..*)?|secrets\.json|[^/]+\.(pfx|p12|pem|key))$'
$secretAssignmentPattern = '(?im)\b(?:api[_-]?key|access[_-]?token|refresh[_-]?token|client[_-]?secret|password)\b\s*(?:=|:)\s*["''](?!(?:<|\$\{|\{|\[|YOUR_|REPLACE_|example|changeme))[^"''\r\n]{8,}["'']'
$sourceExtensions = @('.cs', '.csproj', '.props', '.targets', '.json', '.yml', '.yaml', '.ps1', '.config')
$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Assert-NoSecretAssignment {
    param(
        [Parameter(Mandatory)]
        [string]$Content,
        [Parameter(Mandatory)]
        [string]$Source
    )

    if ($Content -match $secretAssignmentPattern) {
        throw "Potential plaintext secret found in $Source. Remove it and use a secret adapter."
    }
}

if ($SimulateFailure) {
    $simulatedValue = 'simulated-' + 'secret-value'
    Assert-NoSecretAssignment -Content "client_secret = `"$simulatedValue`"" -Source 'simulation'
}

$trackedFiles = @(git ls-files)
$sensitiveFiles = @($trackedFiles | Where-Object { $_ -match $sensitiveFilePattern })
if ($sensitiveFiles.Count -gt 0) {
    throw "Sensitive file tracked by Git: $($sensitiveFiles -join ', ')"
}

foreach ($path in $trackedFiles) {
    if ($sourceExtensions -notcontains [IO.Path]::GetExtension($path).ToLowerInvariant()) {
        continue
    }

    $fullPath = [IO.Path]::Combine($repositoryRoot, $path)
    Assert-NoSecretAssignment -Content ([IO.File]::ReadAllText($fullPath)) -Source $path
}

Write-Output 'SECRET_HYGIENE=PASS'
