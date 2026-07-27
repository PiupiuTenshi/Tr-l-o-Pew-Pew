# Prompt — Implement One Pew Pew Assistant Task

## Input

- Task: `P00-T04`
- Phase path: `plans/phase-00-governance`
- Optional user constraints: `C00`

Resolve `C00` từ [Goal & Constraint Map](GOAL_CONSTRAINT_MAP.md) trước khi triển khai.

## Vai trò

Bạn đang ở mode `IMPLEMENT`. Chỉ triển khai outcome của `P00-T04`; không trộn feature, refactor diện rộng, dependency major upgrade hoặc migration ngoài scope.

## Context bắt buộc

1. Đọc `AGENTS.md`, `RULES.md`, `.ai/CONTEXT.md`, current phase, next action và blockers.
2. Mở task file `plans/phase-00-governance/tasks/P00-T04.md`, phase contract, backlog và phase Git flow.
3. Đọc Business Rule IDs, entity lifecycles, dependency rules và ADR được task chỉ ra.
4. Đọc Definition of Done, Quality Gates, Test Strategy và Security Checklist áp dụng.
5. Kiểm tra repository/Git state và thay đổi chưa rõ owner.

Không bắt đầu production code nếu task không `READY`, dependency thiếu evidence hoặc stop condition đang đúng.

## Kiến trúc phải giữ

- Presentation → Contracts/Application; UI không truy cập database hoặc quyết định permission cuối cùng.
- API → Application; controller/endpoint không chứa business workflow dài.
- Application → Domain/SharedKernel và abstractions; không phụ thuộc concrete EF/OS/browser adapter.
- Domain không phụ thuộc infrastructure, persistence, UI hoặc provider.
- Infrastructure/Persistence implement abstraction; không định nghĩa business rule.
- Model output phải parse/validate/policy/risk/confirmation trước Action Engine.

## Quy trình

1. Trước khi code, nêu:
   - Task ID, goal, scope và acceptance.
   - Files tạo/sửa.
   - Business rules, lifecycle transition và dependency rules.
   - Security/data/side-effect impact.
   - Test commands, rollback và Git contract.
2. Khám phá có mục tiêu: entry point, module owner, abstraction, implementation, tests, config và composition root.
3. Thực hiện thay đổi nhỏ nhất:
   - Domain bảo vệ invariant/state transition.
   - I/O async có cancellation/timeout.
   - Retry chỉ cho operation an toàn/idempotent.
   - Side effect có permission, confirmation, verification và audit khi áp dụng.
   - Không log secret, token, raw voice/message/memory nhạy cảm.
4. Sau mỗi thay đổi có ý nghĩa: compile gần → test gần → test module → gate áp dụng.
5. Tự review correctness, architecture, security, reliability, tests và docs.
6. Đồng bộ task file, backlog, task board, session log, working memory, next action và handoff.

## Verification matrix

- Domain/rule: unit tests cho happy path, illegal transition, guard, expiry/revoke/idempotency.
- Persistence/API/adapter: integration/contract tests.
- Dependency change: architecture tests.
- Permission/action/terminal/browser/memory/sync/integration: security tests và denied path.
- Bug behavior: regression test.
- UI/automation: manual evidence trên target/reference environment.

## Stop conditions

- Cần thay public contract/architecture nhiều module ngoài task.
- Cần secret thật, production data, quyền cao hoặc external paid service.
- Confirmation/permission/audit bị bypass.
- Test fail ngoài scope không thể cô lập.
- Git working tree có thay đổi xung đột chưa rõ owner.

## Output bắt buộc

Dùng output contract trong `prompts/README.md`. Không dùng `DONE` nếu build/test/gate áp dụng chưa có evidence.
