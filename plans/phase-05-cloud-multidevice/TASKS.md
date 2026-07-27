# P05 — Small Task Backlog

## Task contract

Mỗi task tạo một cloud/multi-device outcome trong tối đa 2 ngày. Task kế thừa identity, device, mode, sync và security rules từ [phase contract](README.md). Cloud chỉ điều phối signed command; Device Agent vẫn re-authorize và thực thi.

| ID | Size | Status | Single outcome | Depends on | Acceptance / evidence |
|---|---:|---|---|---|---|
| [P05-T01](tasks/P05-T01.md) | 1–2d | BACKLOG | Cloud/multi-device specs và data-flow threats được duyệt | P04 | Trust/data diagram + egress inventory |
| [P05-T02](tasks/P05-T02.md) | 1–2d | BACKLOG | API auth/error/rate-limit boundary fail closed | P05-T01, P01 identity | Auth/expiry/revoke/abuse tests |
| [P05-T03](tasks/P05-T03.md) | 1–2d | BACKLOG | Cloud persistence/migration/outbox atomicity hoạt động | P05-T02 | Forward/rollback/replay integration tests |
| [P05-T04](tasks/P05-T04.md) | 1–2d | BACKLOG | Pairing challenge/device key chống replay | P05-T02, P01 DeviceNode | Pair/expire/replay/rekey tests |
| [P05-T05](tasks/P05-T05.md) | 1–2d | BACKLOG | Signed device command validate target/capability | P05-T04, P03 Action Engine | Wrong-device/offline/stale/replay tests |
| [P05-T06](tasks/P05-T06.md) | 1–2d | BACKLOG | Revoke cascade tới session/grant/task/sync | P05-T04, P05-T05 | Immediate revoke/race tests |
| [P05-T07](tasks/P05-T07.md) | 1–2d | BACKLOG | Cloud provider config/adapter giữ secret boundary | P05-T01 | Config/timeout/rate/no-secret tests |
| [P05-T08](tasks/P05-T08.md) | 1–2d | BACKLOG | Connected/Auto routing có disclosure và fallback đúng | P05-T07, P02 mode baseline | Sensitive/redaction/private/fallback tests |
| [P05-T09](tasks/P05-T09.md) | 1–2d | BACKLOG | MemorySyncJob opt-in/encrypted/idempotent | P05-T03, P04 memory | Private/offline/retry/duplicate tests |
| [P05-T10](tasks/P05-T10.md) | 1–2d | BACKLOG | Tombstone/conflict merge ưu tiên deletion/user edit | P05-T09 | Out-of-order/resurrection/conflict tests |
| [P05-T11](tasks/P05-T11.md) | 1–2d | BACKLOG | Cross-device context handoff có TTL/scope | P05-T05, P05-T09 | Stale-device/no-local-only-leak tests |
| [P05-T12](tasks/P05-T12.md) | 1–2d | BACKLOG | Web Console quản lý device/privacy/permission/audit | P05-T02, P05-T06, P05-T09 | Authorization/accessibility tests |
| [P05-T13](tasks/P05-T13.md) | 1–2d | BACKLOG | Web memory/routine management giữ ownership/version | P05-T10, P05-T12 | CRUD/version/conflict tests |
| [P05-T14](tasks/P05-T14.md) | 1–2d | BACKLOG | Android companion command/status/confirm/stop | P05-T05, P05-T06 | Trusted-session/remote-confirm/stop tests |
| [P05-T15](tasks/P05-T15.md) | 1–2d | BACKLOG | Cross-device/cloud journeys được nghiệm thu | P05-T02..P05-T14 | AC-03/06/09/12 evidence |

## Execution waves

1. API/data/device trust: P05-T01–P05-T06.
2. Provider/routing/sync: P05-T07–P05-T11.
3. Clients/acceptance: P05-T12–P05-T15.

