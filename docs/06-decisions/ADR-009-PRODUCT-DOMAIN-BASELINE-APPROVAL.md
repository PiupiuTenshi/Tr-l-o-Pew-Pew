# ADR-009 — Approve product and domain baselines

- Status: Accepted
- Date: 2026-07-28
- Decision owners: Product Owner
- Task: P00-T16

## Context

The Project Scope, Business Rules and Entity Lifecycles documents were used as planning sources while marked Proposed. Implementation must not treat a proposed product or domain contract as silently approved.

## Decision

The Product Owner approves these documents as-is:

- `docs/00-product/PROJECT_SCOPE.md`
- `docs/00-product/BUSINESS_RULES.md`
- `docs/01-domain/ENTITY_LIFECYCLES.md`

Their existing content becomes the approved baseline for subsequent P00 implementation and planning. Any material scope, business-rule or lifecycle change requires a Change Request or a superseding accepted decision.

## Consequences

- `BLK-004` is resolved.
- ACH-001 and ACH-002 may be recorded as achieved.
- This decision does not approve Git policy `DEC-006`, create a solution, or authorize a commit/push.

## Security and operational impact

The approved baseline preserves default deny, Policy Engine control, confirmation, verification and audit requirements. No permissions or external side effects are added by this decision.
