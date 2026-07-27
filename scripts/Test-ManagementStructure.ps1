param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$repositoryRootPath = [System.IO.Path]::GetFullPath($RepositoryRoot)
$errors = [System.Collections.Generic.List[string]]::new()

function Add-ValidationError {
    param([string]$Message)
    $script:errors.Add($Message)
}

$requiredFiles = @(
    'AGENTS.md',
    'RULES.md',
    'README.md',
    '.gitignore',
    '.ai\CONTEXT.md',
    '.ai\WORKING_MEMORY.md',
    '.ai\NEXT_ACTION.md',
    '.ai\BLOCKERS.md',
    '.ai\GUARDRAILS.md',
    '.ai\OUTPUT_CONTRACT.md',
    'docs\00-product\PROJECT_VISION.md',
    'docs\00-product\PROJECT_SCOPE.md',
    'docs\00-product\BUSINESS_RULES.md',
    'docs\01-domain\ENTITY_LIFECYCLES.md',
    'docs\02-architecture\DEPENDENCY_RULES.md',
    'docs\02-architecture\SYSTEM_CONTEXT.md',
    'docs\02-architecture\MODULE_MAP.md',
    'docs\03-planning\ROADMAP.md',
    'docs\03-planning\PHASES.md',
    'docs\03-planning\CURRENT_PHASE.md',
    'docs\03-planning\MILESTONES.md',
    'docs\04-execution\TASK_BOARD.md',
    'docs\04-execution\FEATURE_SPEC_TEMPLATE.md',
    'docs\04-execution\BUG_REPORT_TEMPLATE.md',
    'docs\04-execution\CHANGE_REQUEST_TEMPLATE.md',
    'docs\04-execution\SESSION_LOG.md',
    'docs\04-execution\HANDOFF.md',
    'docs\05-quality\DEFINITION_OF_DONE.md',
    'docs\05-quality\QUALITY_GATES.md',
    'docs\05-quality\TEST_STRATEGY.md',
    'docs\05-quality\SECURITY_CHECKLIST.md',
    'docs\06-decisions\ADR-000-TEMPLATE.md',
    'docs\06-decisions\DECISION_LOG.md',
    'docs\06-decisions\RISK_REGISTER.md',
    'docs\07-release\GIT_FLOW.md',
    'docs\07-release\RELEASE_CHECKLIST.md',
    'docs\07-release\CHANGELOG.md',
    'docs\07-release\ACHIEVEMENTS.md',
    'plans\README.md',
    'prompts\README.md',
    'scripts\Generate-TaskFiles.ps1',
    'scripts\Test-ManagementStructure.ps1',
    'scripts\Test-TaskGitFlow.ps1',
    'scripts\Test-ProjectPrompts.ps1',
    'scripts\Test-RepositoryReadiness.ps1'
)

$requiredDirectories = @(
    '.ai',
    'docs\00-product',
    'docs\01-domain',
    'docs\02-architecture',
    'docs\03-planning',
    'docs\04-execution',
    'docs\05-quality',
    'docs\06-decisions',
    'docs\07-release',
    'plans',
    'prompts',
    'scripts',
    'src',
    'tests'
)

foreach ($relativePath in $requiredFiles) {
    $path = Join-Path $repositoryRootPath $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Add-ValidationError "Missing required file: $relativePath"
    }
}

foreach ($relativePath in $requiredDirectories) {
    $path = Join-Path $repositoryRootPath $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
        Add-ValidationError "Missing required directory: $relativePath"
    }
}

$plansRoot = Join-Path $repositoryRootPath 'plans'
$phaseDirectories = @(
    Get-ChildItem -LiteralPath $plansRoot -Directory -Filter 'phase-*' -ErrorAction SilentlyContinue |
        Sort-Object Name
)
if ($phaseDirectories.Count -ne 8) {
    Add-ValidationError "Phase directory count mismatch: expected=8, actual=$($phaseDirectories.Count)"
}

foreach ($phaseDirectory in $phaseDirectories) {
    foreach ($requiredPhasePath in @('README.md', 'TASKS.md', 'GIT_FLOW.md', 'tasks\README.md')) {
        $phasePath = Join-Path $phaseDirectory.FullName $requiredPhasePath
        if (-not (Test-Path -LiteralPath $phasePath -PathType Leaf)) {
            Add-ValidationError "Missing phase artifact: $($phaseDirectory.Name)\$requiredPhasePath"
        }
    }
}

$decisionLogPath = Join-Path $repositoryRootPath 'docs\06-decisions\DECISION_LOG.md'
if (Test-Path -LiteralPath $decisionLogPath -PathType Leaf) {
    $decisionLines = Get-Content -LiteralPath $decisionLogPath -Encoding utf8
    foreach ($line in $decisionLines) {
        if ($line -notmatch '^\| (?<id>DEC-\d{3}) \| .+ \| Accepted \| [^|]+ \| `(?<adr>ADR-[^`]+\.md)` \|$') {
            continue
        }

        $adrPath = Join-Path (Split-Path -Parent $decisionLogPath) $Matches.adr
        if (-not (Test-Path -LiteralPath $adrPath -PathType Leaf)) {
            Add-ValidationError "$($Matches.id) is Accepted but ADR is missing: $($Matches.adr)"
        }
    }
}

$markdownRoots = @(
    (Join-Path $repositoryRootPath 'README.md'),
    (Join-Path $repositoryRootPath 'AGENTS.md'),
    (Join-Path $repositoryRootPath 'RULES.md'),
    (Join-Path $repositoryRootPath '.ai'),
    (Join-Path $repositoryRootPath 'docs'),
    (Join-Path $repositoryRootPath 'plans'),
    (Join-Path $repositoryRootPath 'prompts')
)
$markdownFiles = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
foreach ($markdownRoot in $markdownRoots) {
    if (Test-Path -LiteralPath $markdownRoot -PathType Leaf) {
        $markdownFiles.Add((Get-Item -LiteralPath $markdownRoot))
    }
    elseif (Test-Path -LiteralPath $markdownRoot -PathType Container) {
        foreach ($markdownFile in Get-ChildItem -LiteralPath $markdownRoot -Recurse -File -Filter '*.md') {
            $markdownFiles.Add($markdownFile)
        }
    }
}

$legacyReferencePattern = '(PROJECT_VISION|PROJECT_SCOPE|BUSINESS_RULES|ENTITY_LIFECYCLES)_PEW_PEW_ASSISTANT\.md'
foreach ($markdownFile in $markdownFiles) {
    $content = Get-Content -LiteralPath $markdownFile.FullName -Raw -Encoding utf8
    if ($content -match $legacyReferencePattern) {
        Add-ValidationError "Legacy document reference in $($markdownFile.FullName): $($Matches[0])"
    }

    $linkMatches = [regex]::Matches($content, '\[[^\]]+\]\((?<target>[^)]+)\)')
    foreach ($linkMatch in $linkMatches) {
        $target = $linkMatch.Groups['target'].Value.Trim()
        if ($target -match '^(https?://|mailto:|app://|#)' -or $target -match '[<{]') {
            continue
        }

        $pathPart = ($target -split '#', 2)[0].Trim('<', '>')
        if ([string]::IsNullOrWhiteSpace($pathPart)) {
            continue
        }

        try {
            $resolvedTarget = [System.IO.Path]::GetFullPath((Join-Path $markdownFile.DirectoryName $pathPart))
            if (-not (Test-Path -LiteralPath $resolvedTarget)) {
                Add-ValidationError "Broken local link in $($markdownFile.FullName): $target"
            }
        }
        catch {
            Add-ValidationError "Invalid local link in $($markdownFile.FullName): $target"
        }
    }
}

if ($errors.Count -gt 0) {
    foreach ($validationError in $errors) {
        Write-Output "ERROR: $validationError"
    }
    throw "Management structure validation failed with $($errors.Count) error(s)."
}

Write-Output "REQUIRED_FILES=$($requiredFiles.Count)"
Write-Output "REQUIRED_DIRECTORIES=$($requiredDirectories.Count)"
Write-Output "PHASE_DIRECTORIES=$($phaseDirectories.Count)"
Write-Output "MARKDOWN_FILES=$($markdownFiles.Count)"
Write-Output 'ACCEPTED_ADR_REFERENCES=PASS'
Write-Output 'LEGACY_REFERENCES=PASS'
Write-Output 'ALL_LOCAL_MARKDOWN_LINKS=PASS'
Write-Output 'MANAGEMENT_STRUCTURE=PASS'
