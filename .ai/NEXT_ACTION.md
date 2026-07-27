# Next Action

## Task

`P00-T03` — xác minh GitHub Actions CI sau khi commit/push được Product Owner cho phép.

## Goal

Lấy evidence GitHub Actions cho workflow restore → Release build → test đã được tạo; chỉ thực hiện commit/push khi có authorization rõ ràng.

## Start here

`plans/phase-00-governance/tasks/P00-T03.md`

## Read first

- `plans/phase-00-governance/tasks/P00-T03.md`
- `PewPew.sln`
- `tests/PewPew.Architecture.Tests/`

## Steps

1. Xác nhận Product Owner cho phép tạo commit/task branch và push lên `origin`.
2. Đẩy workflow cùng source hiện tại theo Git contract.
3. Quan sát GitHub Actions `CI` run và lưu URL/status PASS.
4. Chỉ sau evidence đó mới chuyển P00-T03 thành `DONE`.

## Acceptance criteria

- Workflow chứa restore, Release build và test steps; local equivalent PASS.
- Simulated failure trả exit 1.
- GitHub Actions clean run còn pending authorization.

## Expected evidence

- `scripts/Test-CiWorkflow.ps1` PASS.
- `scripts/Invoke-Ci.ps1` PASS; `-SimulateFailure` exit 1.
- GitHub Actions run URL/status sau authorization.

## Known constraints

- `DEC-006`, `DEC-008` và `DEC-009` đã accepted; P00-T01 và P00-T02 đã có Release build/test evidence.
- Git local có nhánh `main` nhưng chưa có commit; không tự commit/push.
- `PewPew.sln` gồm root launcher, 10 source project và 1 architecture-test project.

## Stop condition

Dừng nếu CI cần secret, deployment, external paid service, thay đổi branch protection hoặc security exception; không push khi chưa có authorization.
