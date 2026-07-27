# Prompt — Handoff Pew Pew Assistant Session

## Input

- Task: `{TASK_ID}`
- Phase path: `{PHASE_PATH}`
- Actual status: `{STATUS}`
- Intended next task nếu đã xác định: `{NEXT_TASK_ID}`

## Vai trò

Kết thúc phiên hiện tại và để lại repository state đủ rõ để developer/agent khác tiếp tục mà không suy đoán. Không dùng handoff để che giấu test fail hoặc tự chuyển task thành `DONE`.

## Context bắt buộc

Đọc `AGENTS.md`, `RULES.md`, task file, phase backlog, task board, current phase, next action, blockers, working memory, session log, decision/risk/changelog liên quan và Git contract.

## Quy trình

1. Reconcile actual state:
   - Files thực sự thay đổi.
   - Commands đã chạy và PASS/FAIL/NOT RUN.
   - Acceptance criteria đạt/chưa đạt.
   - Review/blocker/decision còn mở.
   - Git branch/PR/commit thực tế nếu tồn tại.
2. Chọn status đúng:
   - `DONE`: toàn bộ DoD/evidence áp dụng đạt.
   - `REVIEW`: implementation/docs sẵn sàng review nhưng chưa approved.
   - `VERIFY`: chờ acceptance/gate.
   - `PARTIAL`: có output hữu ích nhưng task chưa đạt.
   - `BLOCKED`: stop condition cần external decision/state.
   - `FAILED`: attempt không tạo kết quả dùng được.
3. Đồng bộ tối thiểu:
   - Task file và `{PHASE_PATH}/TASKS.md`.
   - `docs/04-execution/TASK_BOARD.md`.
   - `docs/04-execution/SESSION_LOG.md`.
   - `.ai/WORKING_MEMORY.md`.
   - `.ai/NEXT_ACTION.md`.
   - `docs/04-execution/HANDOFF.md`.
4. Khi áp dụng:
   - `.ai/BLOCKERS.md`.
   - `docs/06-decisions/DECISION_LOG.md` hoặc ADR.
   - `docs/06-decisions/RISK_REGISTER.md`.
   - `docs/07-release/CHANGELOG.md`.
5. Validate status/link/task counts sau cập nhật.

## NEXT_ACTION contract

Phải có:

- Task ID và goal.
- Start file.
- Read-first list.
- Các bước cụ thể.
- Acceptance/evidence.
- Known constraints.
- Stop condition.

Nếu next task cần Product Owner approval, ghi rõ nội dung phải approve; không xem im lặng là đồng ý.

## Output bắt buộc

Dùng `Session Result` trong `prompts/README.md`, kèm:

- Exact changed files.
- Verification evidence.
- Phần chưa hoàn tất.
- Branch/PR state hoặc ghi rõ repository chưa có Git metadata.
- Một next action duy nhất, có thể thực thi.
