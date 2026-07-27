# Prompt — Plan One Pew Pew Assistant Phase

## Input

- Phase: `P07`
- Phase path: `plans/phase-07-hardening-release`
- Planning goal: `G07`
- Constraints đã xác nhận: `C07`

Nếu một input làm thay đổi product scope, security boundary hoặc kiến trúc nền tảng, dừng và tạo Change Request/decision request.

Resolve `G07` và `C07` từ [Goal & Constraint Map](GOAL_CONSTRAINT_MAP.md) trước khi lập kế hoạch.

## Vai trò

Bạn đang ở mode `PLAN`. Hãy phân rã đúng một execution phase của Pew Pew Assistant thành các task nhỏ, có dependency, evidence và Git contract. Không sửa production code.

## Context bắt buộc

Đọc:

1. `AGENTS.md`, `RULES.md`, `.ai/CONTEXT.md`
2. `docs/product/PROJECT_VISION.md`
3. `docs/product/PROJECT_SCOPE.md`
4. `docs/product/BUSINESS_RULES.md`
5. `docs/domain/ENTITY_LIFECYCLES.md`
6. `docs/architecture/DEPENDENCY_RULES.md`
7. `docs/planning/ROADMAP.md`, `PHASES.md`, `CURRENT_PHASE.md`, `MILESTONES.md`
8. `plans/README.md` và phase contract `plans/phase-07-hardening-release`
9. [Goal & Constraint Map](GOAL_CONSTRAINT_MAP.md) để resolve ID trong Input
10. `docs/07-quality/DEFINITION_OF_DONE.md`, `QUALITY_GATES.md`, `TEST_STRATEGY.md`, `SECURITY_CHECKLIST.md`
11. `docs/07-release/GIT_FLOW.md`

## Project constraints

- Không đưa capability phase sau vào phase trước để “làm tiện”.
- P07 security/domain precedes local assistant actions.
- Browser/terminal/device write cần worker isolation, timeout, cancellation, policy, confirmation và audit.
- P07 phải chốt local memory/delete/tombstone/routine trust trước P07 cloud sync.
- Integration cần scoped permission, credential isolation, revoke và failure isolation.
- P07 không thêm feature; chỉ hardening, acceptance, recovery và release.
- Level 3 actions mặc định bị chặn trong MVP.

## Quy trình

1. Reconcile `P07` với scope baseline và phase dependency.
2. Viết/cập nhật phase contract:
   - Goal, non-goals, entry criteria.
   - Deliverables và demo journey.
   - Business/lifecycle/dependency traceability.
   - Security review level.
   - Acceptance, quality gates, risks, recovery và exit evidence.
3. Chia task:
   - Một task = một outcome.
   - Size mục tiêu `0.5d–2d`.
   - Có priority, status, dependency và evidence kiểm chứng được.
   - Task side effect/rủi ro cao tách policy/domain/adapter/test.
4. Tạo/cập nhật:
   - `plans/phase-07-hardening-release/README.md`
   - `plans/phase-07-hardening-release/TASKS.md`
   - `plans/phase-07-hardening-release/tasks/Pxx-Tyy.md`
   - `plans/phase-07-hardening-release/tasks/README.md`
   - `plans/phase-07-hardening-release/GIT_FLOW.md`
5. Cập nhật task board và execution state.
6. Chạy planning/task/Git-link validators áp dụng.

## Acceptance criteria

- Phase không mở rộng Project Scope.
- Must task bao phủ toàn bộ phase acceptance và failure/security paths.
- Task size, ID, dependency, status và evidence hợp lệ.
- Phase gate chặn capability có quyền cao khi prerequisite chưa pass.
- README, backlog, individual task files và Git flow không drift.
- Có next action thực thi được.

## Output bắt buộc

- Phase summary và dependency graph.
- Files thay đổi.
- Số task theo status/priority.
- Validator commands và PASS/FAIL.
- Open decisions/blockers.
- Next READY task; không ghi “tiếp tục phase” chung chung.
