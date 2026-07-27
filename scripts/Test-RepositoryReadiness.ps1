param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$FailIfNotReady
)

$ErrorActionPreference = 'Stop'
$repositoryRootPath = [System.IO.Path]::GetFullPath($RepositoryRoot)
$blockers = [System.Collections.Generic.List[string]]::new()

function Get-DocumentStatus {
    param([string]$RelativePath)

    $path = Join-Path $repositoryRootPath $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return 'MISSING'
    }

    $lines = Get-Content -LiteralPath $path -Encoding utf8
    foreach ($line in $lines | Select-Object -First 20) {
        if ($line -match '^\| [^|]+ \| (?<status>[^|]*(?:baseline|proposed|approved|accepted)[^|]*) \|$') {
            return $Matches.status.Trim()
        }
    }

    return 'UNKNOWN'
}

function Test-ApprovedStatus {
    param([string]$Status)
    return $Status -match '(?i)\b(approved|accepted)\b' -and $Status -notmatch '(?i)\bproposed\b'
}

$scopeStatus = Get-DocumentStatus -RelativePath 'docs\00-product\PROJECT_SCOPE.md'
$businessRulesStatus = Get-DocumentStatus -RelativePath 'docs\00-product\BUSINESS_RULES.md'
$lifecycleStatus = Get-DocumentStatus -RelativePath 'docs\01-domain\ENTITY_LIFECYCLES.md'

if (-not (Test-ApprovedStatus -Status $scopeStatus)) {
    $blockers.Add("Product Scope baseline is not approved: $scopeStatus")
}
if (-not (Test-ApprovedStatus -Status $businessRulesStatus)) {
    $blockers.Add("Business Rules baseline is not approved: $businessRulesStatus")
}
if (-not (Test-ApprovedStatus -Status $lifecycleStatus)) {
    $blockers.Add("Entity Lifecycle baseline is not approved: $lifecycleStatus")
}

$decisionLogPath = Join-Path $repositoryRootPath 'docs\06-decisions\DECISION_LOG.md'
$decisionLog = Get-Content -LiteralPath $decisionLogPath -Raw -Encoding utf8
$requiredAcceptedDecisions = @('DEC-001', 'DEC-002', 'DEC-003', 'DEC-004', 'DEC-005')
foreach ($decisionId in $requiredAcceptedDecisions) {
    if ($decisionLog -notmatch "(?m)^\| $decisionId \| .+ \| Accepted \|") {
        $blockers.Add("$decisionId is not Accepted")
    }
}
if ($decisionLog -notmatch '(?m)^\| DEC-006 \| .+ \| Accepted \|') {
    $blockers.Add('DEC-006 Git flow policy is not Accepted')
}

$blockerPath = Join-Path $repositoryRootPath '.ai\BLOCKERS.md'
$openBlockerIds = [System.Collections.Generic.List[string]]::new()
foreach ($line in Get-Content -LiteralPath $blockerPath -Encoding utf8) {
    if ($line -match '^\| (?<id>BLK-\d{3}) \| .+ \| Open \|$') {
        $openBlockerIds.Add($Matches.id)
    }
}
if ($openBlockerIds.Count -gt 0) {
    $blockers.Add("Open decision blockers: $($openBlockerIds -join ', ')")
}

$gitMetadataPresent = Test-Path -LiteralPath (Join-Path $repositoryRootPath '.git') -PathType Container
$gitHasCommit = $false
if ($gitMetadataPresent) {
    $gitRoot = Join-Path $repositoryRootPath '.git'
    $headPath = Join-Path $gitRoot 'HEAD'
    if (Test-Path -LiteralPath $headPath -PathType Leaf) {
        $headValue = (Get-Content -LiteralPath $headPath -Raw).Trim()
        if ($headValue -match '^ref: (?<reference>refs/.+)$') {
            $looseReferencePath = Join-Path $gitRoot $Matches.reference
            if (Test-Path -LiteralPath $looseReferencePath -PathType Leaf) {
                $gitHasCommit = $true
            }
            else {
                $packedReferencesPath = Join-Path $gitRoot 'packed-refs'
                if (Test-Path -LiteralPath $packedReferencesPath -PathType Leaf) {
                    $packedReferences = Get-Content -LiteralPath $packedReferencesPath -Raw
                    $gitHasCommit = $packedReferences -match "(?m)^[0-9a-f]{40} $([regex]::Escape($Matches.reference))$"
                }
            }
        }
        elseif ($headValue -match '^[0-9a-f]{40}$') {
            $gitHasCommit = $true
        }
    }
}
$solutionFiles = @(
    Get-ChildItem -LiteralPath $repositoryRootPath -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in @('.sln', '.slnx') }
)
$projectFiles = @(
    Get-ChildItem -LiteralPath $repositoryRootPath -Recurse -File -Filter '*.csproj' -ErrorAction SilentlyContinue
)
$workflowRoot = Join-Path $repositoryRootPath '.github\workflows'
$workflowFiles = @()
if (Test-Path -LiteralPath $workflowRoot -PathType Container) {
    $workflowFiles = @(
        Get-ChildItem -LiteralPath $workflowRoot -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Extension -in @('.yml', '.yaml') }
    )
}
$architectureTestProjects = @(
    $projectFiles | Where-Object { $_.BaseName -match '(?i)architecture' }
)

if (-not $gitMetadataPresent) {
    $blockers.Add('Git repository metadata is not initialized')
}
elseif (-not $gitHasCommit) {
    $blockers.Add('Git repository has no baseline commit')
}
if ($solutionFiles.Count -eq 0) {
    $blockers.Add('No .sln or .slnx exists')
}
if ($projectFiles.Count -eq 0) {
    $blockers.Add('No .csproj exists')
}
if ($workflowFiles.Count -eq 0) {
    $blockers.Add('No CI workflow exists under .github/workflows')
}
if ($architectureTestProjects.Count -eq 0) {
    $blockers.Add('No architecture test project exists')
}

$implementationReady = $blockers.Count -eq 0
$readyValue = if ($implementationReady) { 'YES' } else { 'NO' }
$gitValue = if ($gitMetadataPresent) { 'YES' } else { 'NO' }
$gitCommitValue = if ($gitHasCommit) { 'YES' } else { 'NO' }

Write-Output 'READINESS_CHECK=PASS'
Write-Output "IMPLEMENTATION_READY=$readyValue"
Write-Output "GIT_METADATA=$gitValue"
Write-Output "GIT_HAS_COMMIT=$gitCommitValue"
Write-Output "SOLUTION_FILES=$($solutionFiles.Count)"
Write-Output "PROJECT_FILES=$($projectFiles.Count)"
Write-Output "CI_WORKFLOWS=$($workflowFiles.Count)"
Write-Output "ARCHITECTURE_TEST_PROJECTS=$($architectureTestProjects.Count)"
Write-Output "SCOPE_STATUS=$scopeStatus"
Write-Output "BUSINESS_RULES_STATUS=$businessRulesStatus"
Write-Output "LIFECYCLE_STATUS=$lifecycleStatus"
Write-Output "OPEN_BLOCKERS=$($openBlockerIds.Count)"
foreach ($blockingReason in $blockers) {
    Write-Output "BLOCKING_REASON=$blockingReason"
}

if (-not $implementationReady -and $FailIfNotReady) {
    throw "Repository is not implementation-ready; $($blockers.Count) blocking condition(s) remain."
}
