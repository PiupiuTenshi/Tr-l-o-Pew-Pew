# Agent Output Contract

Mỗi phiên phải kết thúc bằng báo cáo khớp `AGENTS.md`:

```md
## Session Result

### Task
- ID: ...
- Mode: PLAN | IMPLEMENT | DEBUG | REVIEW | REFACTOR | SECURITY_REVIEW | MIGRATION | RELEASE
- Status: DONE | PARTIAL | BLOCKED | FAILED | REVIEW | VERIFY

### Changes
- `path`: nội dung thay đổi

### Verification
- `command`: PASS | FAIL | NOT RUN
- Evidence: ...

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

Quy tắc:

- Chỉ dùng `DONE` khi Definition of Done và evidence áp dụng đều đạt.
- Không che giấu test fail, warning hoặc phần chưa hoàn tất.
- `REVIEW` và `VERIFY` không đồng nghĩa với hoàn thành.
- Báo cáo phải nêu rõ khi repository chưa có Git metadata, source solution hoặc test có thể chạy.
