# Next Action

## Task

`P00-T08` — thiết lập config startup validation và secret-safe baseline.

## Goal

Thiết lập baseline cấu hình fail-closed và xử lý secret an toàn, không sử dụng secret thật hoặc dữ liệu production.

## Start here

`plans/phase-00-governance/tasks/P00-T08.md`

## Read first

- `plans/phase-00-governance/tasks/P00-T08.md`
- `docs/05-quality/SECURITY_CHECKLIST.md`
- `docs/02-architecture/DEPENDENCY_RULES.md`

## Steps

1. Xác định configuration boundary và secret-safe abstraction trong scope P00.
2. Thêm validation fail-closed cùng test invalid/absent configuration.
3. Chạy secret scan, build, tests và quality gates áp dụng.
4. Đồng bộ task board, session log, working memory, next action và handoff.

## Acceptance criteria

- Invalid configuration bị từ chối fail-closed.
- Source/log không chứa secret và secret scan PASS.
- Build/tests/gates áp dụng có evidence.

## Expected evidence

- Configuration validation tests PASS.
- Secret scan PASS.
- Build/test command cùng PASS/FAIL thực tế.

## Known constraints

- `DEC-006`, `DEC-008` và `DEC-009` đã accepted; P00-T01, P00-T04 và P00-T07 đã có evidence phù hợp.
- Không dùng secret thật, production data hoặc quyền Administrator/root.
- `PewPew.sln` gồm root launcher, 10 source project và 1 architecture-test project.

## Stop condition

Dừng nếu cần secret thật, cloud provider, thay đổi public contract/architecture hoặc security exception ngoài scope.
