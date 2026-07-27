# Vibe Coding Management Structure

## 1. Mục tiêu

Cấu trúc này biến quá trình “nói yêu cầu rồi để AI code” thành một quy trình kỹ thuật có trạng thái, giới hạn, kiểm thử và bàn giao. Nó giảm bốn rủi ro phổ biến: mất ngữ cảnh, scope creep, AI tự suy đoán và code phá kiến trúc.

## 2. Bốn lớp quản lý

### Lớp 1 — Product truth

Vision, scope và business rules trả lời: xây cái gì, cho ai, giới hạn nào và hành vi nào bắt buộc.

### Lớp 2 — Engineering truth

Entity lifecycle, dependency rules, module map và ADR trả lời: hệ thống được tổ chức như thế nào và điều gì bị cấm.

### Lớp 3 — Execution truth

Roadmap, current phase, task board, feature spec và test criteria trả lời: hiện tại đang làm gì và khi nào được coi là xong.

### Lớp 4 — AI continuity

Context, working memory, blockers, next action và session log trả lời: agent tiếp theo cần biết gì để tiếp tục chính xác.

## 3. Đơn vị công việc

```text
Epic → Feature Specification → Task → Commit/PR → Evidence
```

Task tốt phải:

- Có một outcome chính.
- Có boundary rõ.
- Có acceptance criteria.
- Có rules tham chiếu.
- Có test plan.
- Có rollback hoặc failure handling.
- Có thể hoàn thành và kiểm chứng trong một khoảng ngắn.

## 4. Protocol cho mỗi phiên AI

### Bước A — Load context

Chỉ đọc tài liệu liên quan theo thứ tự trong `AGENTS.md`.

### Bước B — Contract

AI nêu task, plan, files, rules và test trước khi sửa.

### Bước C — Execute

Thực hiện thay đổi nhỏ, không mở rộng scope và không thay đổi business rule.

### Bước D — Verify

Chạy các quality gate áp dụng và lưu evidence.

### Bước E — Persist state

Cập nhật board, log, memory, blocker và next action.

## 5. Quy tắc chống context drift

- `WORKING_MEMORY.md` chỉ chứa tóm tắt ngắn, không phải lịch sử đầy đủ.
- Quyết định dài hạn phải vào ADR hoặc decision log.
- Kết quả phiên phải vào session log.
- Việc tiếp theo phải vào next action.
- Không dựa vào trí nhớ hội thoại của một AI cụ thể.

## 6. Quy tắc chống scope creep

Mọi yêu cầu mới được phân loại:

```text
Trong acceptance criteria hiện tại → làm trong task.
Cần thiết để build/test task → ghi rõ và làm tối thiểu.
Feature mới hoặc thay behavior → tạo Change Request / task mới.
Thay kiến trúc hoặc dependency → tạo ADR.
```

## 7. Quy tắc dùng AI hiệu quả

- Không giao cả phase trong một prompt.
- Không yêu cầu “làm toàn bộ dự án”.
- Cung cấp task ID và file nguồn sự thật.
- Yêu cầu AI nêu assumptions trước khi code.
- Yêu cầu test/evidence cụ thể.
- Tách implement và review thành hai lượt hoặc hai agent khi có thể.
- Sau mỗi phiên phải handoff.

## 8. Mô hình agent khuyến nghị

| Agent | Trách nhiệm | Không được làm |
|---|---|---|
| Product/Analyst | Spec, flow, criteria | Sửa code production |
| Architect | Boundary, ADR, dependencies | Tự thay business scope |
| Implementer | Code một task | Tự mở rộng task |
| Reviewer | Tìm lỗi và rule violation | Sửa lớn không có task |
| Security Reviewer | Threat and controls | Bỏ qua usability/business |
| Test Agent | Test and evidence | Tuyên bố behavior không có test |
| Release Agent | Gate and packaging | Phát hành khi gate fail |

Một agent có thể đảm nhận nhiều vai trò ở dự án cá nhân, nhưng từng phiên phải tuyên bố vai trò hiện tại.

## 9. Chỉ số quản lý

- Tỷ lệ task có acceptance criteria trước code.
- Tỷ lệ task pass lần đầu.
- Số dependency violation.
- Số scope change không qua request.
- Số blocker do thiếu quyết định.
- Thời gian để agent mới tiếp tục từ handoff.
- Tỷ lệ claim hoàn thành có evidence.
- Số regression sau merge.

## 10. Definition of Ready

Một task READY khi:

- Outcome rõ.
- In/out scope rõ.
- Dependency có sẵn.
- Rule tham chiếu đầy đủ.
- Acceptance criteria testable.
- Không có blocker bắt buộc.
- Security level xác định.

## 11. Definition of Done

Xem `docs/05-quality/DEFINITION_OF_DONE.md`. Trạng thái DONE là kết quả của evidence, không phải cảm nhận của AI.
