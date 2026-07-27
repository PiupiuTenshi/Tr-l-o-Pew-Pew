# Prompt — Debug and Fix One Pew Pew Assistant Bug

## Input

- Bug: `{BUG_ID}`
- Related task: `{TASK_ID}`
- Reported behavior: `{OBSERVED_BEHAVIOR}`
- Expected behavior/source: `{EXPECTED_BEHAVIOR}`

## Vai trò

Bạn đang ở mode `DEBUG`. Tuân thủ:

```text
Reproduce → Observe → Form hypothesis → Isolate → Regression test → Fix root cause → Verify
```

Không sửa theo cảm tính, không đổi test để hợp thức hóa behavior sai và không mở rộng thành refactor.

## Context bắt buộc

Đọc:

- `AGENTS.md`, `RULES.md`, AI/current-phase state.
- Bug report và task file liên quan.
- Business Rule IDs, entity lifecycle, dependency rules và ADR.
- `docs/05-quality/DEFINITION_OF_DONE.md`, `QUALITY_GATES.md`, `TEST_STRATEGY.md`, `SECURITY_CHECKLIST.md`.
- Phase/task Git workflow.

## Quy trình

1. Chuẩn hóa reproduction:
   - Environment/config không chứa secret.
   - Input tối thiểu.
   - Expected/actual result.
   - Logs/trace đã redact.
2. Tái hiện trước khi sửa. Nếu không tái hiện được:
   - Ghi command/evidence.
   - Thu hẹp điều kiện còn thiếu.
   - Không tuyên bố nguyên nhân hoặc fix.
3. Khoanh vùng boundary:
   - Domain invariant/state transition.
   - Application orchestration/policy.
   - Persistence/concurrency/outbox.
   - Adapter/provider/OS/browser/worker.
   - UI/API mapping.
4. Đánh giá security impact:
   - Permission/confirmation replay hoặc bypass.
   - Prompt/untrusted input.
   - Secret/cloud disclosure.
   - Arbitrary shell/path/worker escape.
   - Data loss/tombstone resurrection.
5. Viết failing regression test khi kỹ thuật cho phép.
6. Sửa root cause bằng thay đổi nhỏ nhất.
7. Chạy:
   - Regression test.
   - Test lân cận/module.
   - Architecture/security/integration gate áp dụng.
8. Cập nhật bug report, task/execution state và changelog nếu behavior người dùng thay đổi.

## Stop conditions

- Không tái hiện và không có evidence đủ để isolate.
- Expected behavior mâu thuẫn source of truth.
- Fix cần nới permission, validation, TLS, confirmation hoặc audit.
- Cần destructive migration, production data, secret hoặc quyền cao chưa được duyệt.
- Root cause nằm ngoài task và thay đổi public contract/architecture đáng kể.

## Output bắt buộc

- Reproduction: PASS/FAIL và evidence.
- Root cause: boundary, trigger và rule bị vi phạm.
- Fix: changed files và vì sao là thay đổi nhỏ nhất.
- Regression/gates: command + PASS/FAIL.
- Security/data impact.
- Acceptance checklist, phần chưa hoàn tất và next action.
