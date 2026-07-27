# ADR-005 — Separate model proposal from privileged action

- Status: Accepted
- Decision: `DEC-005`
- Date: 2026-07-27
- Owners: Product Owner, Security, Architecture

## Context

Model output có thể sai, bị prompt injection hoặc tạo tool call không hợp lệ. Nối model trực tiếp tới shell, database, browser/device hoặc messaging sẽ bypass policy và domain invariant.

## Decision

Luồng đặc quyền bắt buộc:

```text
Model proposal
→ strict schema parse/validation/normalization
→ Policy Engine và risk classification
→ domain state transition
→ confirmation khi cần
→ isolated Action Engine/worker
→ verification
→ redacted audit
```

External content luôn là untrusted data và không thể tự cấp quyền hoặc tự phê duyệt confirmation.

## Alternatives

- Direct model tool execution: từ chối.
- Prompt-only safety boundary: từ chối vì không enforce được.
- Adapter tự quyết định permission: từ chối vì bypass policy ownership.

## Consequences

- Structured plan/contract và policy tests là bắt buộc.
- Worker cần timeout, cancellation, resource bounds và process cleanup.
- Verification result, không phải model claim, quyết định action outcome.

## Revisit

Không nới separation này nếu chưa có security review và accepted ADR mới.
