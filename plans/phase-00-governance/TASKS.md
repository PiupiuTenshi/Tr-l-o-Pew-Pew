# P00 — Small Task Backlog

## Task contract

Mỗi dòng dưới đây là một outcome độc lập, mục tiêu hoàn thành trong tối đa 2 ngày. Tất cả task kế thừa Non-goals, rules và quality gates từ [phase contract](README.md). Trước khi chuyển `READY`, owner phải ghi file dự kiến sửa, rules áp dụng cụ thể và lệnh kiểm tra vào task/session plan.

| ID | Size | Status | Single outcome | Depends on | Acceptance / evidence |
|---|---:|---|---|---|---|
| [P00-T01](tasks/P00-T01.md) | 1–2d | DONE | Solution có đúng project và reference graph tối thiểu | P00-T07 | Clean Release build PASS (0 warning/error); `dotnet list ... reference` khớp DR matrix |
| [P00-T02](tasks/P00-T02.md) | 1d | DONE | Architecture tests bảo vệ dependency cốt lõi | P00-T01 | Release `dotnet test` PASS (2/2); negative DR-002 fixture bị bắt |
| [P00-T03](tasks/P00-T03.md) | 1d | VERIFY | CI tự động restore/build/test | P00-T01 | Local CI equivalent PASS; simulated failure exit 1; chờ GitHub Actions run sau commit/push được duyệt |
| [P00-T04](tasks/P00-T04.md) | 0.5–1d | BACKLOG | Coding/analyzer/warning convention được chốt | P00-T07 | Approved config/doc; analyzer run có evidence |
| [P00-T05](tasks/P00-T05.md) | 0.5d | DONE | Phase-aligned Git/release workflow được phê duyệt | None | `DEC-006` accepted; central + 8 phase flows; validator pass |
| [P00-T06](tasks/P00-T06.md) | 1d | DONE | Execution roadmap P00–P07 có phase gates | None | 9 plan files; link/ID/AC validator pass |
| [P00-T07](tasks/P00-T07.md) | 1–2d | DONE | Target framework, desktop UI và namespace được quyết định | None | `ADR-008`; BLK-001..003 resolved |
| [P00-T08](tasks/P00-T08.md) | 1d | BACKLOG | Config startup validation và secret-safe baseline hoạt động | P00-T01, P00-T07 | Invalid config fail closed; secret scan pass |
| [P00-T09](tasks/P00-T09.md) | 1d | BACKLOG | CI chặn dependency/secret violation | P00-T02, P00-T03, P00-T08 | Negative fixtures làm gate fail |
| [P00-T10](tasks/P00-T10.md) | 1–2d | BACKLOG | Wake-word/local-tool uncertainty có spike report | P00-T07 | Timebox, benchmark thô, no production claim |
| [P00-T11](tasks/P00-T11.md) | 1d | DONE | Mỗi phase có small-task backlog thực thi được | P00-T06 | 8 `TASKS.md`; IDs/size/status/dependency/link pass |
| [P00-T12](tasks/P00-T12.md) | 1d | BACKLOG | M0 được verify và bàn giao P01 | P00-T01..P00-T09, P00-T11, P00-T13..P00-T16 | M0 checklist, session log và handoff pass |
| [P00-T13](tasks/P00-T13.md) | 1d | DONE | Mỗi Task ID có một executable specification file riêng | P00-T11 | 120 task files; schema/ID/link/field validator pass |
| [P00-T14](tasks/P00-T14.md) | 1d | DONE | Engineering prompt pack phản ánh đúng project contract | P00-T11 | 9 prompts + usage guide; structure/token/project-rule validator pass |
| [P00-T15](tasks/P00-T15.md) | 1–2d | DONE | Governance/readiness findings được reconcile và có Windows validators | P00-T14 | State/reference/ADR/task/readiness validators pass |
| [P00-T16](tasks/P00-T16.md) | 0.5–1d | DONE | Product/domain baseline có explicit approval decision | P00-T15 | `ADR-009`; baseline/achievement/blocker state synced |

## Execution waves

1. Decision/workflow: P00-T05, P00-T07, P00-T11.
2. Repository baseline: P00-T01, P00-T04, P00-T08.
3. Automated protection: P00-T02, P00-T03, P00-T09.
4. Optional uncertainty spike: P00-T10.
5. Individual task specs: P00-T13.
6. Project-specific prompt pack: P00-T14.
7. Governance/readiness remediation: P00-T15.
8. Product/domain baseline approval: P00-T16.
9. Phase verification: P00-T12.
