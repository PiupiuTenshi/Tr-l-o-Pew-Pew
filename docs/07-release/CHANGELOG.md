# Changelog

Tài liệu theo Keep a Changelog ở mức khái niệm.

## Unreleased

### Added

- Vibe coding management system.
- Product, domain and architecture documentation baseline.
- Detailed P00–P07 execution roadmap with task, dependency, traceability and phase gates.
- Per-phase small-task backlogs with size, status, dependency and acceptance evidence.
- Individual executable task specification files and per-phase task indexes.
- Proposed phase-aligned Git flow with 8 phase mappings and per-task branch/PR contracts.
- Project-specific engineering prompt pack for bootstrap, planning, implementation, review, debug, refactor, security, handoff and release.
- ADR-001 through ADR-005 for decisions already recorded as Accepted.
- Windows-native management-structure and repository-readiness validators.
- Safe `.gitignore` baseline for .NET, IDE output, local configuration and secrets.
- `PewPew.sln` and the P00-T01 source skeleton: core, API, worker/device, contracts and an Avalonia Desktop boundary.
- Root development launcher so `dotnet watch run` starts the Desktop shell without a `--project` argument.
- xUnit v3 architecture gate covering DR-001, DR-002, DR-005 and DR-011 with a negative project-reference fixture.
- GitHub Actions CI definition with least-privilege read access and Windows restore/Release-build/test workflow.

### Changed

- Aligned the high-level roadmap with the Project Scope phase baseline.
- Task specifications now trace branch point, task branch, PR target, commit scope and merge gate.
- Replaced generic angle-bracket prompt placeholders with documented `{TOKEN}` inputs and project-specific stop/output contracts.
- All 123 task rows now declare explicit mode and risk; generated task/index/Git views are guarded against unmanaged overwrite.
- Session Result status and sections are aligned across repository instructions, AI state and prompt pack.
- Planning and implementation prompts now use phase IDs `G00`–`G07` and `C00`–`C07`, resolved through a dedicated goal/constraint map.
- Recorded the accepted technical baseline: `net10.0` core, `net10.0-windows` desktop/Windows boundary, Avalonia and the `PewPew` root namespace.
- Recorded Product Owner approval of the Project Scope, Business Rules and Entity Lifecycles baselines.
- Accepted phase-integrated Git workflow policy `DEC-006`.
- Pinned the P00-T01 Desktop shell to Avalonia 12.1.0; core projects use `net10.0` and Windows boundaries use `net10.0-windows`.

### Fixed

- Corrected stale Product/Domain references and prevented proposed baselines from being reported as achieved.
- Added missing ADR files for DEC-001..005 and corrected RSK-005 to `Mitigating` while CI/architecture tests are absent.
- Corrected `DONE` task acceptance/evidence drift and task-count drift.
- Marked DEC-006-derived Git mappings as draft until Product Owner approval.

### Security

- Added AI guardrails and security checklist.
