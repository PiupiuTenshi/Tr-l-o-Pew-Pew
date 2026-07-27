# ADR-003 — Local–Cloud Hybrid AI routing

- Status: Accepted
- Decision: `DEC-003`
- Date: 2026-07-27
- Owners: Product Owner, Architecture, Security

## Context

Trợ lý cần privacy/offline capability nhưng một số tác vụ cần năng lực model cloud. Routing ảnh hưởng dữ liệu nhạy cảm, latency, cost và availability.

## Decision

AI Router chọn local hoặc cloud dựa trên user mode, sensitivity, network, capability, latency, cost và resource budget. Private Mode cấm cloud AI, cloud sync và upload audio/screenshot; không silent fallback.

Trước cloud disclosure phải xác định provider, dữ liệu, mục đích và policy. Dữ liệu phải được giảm thiểu/redact khi áp dụng.

## Alternatives

- Cloud-only: từ chối vì không đáp ứng privacy/offline.
- Local-only cho mọi tác vụ: từ chối vì không đáp ứng capability roadmap.
- Silent auto-fallback: từ chối vì làm mất user control.

## Consequences

- Routing decision cần policy, audit và deterministic tests.
- Provider adapter không được trực tiếp thực thi action.
- UI phải hiển thị mode và nơi dữ liệu được xử lý.

## Revisit

Revisit khi model/runtime capability hoặc privacy policy thay đổi qua decision được phê duyệt.
