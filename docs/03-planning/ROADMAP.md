# Product Roadmap

## Mục tiêu

Roadmap này chia MVP thành các vertical phase có dependency và quality gate rõ ràng. Chi tiết task, traceability, risk và exit evidence nằm tại [`plans/README.md`](../../plans/README.md).

`PROJECT_SCOPE.md` là scope baseline. Chuỗi 8 execution phase dưới đây là cách phân rã 7 scope phase thành các gate nhỏ hơn; không mở rộng capability hoặc thay đổi business rule.

## Phase sequence

| Phase | Outcome | Scope mapping | Exit evidence chính |
|---|---|---|---|
| `P00` — Governance and Repository Foundation | Repository, CI và workflow có thể kiểm chứng | Scope Phase 0 — governance/tooling | Clean build, architecture test, CI, handoff |
| `P01` — Core Security and Domain Foundation | Identity, device, interaction, policy, action, audit và persistence primitives | Scope Phase 0 — security baseline; enabler Scope Phase 1 | Domain/policy tests và denied-by-default vertical slice |
| `P02` — Local Windows Assistant | Voice/text local và Level 0–1 action offline | Scope Phase 1 | Offline assistant demo và resource baseline |
| `P03` — Safe Device, Browser and Terminal Automation | Browser/OS/terminal action qua worker, policy và verification | Scope Phase 2 | Browser và terminal journeys với cancel/audit |
| `P04` — User-controlled Memory and Routines | Memory local quản lý được và routine có version/approval | Scope Phase 4 — phần local | Memory CRUD/delete và routine approval/run demo |
| `P05` — Cloud, Web and Multi-device | Cloud routing, sync, Web Console, Android và device revoke | Scope Phase 3; Scope Phase 4 — phần sync | Cross-device command/confirmation và tombstone sync |
| `P06` — Messaging and Home Assistant Integrations | Integration có permission, confirmation, revoke và audit | Scope Phase 5 | Verified message send và Home Assistant low-risk action |
| `P07` — Hardening, Private Beta and Release | Security, performance, installer/update, recovery và release evidence | Scope Phase 6 | Release checklist và MVP acceptance report |

## Dependency

```text
P00 → P01 → P02 → P03 → P04 → P05 → P06 → P07
```

Phase sau chỉ được mở khi Must task và exit gate của phase trước có evidence, trừ khi có decision được phê duyệt ghi rõ phần việc có thể chạy song song mà không vượt security/dependency boundary.

## Planning rules

- P04 đứng trước P05 để chốt local memory, retention và tombstone semantics trước cloud sync.
- Capability write không được triển khai trước Policy Engine, confirmation, verification và audit foundation.
- Level 3 action bị chặn mặc định trong toàn bộ MVP.
- Optional/Should/Could task không được làm chậm Must path hoặc được dùng để tuyên bố phase hoàn tất.
- Mọi thay đổi phase, scope hoặc exit criterion phải cập nhật `plans/`, milestone, task board và decision/change request phù hợp.
