# Goal & Constraint Map

File này là nơi giải nghĩa các ID ngắn được dùng trong phần `Input` của prompt. Agent phải đọc và resolve ID trước khi lập kế hoạch; ID không tự tạo approval, scope hoặc architecture decision.

## Phase ID index

| Phase | Goal ID | Constraint ID | Phase contract |
|---|---|---|---|
| P00 — Governance | `G00` | `C00` | [P00](../plans/phase-00-governance/README.md) |
| P01 — Security & Domain | `G01` | `C01` | [P01](../plans/phase-01-foundation/README.md) |
| P02 — Local Assistant | `G02` | `C02` | [P02](../plans/phase-02-local-assistant/README.md) |
| P03 — Safe Automation | `G03` | `C03` | [P03](../plans/phase-03-device-automation/README.md) |
| P04 — Memory & Routines | `G04` | `C04` | [P04](../plans/phase-04-memory-routines/README.md) |
| P05 — Cloud & Multi-device | `G05` | `C05` | [P05](../plans/phase-05-cloud-multidevice/README.md) |
| P06 — Integrations | `G06` | `C06` | [P06](../plans/phase-06-integrations/README.md) |
| P07 — Hardening & Release | `G07` | `C07` | [P07](../plans/phase-07-hardening-release/README.md) |

## Goal IDs

| ID | Nội dung |
|---|---|
| `G00` | Hoàn thiện governance/repository foundation để solution, architecture tests và CI có thể bắt đầu sau các decision bắt buộc. |
| `G01` | Xây dựng core security/domain control path: identity, device trust, structured plan, permission, confirmation, action state và audit. |
| `G02` | Tạo vertical slice Windows-first local assistant với text/push-to-talk/wake word và Level 0–1 actions offline qua control path P01. |
| `G03` | Mở rộng action an toàn cho Windows UI, browser và terminal qua worker cô lập, policy, confirmation, verification, audit và Emergency Stop. |
| `G04` | Xây memory local có kiểm soát và routines được phê duyệt, với deletion/tombstone semantics hoàn chỉnh trước cloud sync. |
| `G05` | Bổ sung cloud/API/Web/Android và cross-device continuity với explicit disclosure, pairing/revoke, signed commands và opt-in sync. |
| `G06` | Chứng minh integration framework an toàn qua messaging và Home Assistant với least privilege, confirmation, verification, revoke và audit. |
| `G07` | Đóng băng capability, harden, kiểm thử recovery/security, hoàn thiện package/update và phát hành chỉ khi release gates có evidence. |

## Constraint IDs

| ID | Nội dung |
|---|---|
| `C00` | Không tự phê duyệt Scope, Business Rules hoặc Entity Lifecycles; không tự chọn desktop UI hoặc root namespace; không commit hoặc push khi `DEC-006` vẫn `Proposed`. |
| `C01` | Không OS/browser/terminal thật, cloud AI/sync hoặc remote device control; giữ default deny và không gom toàn bộ lifecycle vào một aggregate/service. |
| `C02` | Local/Private Mode không gọi cloud; voice không là authentication; không lưu raw audio dài hạn, browser DOM automation hoặc arbitrary terminal. |
| `C03` | Không arbitrary shell, admin/root, firewall/registry/security changes, CAPTCHA/DRM/paywall bypass hoặc cloud remote execution; mọi write action đi qua worker/policy/confirmation/verification/audit. |
| `C04` | Memory không tạo authority; delete/tombstone không được resurrection; routine không tự kích hoạt nếu chưa được user approve; không cloud sync trước P05. |
| `C05` | Private Mode fail-closed; cloud không có shell/OS authority; không sync memory/secret mặc định; device command phải signed, scoped và revoke được. |
| `C06` | Không mass messaging, auto-reply toàn bộ, impersonation, auto-call, life-safety control hoặc plugin marketplace; credential ở vault và high-risk action cần confirmation. |
| `C07` | Không thêm capability/platform/provider lớn; không tắt security/quality gate để kịp deadline; không release khi blocker High/Critical hoặc rollback/signing authority chưa rõ. |

## Cách dùng

```md
- Planning goal: `G00`
- Constraints đã xác nhận: `C00`
```

Khi cần mục tiêu/ràng buộc bổ sung trong cùng phase, thêm ID phụ thay vì ghi đè map phase:

```md
| `G03A` | Planning goal | ... |
| `C03A` | Confirmed constraints | ... |
```

Nếu một ID yêu cầu thay đổi product scope, business rule, lifecycle, security boundary hoặc kiến trúc nền tảng, phải tạo Change Request hoặc decision request; không coi map là approval.
