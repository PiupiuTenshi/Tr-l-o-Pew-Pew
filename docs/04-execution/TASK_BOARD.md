# Task Board

Board này theo dõi task của current phase. Global backlog và task specifications thuộc `plans/<phase>/TASKS.md` và `plans/<phase>/tasks/`.

## Status definitions

- `BACKLOG`: Chưa đủ điều kiện thực hiện.
- `READY`: Có spec, criteria và dependency rõ.
- `IN_PROGRESS`: Đang được một người/agent xử lý.
- `REVIEW`: Chờ code/doc review.
- `VERIFY`: Chờ chạy acceptance/quality gate.
- `BLOCKED`: Không thể tiếp tục vì thiếu quyết định hoặc dependency.
- `DONE`: Đã có evidence và đạt Definition of Done.

## Board

| ID | Task | Phase | Priority | Status | Depends on | Evidence |
|---|---|---|---|---|---|---|
| P00-T01 | Tạo solution skeleton | 00 | Must | DONE | P00-T07 | Clean Release build PASS (0 warning/error); reference graph verified |
| P00-T02 | Thêm architecture tests | 00 | Must | DONE | P00-T01 | Release `dotnet test` PASS (2/2); negative fixture caught |
| P00-T03 | Thiết lập CI build/test | 00 | Must | DONE | P00-T01 | Local CI PASS; simulated failure exit 1; GitHub Actions #30296312267 PASS |
| P00-T04 | Chốt coding conventions | 00 | Should | READY | P00-T07 | Approved document; analyzer run pending |
| P00-T05 | Chốt phase-aligned release and Git flow | 00 | Should | DONE | None | `DEC-006` accepted; central + 8 phase flows; validator PASS |
| P00-T06 | Chuẩn hóa execution roadmap và phase plans | 00 | Must | DONE | None | 9 plan files; 119 unique task IDs; links/AC coverage PASS |
| P00-T07 | Chốt target framework, desktop UI và root namespace | 00 | Must | DONE | None | `ADR-008`; BLK-001..003 resolved |
| P00-T08 | Thiết lập config validation và secret-safe baseline | 00 | Must | BACKLOG | P00-T01, P00-T07 | Startup/config tests + secret scan |
| P00-T09 | Bổ sung CI architecture/security gates | 00 | Must | BACKLOG | P00-T02, P00-T03, P00-T08 | Negative gate evidence |
| P00-T10 | Timeboxed wake-word/local tool-call spikes | 00 | Should | BACKLOG | P00-T07 | Spike report + resource notes |
| P00-T11 | Chuẩn hóa task workflow và evidence path | 00 | Must | DONE | P00-T06 | 8 task backlogs; 119 IDs; max size 2d; validator PASS |
| P00-T12 | Verify phase và bàn giao P01 | 00 | Must | BACKLOG | P00-T01..P00-T09, P00-T11, P00-T13..P00-T16 | M0 checklist + handoff |
| P00-T13 | Tạo individual task specification files | 00 | Must | DONE | P00-T11 | 120 task files; schema/ID/link/field validator PASS |
| P00-T14 | Chuẩn hóa engineering prompt pack theo dự án | 00 | Must | DONE | P00-T11 | 9 prompts + usage guide; structure/token/project-rule validator PASS |
| P00-T15 | Reconcile governance/readiness findings | 00 | Must | DONE | P00-T14 | State/reference/ADR/task/readiness validators PASS |
| P00-T16 | Phê duyệt Product/Domain baselines | 00 | Must | DONE | P00-T15 | `ADR-009`; baseline/achievement/blocker state synced; BLK-004 resolved |
