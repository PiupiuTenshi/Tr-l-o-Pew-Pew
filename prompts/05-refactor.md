# Prompt — Controlled Pew Pew Assistant Refactor

## Input

- Task: `{TASK_ID}`
- Area: `{AREA}`
- Behavior contract phải giữ: `{BEHAVIOR_CONTRACT}`
- Lý do đo được: `{MEASURABLE_REASON}`

## Vai trò

Bạn đang ở mode `REFACTOR`. Chỉ thay đổi cấu trúc nội bộ của `{AREA}`; không thêm capability, không đổi business behavior và không trộn migration/dependency upgrade.

## Preconditions

- `{TASK_ID}` có scope refactor rõ và trạng thái `READY`.
- Baseline test bảo vệ `{BEHAVIOR_CONTRACT}` đã pass.
- Không có blocker/working-tree conflict liên quan.
- Refactor không bypass dependency/security boundary.
- Nếu thay module boundary, data ownership, deployment topology hoặc package nền tảng: ADR phải được accepted trước.

## Context bắt buộc

Đọc `AGENTS.md`, `RULES.md`, AI/current-phase state, task file, phase contract, business rules, lifecycle, dependency rules, ADR, architecture tests, Definition of Done và Git workflow.

Đặc biệt bảo vệ:

- Domain không phụ thuộc framework/infrastructure.
- Application không phụ thuộc concrete EF/OS/browser implementation.
- UI/API không chứa core business workflow.
- Module không truy cập bảng nội bộ của module khác.
- Permission, confirmation, audit, cancellation, timeout và idempotency giữ nguyên.

## Quy trình

1. Ghi baseline:
   - Test commands và kết quả.
   - Public contracts/observable behavior.
   - Dependency graph hoặc metric liên quan.
2. Chia refactor thành bước nhỏ, mỗi bước build/test được.
3. Thực hiện đúng `{AREA}`; không format/rewrite toàn repository.
4. Sau mỗi bước:
   - Compile/test gần.
   - So sánh public behavior.
   - Kiểm tra dependency/security invariant.
5. Chạy full gate áp dụng và so sánh metric `{MEASURABLE_REASON}`.
6. Review diff để loại bỏ accidental feature/contract change.
7. Đồng bộ execution state và handoff.

## Rollback

- Mỗi bước phải có thể revert độc lập.
- Nếu baseline test thiếu, bổ sung characterization test trong cùng scope trước refactor.
- Nếu behavior thay đổi ngoài dự kiến, dừng và rollback bước gây thay đổi; không đổi expected test.

## Output bắt buộc

- Before/after metric hoặc dependency evidence.
- Behavior contract và test bảo vệ.
- Changed files theo từng bước.
- Build/test/architecture/security results.
- Xác nhận không thêm feature/public-contract change, hoặc ghi phần lệch và chuyển `PARTIAL/BLOCKED`.
