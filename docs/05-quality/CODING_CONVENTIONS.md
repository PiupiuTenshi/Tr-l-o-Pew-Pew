# Coding Conventions and Warning Policy

## Scope

This convention applies to repository-owned .NET source, project files and CI. It implements P00-T04 and is subordinate to `AGENTS.md`, accepted decisions and the quality gates.

## Baseline

- Use UTF-8 text, LF line endings, four spaces for C# and two spaces for MSBuild XML.
- Keep nullable reference types and implicit usings enabled; do not opt out per project without an approved task and recorded reason.
- Prefer file-scoped namespaces for new C# files, braces for every control-flow body, explicit accessibility for non-interface members and `readonly` fields where applicable.
- Use the repository root `.editorconfig` as the editor and formatter source of truth. Do not add conflicting per-project editor configuration without an approved task.

## Analyzer and warning policy

- Built-in .NET analyzers run at `latest-recommended` level in every build.
- Code-style diagnostics are evaluated during build.
- Compiler and analyzer warnings are errors in local Release builds and CI. There is no global warning suppression list.
- A task that needs a suppression must identify the diagnostic, explain why the warning is inapplicable, limit scope to the smallest code location and record the exception in its evidence. Disabling analyzers, lowering analysis level or adding broad `NoWarn` entries to pass a gate is prohibited.

## Required local checks

Run these from the repository root before handing off a .NET change:

```powershell
dotnet format whitespace PewPew.sln --verify-no-changes --no-restore
dotnet build PewPew.sln --configuration Release --nologo
dotnet test PewPew.sln --configuration Release --no-build --nologo
```

`scripts/Invoke-Ci.ps1` is the CI-equivalent restore, Release build and test command. CI is the final hosted evidence; local checks do not replace it.

## Review boundary

Formatting and analyzer changes must not be used to mix unrelated refactors into a feature task. If a pre-existing violation is discovered outside scope, record it and create a separate task rather than suppressing the warning or modifying unrelated code.
