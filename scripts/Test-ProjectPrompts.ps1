param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$repositoryRootPath = [System.IO.Path]::GetFullPath($RepositoryRoot)
$promptsRoot = Join-Path $repositoryRootPath 'prompts'
$readmePath = Join-Path $promptsRoot 'README.md'
$errors = [System.Collections.Generic.List[string]]::new()

$expectedPromptNames = @(
    '00-bootstrap.md',
    '01-plan-phase.md',
    '02-implement-task.md',
    '03-review-code.md',
    '04-fix-bug.md',
    '05-refactor.md',
    '06-security-review.md',
    '07-handoff.md',
    '08-release.md'
)

$allowedTokens = @(
    'AREA',
    'BEHAVIOR_CONTRACT',
    'BUG_ID',
    'CHANNEL',
    'CONSTRAINTS',
    'C00',
    'DATA_AND_SIDE_EFFECTS',
    'EXPECTED_BEHAVIOR',
    'FOCUS',
    'G00',
    'MEASURABLE_REASON',
    'NEXT_TASK_ID',
    'OBSERVED_BEHAVIOR',
    'PHASE_ID',
    'PHASE_PATH',
    'PLANNING_GOAL',
    'RELEASE_TARGET',
    'REVIEW_TARGET',
    'STATUS',
    'TASK_ID',
    'VERSION'
)

function Add-ValidationError {
    param([string]$Message)
    $script:errors.Add($Message)
}

if (-not (Test-Path -LiteralPath $readmePath -PathType Leaf)) {
    throw "Prompt README not found: $readmePath"
}

$readme = Get-Content -LiteralPath $readmePath -Raw -Encoding utf8
$goalConstraintMapPath = Join-Path $promptsRoot 'GOAL_CONSTRAINT_MAP.md'
$goalConstraintMap = ''
if (Test-Path -LiteralPath $goalConstraintMapPath -PathType Leaf) {
    $goalConstraintMap = Get-Content -LiteralPath $goalConstraintMapPath -Raw -Encoding utf8
}
$promptFiles = Get-ChildItem -LiteralPath $promptsRoot -File -Filter '*.md' |
    Where-Object { $_.Name -match '^\d{2}-.*\.md$' } |
    Sort-Object Name

if ($promptFiles.Count -ne $expectedPromptNames.Count) {
    Add-ValidationError "Prompt count mismatch: expected=$($expectedPromptNames.Count), actual=$($promptFiles.Count)"
}

$allPromptContent = [System.Text.StringBuilder]::new()
[void]$allPromptContent.AppendLine($readme)
foreach ($expectedName in $expectedPromptNames) {
    $promptPath = Join-Path $promptsRoot $expectedName
    if (-not (Test-Path -LiteralPath $promptPath -PathType Leaf)) {
        Add-ValidationError "Missing prompt: $expectedName"
        continue
    }

    if (-not $readme.Contains("($expectedName)")) {
        Add-ValidationError "Prompt README missing link: $expectedName"
    }

    $content = Get-Content -LiteralPath $promptPath -Raw -Encoding utf8
    [void]$allPromptContent.AppendLine($content)

    if ($content.Length -lt 800) {
        Add-ValidationError "Prompt is too small to carry project contract: $expectedName"
    }

    $requiredPatterns = @(
        '(?m)^# Prompt',
        '(?m)^## Input$',
        '(?m)^## Vai tr',
        '(?m)^## Context b',
        '(?m)^## Output b',
        'Pew Pew Assistant',
        'AGENTS\.md'
    )

    foreach ($pattern in $requiredPatterns) {
        if ($content -notmatch $pattern) {
            Add-ValidationError "$expectedName missing required pattern: $pattern"
        }
    }

    $legacyPlaceholders = [regex]::Matches($content, '<(?:PHASE|TASK|BUG|AREA|VERSION|ID)[^>]*>')
    foreach ($placeholder in $legacyPlaceholders) {
        Add-ValidationError "$expectedName contains legacy placeholder: $($placeholder.Value)"
    }

    $tokens = [regex]::Matches($content, '\{(?<token>[A-Z][A-Z0-9_]*)\}')
    foreach ($tokenMatch in $tokens) {
        $token = $tokenMatch.Groups['token'].Value
        if ($allowedTokens -notcontains $token) {
            Add-ValidationError "$expectedName contains unknown token: {$token}"
        }
    }
}

$combined = $allPromptContent.ToString()
$combined += [Environment]::NewLine + $goalConstraintMap
$requiredProjectConcepts = @(
    'Windows-first',
    'Modular Monolith',
    'Local',
    'Private Mode',
    'default deny',
    'Policy Engine',
    'Level 3',
    'P04',
    'P05',
    'P07',
    'phase integration branch',
    'Task ID'
)

foreach ($concept in $requiredProjectConcepts) {
    if ($combined -notmatch [regex]::Escape($concept)) {
        Add-ValidationError "Prompt pack missing project concept: $concept"
    }
}

$markdownFiles = Get-ChildItem -LiteralPath $promptsRoot -File -Filter '*.md'
foreach ($markdownFile in $markdownFiles) {
    $content = Get-Content -LiteralPath $markdownFile.FullName -Raw -Encoding utf8
    $matches = [regex]::Matches($content, '\[[^\]]+\]\((?<target>[^)]+)\)')
    foreach ($match in $matches) {
        $target = $match.Groups['target'].Value
        if ($target -match '^(https?://|mailto:|#)') {
            continue
        }

        $pathPart = ($target -split '#', 2)[0]
        $resolvedTarget = [System.IO.Path]::GetFullPath((Join-Path $markdownFile.DirectoryName $pathPart))
        if (-not (Test-Path -LiteralPath $resolvedTarget)) {
            Add-ValidationError "Broken prompt link in $($markdownFile.Name): $target"
        }
    }
}

if ($errors.Count -gt 0) {
    foreach ($validationError in $errors) {
        Write-Output "ERROR: $validationError"
    }
    throw "Project prompt validation failed with $($errors.Count) error(s)."
}

Write-Output "PROMPTS=$($promptFiles.Count)"
Write-Output 'USAGE_GUIDE=PASS'
Write-Output 'REQUIRED_STRUCTURE=PASS'
Write-Output 'PROJECT_CONTRACT_COVERAGE=PASS'
Write-Output 'TOKEN_SCHEMA=PASS'
Write-Output 'LEGACY_PLACEHOLDERS=NONE'
Write-Output 'LOCAL_MARKDOWN_LINKS=PASS'
Write-Output 'VALIDATION=PASS'
