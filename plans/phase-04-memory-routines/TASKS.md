# P04 — Small Task Backlog

## Task contract

Mỗi task tạo một outcome memory hoặc routine trong tối đa 2 ngày. Task kế thừa `BR-MEM-*`, `BR-RTN-*`, lifecycle và no-authority boundary từ [phase contract](README.md). Không task nào được dùng memory để cấp quyền hoặc kích hoạt routine chưa duyệt.

| ID | Size | Status | Single outcome | Depends on | Acceptance / evidence |
|---|---:|---|---|---|---|
| [P04-T01](tasks/P04-T01.md) | 1–2d | BACKLOG | Memory/routine classification và schema được duyệt | P03 | Data types, retention, risk, flows có AC |
| [P04-T02](tasks/P04-T02.md) | 1–2d | BACKLOG | Working Memory bounded theo session/topic TTL | P04-T01 | TTL/isolation/reset/no-authority tests |
| [P04-T03](tasks/P04-T03.md) | 1–2d | BACKLOG | `MemoryRecord` lifecycle bảo vệ metadata/invariants | P04-T01 | Candidate/stale/expire/revoke/delete tests |
| [P04-T04](tasks/P04-T04.md) | 1–2d | BACKLOG | Local store mã hóa và áp quota | P04-T03 | At-rest/quota/key-failure tests |
| [P04-T05](tasks/P04-T05.md) | 1–2d | BACKLOG | Retrieval bounded, lọc state và giải thích source | P04-T04 | Budget/stale-filter/no-full-load tests |
| [P04-T06](tasks/P04-T06.md) | 1–2d | BACKLOG | User review/edit/pin/export/delete memory được | P04-T04, P04-T05 | CRUD/ownership/accessibility tests |
| [P04-T07](tasks/P04-T07.md) | 1–2d | BACKLOG | Retention/cleanup/tombstone không hồi sinh dữ liệu | P04-T03, P04-T04 | Expiry/pinned/delete/resurrection tests |
| [P04-T08](tasks/P04-T08.md) | 1–2d | BACKLOG | Routine validate/version/approval lifecycle hoạt động | P04-T01, P03 skill/workflow | Hash/trust/supersede tests |
| [P04-T09](tasks/P04-T09.md) | 1–2d | BACKLOG | Routine editor preview exact steps/scope/risk | P04-T08 | Edit/approve/failure-policy UX tests |
| [P04-T10](tasks/P04-T10.md) | 1–2d | BACKLOG | RoutineRun điều phối step và terminal outcomes | P04-T08, P03 Action Engine | Complete/fail/cancel/unknown tests |
| [P04-T11](tasks/P04-T11.md) | 1–2d | BACKLOG | Trigger có loop/rate/concurrency guard | P04-T10 | Storm/duplicate/single-instance tests |
| [P04-T12](tasks/P04-T12.md) | 1d | BACKLOG | Runtime revalidate permission/dependency/version | P04-T08, P04-T10 | Revoke/change/disabled dependency tests |
| [P04-T13](tasks/P04-T13.md) | 1–2d | BACKLOG | Pattern detector chỉ tạo routine draft local | P04-T02, P04-T08 | No-auto-activate/consent tests |
| [P04-T14](tasks/P04-T14.md) | 1d | BACKLOG | Memory/routine audit và stop controls hoàn chỉnh | P04-T06, P04-T10 | Redaction/pause/delete/Emergency Stop tests |
| [P04-T15](tasks/P04-T15.md) | 1–2d | BACKLOG | Memory/routine journeys được nghiệm thu | P04-T02..P04-T14 | AC-06/07/08 evidence |

## Execution waves

1. Memory model/storage: P04-T01–P04-T07.
2. Routine definition/execution: P04-T08–P04-T12.
3. Suggestion/control/acceptance: P04-T13–P04-T15.

