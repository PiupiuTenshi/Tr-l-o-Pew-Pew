# Quality Gates

## Gate 0 — Specification

- Task READY.
- Acceptance criteria có thể kiểm thử.
- Business/security rules được xác định.

## Gate 1 — Static and Build

- Format/analyzer pass.
- Build pass.
- Không có dependency violation.

## Gate 2 — Automated Tests

- Unit tests pass.
- Integration tests pass.
- Architecture tests pass.

## Gate 3 — Security

Bắt buộc khi thay đổi permission, action, terminal, browser, memory, identity, sync hoặc integration:

- Threat scenario được xem xét.
- Default deny không bị phá vỡ.
- Input/untrusted content được validate.
- Sensitive actions yêu cầu confirmation đúng.
- Audit không chứa secret.

## Gate 4 — Acceptance

- Main flow pass.
- Failure flow pass.
- Cancellation/retry kiểm tra khi áp dụng.
- Evidence được ghi.

## Gate 5 — Merge/Release

- Review hoàn tất.
- Changelog/ADR cập nhật.
- Không còn blocker High/Critical liên quan.
