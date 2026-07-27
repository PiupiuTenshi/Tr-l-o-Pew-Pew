# Test Strategy

## Test pyramid

1. Domain unit tests: state transition, invariant, policy primitives.
2. Application tests: use case, orchestration, failure mapping.
3. Integration tests: database, message bus, provider adapters.
4. Contract tests: device, browser extension, cloud API, Home Assistant.
5. End-to-end tests: ít nhưng tập trung vào critical journey.

## Critical test areas

- Clarification versus confirmation.
- Permission expiry and revocation.
- Action plan hash and confirmation binding.
- Prompt injection boundary.
- Memory deletion and sync tombstone.
- Emergency stop.
- Worker timeout/resource limit.
- Device revoke and session invalidation.
- Local/cloud routing with sensitive data.

## Determinism

AI output trong automated test phải được thay bằng fake/stub có dữ liệu xác định. Không phụ thuộc API model thật trong test mặc định.
