# Prompt — Prepare Pew Pew Assistant Release

## Input

- Version: `{VERSION}`
- Channel: `{CHANNEL}`
- Release task: `{TASK_ID}`
- Candidate commit/artifact: `{RELEASE_TARGET}`

Version/channel/target phải được Product Owner xác nhận. Không tự tạo version, tag hoặc release.

## Vai trò

Bạn đang ở mode `RELEASE`, chỉ áp dụng trong P07 hoặc hotfix đã được phê duyệt. Chuẩn bị và kiểm chứng release candidate; không thêm feature mới.

## Context bắt buộc

Đọc:

1. `AGENTS.md`, `RULES.md`, AI/current-phase state và blockers.
2. P07 phase contract, task file và phase Git flow.
3. `docs/07-release/RELEASE_CHECKLIST.md`, `GIT_FLOW.md`, `CHANGELOG.md`.
4. Quality Gates, Test Strategy, Security Checklist.
5. Scope AC-01–AC-12, threat model, risk register, decision/ADR.
6. Migration, installer, update, recovery, SBOM, known-issue và support docs.

## Entry gate

- P07-T01 scope/version/channel freeze được approved.
- Must-have inventory đóng; deferred item có owner/rationale.
- Candidate đến từ đúng `phase/p07-hardening-release` hoặc approved `release/{semver}`.
- Không có blocker High/Critical liên quan chưa xử lý.
- Không có secret/production data không được phép trong source/artifact/log.

## Quy trình

1. Reproduce candidate từ clean environment/pinned toolchain.
2. Chạy gate:
   - Restore/format/analyzer/build.
   - Unit/integration/architecture/E2E.
   - Permission/replay/prompt/terminal/browser/device/sync/integration security suites.
   - Resource/accessibility/compatibility/degraded-mode checks.
3. Kiểm tra data/operations:
   - Migration forward/recovery/compatibility.
   - Installer/install/uninstall.
   - Update signature/hash/channel/rollback.
   - Backup/recovery và no-auto-replay after crash.
4. Kiểm tra supply chain:
   - Dependency inventory, SBOM, license và security scan.
5. Hoàn thiện:
   - Changelog, release notes, known issues.
   - Privacy/onboarding/deployment/recovery docs.
   - Evidence index và rollback window.
6. Đề xuất `GO`, `NO-GO` hoặc `GO WITH EXPLICIT CONDITIONS`.
7. Chỉ publish/push/tag/deploy khi người dùng yêu cầu rõ và confirmation/authority phù hợp.

## Mandatory no-go

- Required gate fail hoặc không có evidence.
- Blocker Critical/High liên quan chưa đóng.
- Security bypass, secret leak, destructive migration không có recovery.
- Installer/update/rollback chưa kiểm chứng.
- Version/channel/candidate target chưa được approved.
- Release note che giấu known issue quan trọng.

## Output bắt buộc

- Version/channel/candidate identity.
- Gate matrix với command, environment, PASS/FAIL/NOT RUN và evidence.
- Security/risk/known-issue summary.
- Migration/installer/update/rollback results.
- Artifact hash/signature/SBOM status khi áp dụng.
- GO/NO-GO recommendation và approval còn thiếu.
- Không tuyên bố “released” nếu chưa có evidence từ hành động phát hành thật.
