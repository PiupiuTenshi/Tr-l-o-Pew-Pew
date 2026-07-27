# Security Checklist

- [ ] Threat boundary được xác định.
- [ ] Default deny được giữ nguyên.
- [ ] Không cấp arbitrary shell cho model.
- [ ] External content được đánh dấu untrusted.
- [ ] Input được validate và canonicalize.
- [ ] Secret lưu qua vault/OS secret store.
- [ ] Log không chứa token, password, raw private memory.
- [ ] Sensitive action có confirmation đúng scope.
- [ ] Confirmation không tái sử dụng ngoài plan đã xác nhận.
- [ ] Worker có timeout và resource limit.
- [ ] Device/integration có revoke.
- [ ] Audit record được tạo cho hành động quan trọng.
- [ ] Emergency stop vẫn hoạt động khi worker lỗi.
- [ ] Cloud upload tuân thủ privacy mode.
