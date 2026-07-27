# P03 — Small Task Backlog

## Task contract

Mỗi task thêm đúng một boundary hoặc capability automation, tối đa 2 ngày. Task kế thừa `BR-UI-*`, `BR-TRM-*`, action/security rules và `DR-015`–`DR-030` từ [phase contract](README.md). Không task nào được thêm arbitrary shell, broad origin permission hoặc unverified click.

| ID | Size | Status | Single outcome | Depends on | Acceptance / evidence |
|---|---:|---|---|---|---|
| [P03-T01](tasks/P03-T01.md) | 1–2d | BACKLOG | OS/browser/terminal specs và threat model được duyệt | P02 | Abuse cases, trust boundaries, AC rõ |
| [P03-T02](tasks/P03-T02.md) | 1–2d | BACKLOG | Skill manifest verify/enable/quarantine hoạt động | P03-T01 | Hash/capability/version/quarantine tests |
| [P03-T03](tasks/P03-T03.md) | 1–2d | BACKLOG | Worker manager áp timeout/quota/process ownership | P03-T01 | Crash/timeout/quota/process-tree tests |
| [P03-T04](tasks/P03-T04.md) | 1–2d | BACKLOG | Action Engine dispatch/verify/reconcile qua worker | P03-T02, P03-T03 | Complete/unknown/cancel tests |
| [P03-T05](tasks/P03-T05.md) | 1–2d | BACKLOG | Windows accessibility actions dùng allowlist/readback | P03-T04 | Target/permission/verification tests |
| [P03-T06](tasks/P03-T06.md) | 1d | BACKLOG | Chromium extension có minimal manifest/origin policy | P03-T01 | Manifest review; denied-origin test |
| [P03-T07](tasks/P03-T07.md) | 1–2d | BACKLOG | Desktop–Extension bridge xác thực caller/anti-replay | P03-T03, P03-T06 | Auth/replay/disconnect tests |
| [P03-T08](tasks/P03-T08.md) | 1–2d | BACKLOG | Tab context và `UiTargetSnapshot` có TTL/redaction | P03-T07 | Stale/invalidate/secret-field tests |
| [P03-T09](tasks/P03-T09.md) | 1–2d | BACKLOG | Tab/video/playback actions verify hậu điều kiện | P03-T08, P03-T04 | Ambiguity + URL/player readback tests |
| [P03-T10](tasks/P03-T10.md) | 1d | BACKLOG | Skip-button monitor bị giới hạn tab và TTL | P03-T09 | Stop/no-bypass/tab-close tests |
| [P03-T11](tasks/P03-T11.md) | 1–2d | BACKLOG | Terminal workflow validate/version/approve đúng hash | P03-T01, P03-T02 | Executable/path/args/risk tests |
| [P03-T12](tasks/P03-T12.md) | 1–2d | BACKLOG | Structured terminal runner build/test/start/stop | P03-T03, P03-T11 | Exit/output/cancel/process tests |
| [P03-T13](tasks/P03-T13.md) | 1–2d | BACKLOG | Path/env/network/output boundaries fail closed | P03-T12 | Traversal/injection/secret leak tests |
| [P03-T14](tasks/P03-T14.md) | 1d | BACKLOG | Emergency/revoke/quarantine cascade tới mọi worker | P03-T04, P03-T07, P03-T12 | Browser/terminal/process stop evidence |
| [P03-T15](tasks/P03-T15.md) | 1–2d | BACKLOG | Browser/terminal security journeys được nghiệm thu | P03-T05..P03-T14 | AC-04/05/07/08/12 evidence |

## Execution waves

1. Threat/registry/worker: P03-T01–P03-T04.
2. Windows/browser: P03-T05–P03-T10.
3. Terminal: P03-T11–P03-T13.
4. Cascade/acceptance: P03-T14, P03-T15.

