# Prompt — Review Pew Pew Assistant Change

## Input

- Task: `{TASK_ID}`
- Review target: `{REVIEW_TARGET}`
- Optional review focus: `{FOCUS}`

## Vai trò

Bạn đang ở mode `REVIEW`. Review correctness và risk của thay đổi thuộc `{TASK_ID}`. Mặc định chỉ báo cáo finding; không sửa file, merge, resolve thread hoặc đổi task status nếu chưa được yêu cầu.

## Context bắt buộc

Đọc `AGENTS.md`, `RULES.md`, AI/current-phase state, task file, phase contract/backlog/Git flow, acceptance criteria, business rules, entity lifecycles, dependency rules, ADR, Definition of Done và security checklist liên quan.

Xác định giới hạn review:

- Có xem đủ diff/file không?
- Có test/evidence không?
- Có generated file hoặc thay đổi ngoài task không?
- Repository có Git metadata để xác định chính xác diff không?

## Thứ tự review

1. Security/privilege boundary:
   - Model → Policy → domain → Action Engine → verification → audit.
   - Permission scope/revoke, confirmation binding/replay/expiry.
   - External content/prompt injection.
   - Secret/PII logging, cloud disclosure và Private Mode.
2. Correctness/business:
   - Rule ID, aggregate invariant, illegal transition, single-use semantics.
   - Failure/cancellation/timeout/retry/idempotency.
3. Architecture:
   - Direction giữa Presentation/API/Application/Domain/Infrastructure/Persistence.
   - Module ownership, cross-table access, adapter/composition root.
4. Data/reliability:
   - Migration/recovery, tombstone/delete, concurrency/outbox.
   - Resource bounds, process tree cleanup, worker isolation.
5. Tests/evidence:
   - Acceptance behavior, denied/failure path, regression/architecture/security coverage.
6. Maintainability:
   - Naming, duplication, complexity; chỉ sau correctness/risk.

## Severity

- `Critical`: có thể gây compromise, destructive action, secret exposure hoặc bypass control nghiêm trọng.
- `High`: sai business/security boundary hoặc data loss đáng kể.
- `Medium`: behavior/reliability lỗi trong tình huống thực tế.
- `Low`: maintainability hoặc edge case có tác động giới hạn.

## Finding format

```md
### [Severity] Tiêu đề

- Location: `path:line`
- Rule/criterion:
- Problem:
- Failure scenario:
- Impact:
- Minimal remediation:
- Missing evidence/test:
```

Không báo finding mơ hồ, không suy đoán file/line và không tập trung style khi còn lỗi quan trọng hơn.

## Output bắt buộc

1. Findings theo severity.
2. Open questions/assumptions.
3. Phần đã kiểm tra và giới hạn review.
4. Acceptance/gate status.

Nếu không có finding, nói rõ “không phát hiện finding trong phạm vi đã review”, không tuyên bố hệ thống an toàn tuyệt đối.
