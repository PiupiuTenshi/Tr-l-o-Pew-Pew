# Prompt — Security Review Pew Pew Assistant Capability

## Input

- Feature/task: `{TASK_ID}`
- Surface/boundary: `{AREA}`
- Review target: `{REVIEW_TARGET}`
- Data và side effects dự kiến: `{DATA_AND_SIDE_EFFECTS}`

## Vai trò

Bạn đang ở mode `SECURITY_REVIEW`. Giả định model output, website/email/document/clipboard/screen/notification, client input và external API response đều không đáng tin cậy.

Không sửa code mặc định. Không tuyên bố “an toàn tuyệt đối”; nêu phạm vi và evidence.

## Context bắt buộc

Đọc:

- `AGENTS.md`, `RULES.md`, AI/current-phase state và blockers.
- Product scope/business rules và prohibited capabilities.
- Entity lifecycles của identity, device, action, confirmation, permission, memory, routine, worker, integration, secret, incident và audit khi áp dụng.
- Dependency rules/ADR.
- Task/phase acceptance, `SECURITY_CHECKLIST.md`, Quality Gates và Git flow.

## Trust boundaries bắt buộc xem xét

```text
User/UI
  → API/Application
  → Model proposal (untrusted)
  → Schema validation/normalization
  → Policy + risk classification
  → Domain state transition
  → Confirmation when required
  → Isolated Action Engine/worker
  → Verification
  → Redacted audit
```

## Threat checklist

1. Identity/device:
   - Authentication, authorization, pairing, revoke, session expiry.
2. Permission/confirmation:
   - Exact subject/device/tool/action/resource scope.
   - Plan/target/payload hash, expiry, nonce, single-use và replay.
3. AI/untrusted content:
   - Prompt/tool injection, malformed plan, hallucinated target, schema bypass.
4. Terminal/browser/OS:
   - Allowlisted structured workflow, canonical path, working-directory boundary.
   - Process tree, timeout, cancellation, output/resource limits, worker escape.
5. Local/cloud:
   - Private Mode, explicit disclosure, data minimization, provider/retention.
   - Không silent fallback.
6. Memory/routine/sync:
   - Consent, ownership, delete/tombstone, resurrection, trusted version.
7. Integration/Home Assistant/messaging:
   - Credential isolation, scoped capability, recipient/entity ambiguity.
   - Idempotency, confirmation, verification, revoke và failure isolation.
8. Secret/log/audit:
   - OS vault, no secret in source/config/log/prompt/test artifact.
   - Audit metadata đủ nhưng không chứa full sensitive payload.
9. Reliability/abuse:
   - Rate/resource exhaustion, race, duplicate, retry, queue bounds, emergency stop.
10. Supply chain/update:
    - Dependency provenance/license, signature/hash, rollback, plugin capability change.

## Risk model

- Action Level 0: read-only.
- Level 1: low-risk, reversible.
- Level 2: sensitive/external side effect; confirmation hoặc exact trusted routine.
- Level 3: high-risk/hard-to-reverse; MVP mặc định chặn.

Không hạ risk level để tránh confirmation.

## Finding format

```md
### [Critical|High|Medium|Low] Threat title

- Asset/trust boundary:
- Preconditions:
- Attack path:
- Impact:
- Existing control/evidence:
- Gap:
- Minimal remediation:
- Required regression/security test:
```

## Output bắt buộc

- Scope, assumptions và review limitations.
- Data-flow/trust-boundary summary.
- Findings theo severity.
- Abuse cases đã kiểm tra.
- Gate recommendation: PASS | PASS WITH CONDITIONS | FAIL | NOT ENOUGH EVIDENCE.
- Required blocker/incident/decision updates.
