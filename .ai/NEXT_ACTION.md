# Next Action

## Task

`P00-T04` — chốt coding/analyzer/warning conventions.

## Goal

Thiết lập convention có thể thực thi cho code style, analyzers và policy warnings, không mở rộng product scope hoặc thay đổi kiến trúc.

## Start here

`plans/phase-00-governance/tasks/P00-T04.md`

## Read first

- `plans/phase-00-governance/tasks/P00-T04.md`
- `docs/05-quality/QUALITY_GATES.md`
- `docs/02-architecture/DEPENDENCY_RULES.md`

## Steps

1. Reconcile coding/analyzer/warning convention với `DEC-008` và quality gates.
2. Chỉ thêm configuration/documentation cần thiết trong scope task.
3. Chạy analyzer/build evidence phù hợp; không tắt warning hoặc gate để vượt lỗi.
4. Đồng bộ task board, session log, working memory và handoff.

## Acceptance criteria

- Convention có owner và scope rõ.
- Analyzer/build evidence được lưu hoặc N/A có lý do.
- Không có suppression/security exception ngoài scope.

## Expected evidence

- Configuration/documentation diff rõ ràng.
- Analyzer/build command cùng PASS/FAIL thực tế.
- Task board và session evidence.

## Known constraints

- `DEC-006`, `DEC-008` và `DEC-009` đã accepted; P00-T01 đến P00-T03 đã có evidence.
- Git baseline `77401bc` đã push lên `origin/main`; P00 phase/task branch đã tồn tại.
- `PewPew.sln` gồm root launcher, 10 source project và 1 architecture-test project.

## Stop condition

Dừng nếu cần nới warning/security policy, thay đổi public contract/architecture, secret thật hoặc package/license chưa được duyệt.
