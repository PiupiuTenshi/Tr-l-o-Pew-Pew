# P06 — Small Task Backlog

## Task contract

Mỗi task tạo một integration outcome trong tối đa 2 ngày. Task kế thừa messaging, Home Assistant, secret, permission và external-content rules từ [phase contract](README.md). Mỗi provider phải có sandbox/fake; không dùng production secret để đạt acceptance.

| ID | Size | Status | Single outcome | Depends on | Acceptance / evidence |
|---|---:|---|---|---|---|
| [P06-T01](tasks/P06-T01.md) | 1–2d | BACKLOG | Provider scope/terms/threat/data-flow được duyệt | P05 | Capability/egress/risk matrix |
| [P06-T02](tasks/P06-T02.md) | 1–2d | BACKLOG | Credential vault reference/rotate/revoke hoạt động | P06-T01 | No-plaintext/rotation/fail-closed tests |
| [P06-T03](tasks/P06-T03.md) | 1–2d | BACKLOG | Integration authorize/health/reauth/revoke lifecycle | P06-T02 | OAuth-state/timeout/revoke tests |
| [P06-T04](tasks/P06-T04.md) | 1d | BACKLOG | Capability/data-egress manifest được policy enforce | P06-T03 | Unknown/escalated scope bị deny |
| [P06-T05](tasks/P06-T05.md) | 1–2d | BACKLOG | Message draft phân giải recipient/channel duy nhất | P06-T03 | Ambiguity/alias/no-authority tests |
| [P06-T06](tasks/P06-T06.md) | 1–2d | BACKLOG | Exact preview/confirmation/idempotent send hoạt động | P06-T05, P06-T04 | Payload-change/replay/double-send tests |
| [P06-T07](tasks/P06-T07.md) | 1d | BACKLOG | Delivery verify/unknown không auto-resend | P06-T06 | Receipt/timeout/no-success-guess tests |
| [P06-T08](tasks/P06-T08.md) | 1–2d | BACKLOG | Home Assistant connect qua safe LAN/API adapter | P06-T03 | TLS/timeout/no-token-log tests |
| [P06-T09](tasks/P06-T09.md) | 1–2d | BACKLOG | HA entity binding review/classification/allowlist | P06-T08 | Unknown/high/unsupported tests |
| [P06-T10](tasks/P06-T10.md) | 1–2d | BACKLOG | HA low-risk write có state readback | P06-T09, P06-T04 | Service/unavailable/stale tests |
| [P06-T11](tasks/P06-T11.md) | 1d | BACKLOG | Medium/high-risk HA policy chặn/confirm đúng | P06-T09, P06-T10 | Strong-confirm/voice-only deny tests |
| [P06-T12](tasks/P06-T12.md) | 1–2d | BACKLOG | Messaging/HA routine step revalidate runtime | P06-T07, P06-T11, P04 routine | Revoke/version/private-mode tests |
| [P06-T13](tasks/P06-T13.md) | 1–2d | BACKLOG | Một calendar/email adapter chỉ read/draft | P06-T04 | Contract/scope/no-send tests |
| [P06-T14](tasks/P06-T14.md) | 1–2d | BACKLOG | Internal plugin contract có restricted loading | P06-T04, P03 SkillPackage | Manifest/hash/capability tests |
| [P06-T15](tasks/P06-T15.md) | 1–2d | BACKLOG | Messaging/HA journeys được nghiệm thu | P06-T02..P06-T12 | Message + AC-10 + security evidence |

## Execution waves

1. Shared integration control: P06-T01–P06-T04.
2. Messaging: P06-T05–P06-T07.
3. Home Assistant: P06-T08–P06-T12.
4. Optional adapters và acceptance: P06-T13–P06-T15.

