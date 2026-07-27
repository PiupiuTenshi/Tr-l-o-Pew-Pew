# Phase Governance

## Điều kiện mở phase

Một phase chỉ được chuyển sang `ACTIVE` khi:

- Mục tiêu và phạm vi được xác định.
- Deliverables có owner hoặc agent responsible.
- Acceptance criteria có thể kiểm thử.
- Dependency và blocker chính đã được nhận diện.
- Security review level được xác định.

## Điều kiện đóng phase

- Tất cả Must-have task hoàn thành.
- Build và test pass.
- Không còn blocker Critical/High chưa xử lý.
- Tài liệu, decision và changelog được cập nhật.
- Demo hoặc evidence được lưu.
- Retrospective hoàn thành.

## Trạng thái

```text
DRAFT → READY → ACTIVE → VERIFYING → COMPLETED
                    ↘ PAUSED / BLOCKED ↗
```
