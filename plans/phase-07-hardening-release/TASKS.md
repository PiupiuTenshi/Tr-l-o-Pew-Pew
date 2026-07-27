# P07 — Small Task Backlog

## Task contract

Mỗi task tạo một release evidence hoặc hardening outcome trong tối đa 2 ngày. Task kế thừa toàn bộ business/dependency/security rules từ [phase contract](README.md). Không task nào được thêm feature mới hoặc làm yếu gate để kịp release.

| ID | Size | Status | Single outcome | Depends on | Acceptance / evidence |
|---|---:|---|---|---|---|
| [P07-T01](tasks/P07-T01.md) | 1d | BACKLOG | Release scope/matrix/Must inventory được freeze | P06 | Approved baseline + deferred list |
| [P07-T02](tasks/P07-T02.md) | 1–2d | BACKLOG | Threat model phản ánh toàn system/data flow | P07-T01 | Reviewed threat/control mapping |
| [P07-T03](tasks/P07-T03.md) | 1–2d | BACKLOG | Permission/replay/prompt/tool security suite đầy đủ | P07-T02 | Security suite report |
| [P07-T04](tasks/P07-T04.md) | 1–2d | BACKLOG | Browser/terminal/worker adversarial suite đầy đủ | P07-T02 | Escape/exhaustion/path report |
| [P07-T05](tasks/P07-T05.md) | 1–2d | BACKLOG | Auth/device/sync/secret/integration suite đầy đủ | P07-T02 | Cross-boundary security report |
| [P07-T06](tasks/P07-T06.md) | 1–2d | BACKLOG | Fault injection chứng minh safe failure semantics | P07-T01 | Network/db/provider/worker evidence |
| [P07-T07](tasks/P07-T07.md) | 1–2d | BACKLOG | Safe Mode/crash recovery/startup integrity hoạt động | P07-T06 | No-auto-replay/recovery tests |
| [P07-T08](tasks/P07-T08.md) | 1–2d | BACKLOG | Observability/support bundle redacted và opt-in | P07-T02 | Metadata-only/no-content tests |
| [P07-T09](tasks/P07-T09.md) | 1–2d | BACKLOG | Resource benchmark/budgets được ghi và enforce | P07-T01 | Reference-machine report |
| [P07-T10](tasks/P07-T10.md) | 1–2d | BACKLOG | Accessibility/compatibility/degraded audit đóng findings | P07-T01 | Matrix + closure evidence |
| [P07-T11](tasks/P07-T11.md) | 1–2d | BACKLOG | Clean installer/install/uninstall hoạt động | P07-T07 | Clean VM smoke evidence |
| [P07-T12](tasks/P07-T12.md) | 1–2d | BACKLOG | Update integrity/rollback/channel controls hoạt động | P07-T11 | Signature/hash/rollback tests |
| [P07-T13](tasks/P07-T13.md) | 1d | BACKLOG | SBOM/dependency/license/security scans có report | P07-T01 | Inventory + scan results |
| [P07-T14](tasks/P07-T14.md) | 1–2d | BACKLOG | Onboarding/user/privacy/deploy/recovery docs được duyệt | P07-T08, P07-T10, P07-T11 | Doc/usability review |
| [P07-T15](tasks/P07-T15.md) | 1–2d | BACKLOG | Full AC-01..AC-12 RC suite có report | P07-T03..P07-T14 | MVP acceptance report |
| [P07-T16](tasks/P07-T16.md) | 1–2d | BACKLOG | Private beta findings được triage/retest | P07-T15 | Resolved/deferred finding list |
| [P07-T17](tasks/P07-T17.md) | 1d | BACKLOG | Final release/rollback decision có approval | P07-T16 | Signed checklist, notes, rollback window |

## Execution waves

1. Freeze/threat: P07-T01, P07-T02.
2. Security/recovery/observability/performance: P07-T03–P07-T10, P07-T13.
3. Packaging/docs: P07-T11, P07-T12, P07-T14.
4. Acceptance/beta/release: P07-T15–P07-T17.

