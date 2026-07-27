# Pew Pew Assistant — Engineering Prompt Pack

## Mục đích

Thư mục này chứa prompt vận hành dành riêng cho engineering agent làm việc trong repository Pew Pew Assistant. Prompt không thay thế source of truth trong `docs/`, `AGENTS.md`, task specification hoặc quyết định đã được phê duyệt.

## Baseline của dự án

- Sản phẩm: trợ lý AI cá nhân 1:1, Windows-first, đa thiết bị.
- Kiến trúc: 3-tier ở cấp triển khai; Clean Architecture nội bộ; Modular Monolith cho MVP.
- AI routing: Local–Cloud Hybrid; Private Mode không được silently fallback lên cloud.
- Security: default deny, least privilege, external content không đáng tin cậy.
- Action path bắt buộc: Model proposal → schema validation → Policy Engine → domain transition → Action Engine → verification → audit.
- Model không được nối trực tiếp tới shell, database, browser/device control hoặc message send.
- Mỗi task có một outcome, acceptance criteria, evidence và Git workflow contract.
- Git workflow đang được đề xuất trong `DEC-006`: phase integration branch, task branch ngắn hạn và phase gate PR về `main`. Không áp dụng cho đến khi decision được `Accepted`.
- Target framework, desktop UI và root namespace phải theo decision đã accepted; không suy đoán khi blocker còn mở.

## Cách chọn prompt

| Tình huống | Prompt |
|---|---|
| Bắt đầu phiên từ trạng thái repository | [00-bootstrap.md](00-bootstrap.md) |
| Chia hoặc cập nhật một phase | [01-plan-phase.md](01-plan-phase.md) |
| Triển khai một task READY | [02-implement-task.md](02-implement-task.md) |
| Review code hoặc tài liệu của task | [03-review-code.md](03-review-code.md) |
| Tái hiện và sửa bug | [04-fix-bug.md](04-fix-bug.md) |
| Refactor có kiểm soát | [05-refactor.md](05-refactor.md) |
| Security review/threat review | [06-security-review.md](06-security-review.md) |
| Kết thúc phiên và bàn giao | [07-handoff.md](07-handoff.md) |
| Chuẩn bị release candidate/release | [08-release.md](08-release.md) |

## Cách dùng biến

Thay các token dạng `{TOKEN}` trước khi chạy prompt:

- `{PHASE_ID}`: ví dụ `P03`.
- `{PHASE_PATH}`: ví dụ `plans/phase-03-device-automation`.
- `{TASK_ID}`: ví dụ `P03-T12`.
- `{BUG_ID}`: ID trong bug report/task board.
- `{AREA}`: module hoặc boundary cụ thể.
- `{REVIEW_TARGET}`: file, commit, diff hoặc PR được review.
- `{VERSION}`: version đã được Product Owner phê duyệt.
- `{CHANNEL}`: internal, private-beta, stable hoặc channel đã được duyệt.

Không để agent tự đoán token có ảnh hưởng scope, target, quyền, dữ liệu, version hoặc release channel.

## Goal & Constraint IDs

[Goal & Constraint Map](GOAL_CONSTRAINT_MAP.md) chứa ID cho toàn bộ phase: `G00`–`G07` và `C00`–`C07`. Khi dùng prompt, ghi cặp ID đúng phase (ví dụ P03 dùng `G03`/`C03`) và để agent resolve nội dung từ map trước khi làm việc. ID không thay thế Product Owner approval hoặc source-of-truth docs.

## Context bắt buộc cho mọi prompt

Đọc tối thiểu theo thứ tự:

1. `AGENTS.md`
2. `RULES.md`
3. `.ai/CONTEXT.md`
4. `docs/03-planning/CURRENT_PHASE.md`
5. `.ai/NEXT_ACTION.md`
6. `.ai/BLOCKERS.md`
7. Task file tại `{PHASE_PATH}/tasks/{TASK_ID}.md`
8. `docs/05-quality/DEFINITION_OF_DONE.md`
9. Source-of-truth bổ sung được task file chỉ ra

Trước khi sửa:

- Xác định repository root, Git state và thay đổi chưa rõ owner.
- Xác định Task ID, mode, status, dependency, risk và stop condition.
- Nêu execution plan, file dự kiến thay đổi và verification plan.
- Dừng nếu cần decision, secret, production data, quyền cao hoặc scope expansion chưa được duyệt.

Sau khi sửa:

- Chạy gate phù hợp và ghi PASS/FAIL thực tế.
- Không che giấu warning, test fail hoặc phần chưa hoàn tất.
- Đồng bộ task file, `TASKS.md`, task board, session log, working memory, next action và handoff.
- Tuân thủ branch/PR contract trong task file và `docs/07-release/GIT_FLOW.md`; không push/merge/release nếu chưa được yêu cầu.

## Output contract chung

```md
## Session Result

### Task
- ID:
- Mode:
- Status: DONE | PARTIAL | BLOCKED | FAILED | REVIEW | VERIFY

### Changes
- `path`: outcome

### Verification
- `command`: PASS | FAIL | NOT RUN
- Evidence:

### Acceptance Criteria
- [x] ...
- [ ] ... — lý do

### Risks / Debt
- ...

### Documentation Updated
- ...

### Next Action
- ...
```

Chỉ dùng `DONE` khi Definition of Done và evidence áp dụng đều đạt.
