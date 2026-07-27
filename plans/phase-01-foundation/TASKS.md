# P01 — Small Task Backlog

## Task contract

Mỗi task có một domain/control outcome và tối đa 2 ngày. Tất cả task kế thừa `BR-GOV-*`, `BR-ID-*`, `BR-DEV-*`, `BR-AI-*`, `BR-ACT-*`, `BR-AUD-*` cùng `DR-001`–`DR-030` áp dụng từ [phase contract](README.md). Không task nào được nối model trực tiếp tới adapter thực thi.

| ID | Size | Status | Single outcome | Depends on | Acceptance / evidence |
|---|---:|---|---|---|---|
| [P01-T01](tasks/P01-T01.md) | 1–2d | BACKLOG | Core control-path specs và threats được chấp thuận | P00 | Main/denied/cancel/unknown flows có traceability |
| [P01-T02](tasks/P01-T02.md) | 1d | BACKLOG | SharedKernel chỉ chứa ID/clock/result/event primitives | P01-T01 | Unit + architecture tests; không có feature logic |
| [P01-T03](tasks/P01-T03.md) | 1–2d | BACKLOG | `UserAccount`/`AssistantProfile` lifecycle được bảo vệ | P01-T02 | Happy, illegal, terminal, Safe Mode tests |
| [P01-T04](tasks/P01-T04.md) | 1–2d | BACKLOG | `DeviceNode`/`DeviceSession` trust lifecycle hoạt động | P01-T02, P01-T03 | Pair/revoke/replay/session-expiry tests |
| [P01-T05](tasks/P01-T05.md) | 1–2d | BACKLOG | Interaction/clarification giữ context đúng TTL | P01-T02 | Ambiguity, answer, cancel, expire tests |
| [P01-T06](tasks/P01-T06.md) | 1d | BACKLOG | Structured action contract và immutable plan hash tồn tại | P01-T05 | Valid/invalid schema + version/hash tests |
| [P01-T07](tasks/P01-T07.md) | 1–2d | BACKLOG | `ActionPlan` chỉ đi qua policy review hợp lệ | P01-T06 | Approve/reject/supersede/expire tests |
| [P01-T08](tasks/P01-T08.md) | 1–2d | BACKLOG | `ActionTask` canonical state machine hoạt động | P01-T07 | Complete/fail/cancel/unknown/reconcile tests |
| [P01-T09](tasks/P01-T09.md) | 1–2d | BACKLOG | Permission matching deny-by-default đúng scope | P01-T03, P01-T04 | Scope/expiry/revoke/wildcard-deny tests |
| [P01-T10](tasks/P01-T10.md) | 1–2d | BACKLOG | Confirmation gắn plan/payload/session và dùng một lần | P01-T07, P01-T09 | Replay/expiry/change/revoke tests |
| [P01-T11](tasks/P01-T11.md) | 1d | BACKLOG | Audit append-only có redaction contract | P01-T02 | Seal/immutability/no-secret tests |
| [P01-T12](tasks/P01-T12.md) | 1–2d | BACKLOG | SQLite persistence/outbox giữ concurrency và atomicity | P01-T03..P01-T11 | Migration, version conflict, outbox replay tests |
| [P01-T13](tasks/P01-T13.md) | 1–2d | BACKLOG | Fake vertical slice chạy qua toàn control path | P01-T08..P01-T12 | Denied + confirmed + verified E2E pass |
| [P01-T14](tasks/P01-T14.md) | 1d | BACKLOG | Safe Mode/Emergency Stop chặn action mới | P01-T03, P01-T08, P01-T11 | Block/cancel/no-auto-resume/audit tests |
| [P01-T15](tasks/P01-T15.md) | 1d | BACKLOG | M1 security/phase evidence được chốt | P01-T13, P01-T14 | Findings resolved/deferred; phase checklist pass |

## Execution waves

1. Specification/kernel: P01-T01, P01-T02.
2. Identity/context: P01-T03–P01-T06.
3. Policy/action: P01-T07–P01-T10.
4. Audit/data: P01-T11, P01-T12.
5. Vertical proof: P01-T13–P01-T15.

