# ADR-002 — Modular Monolith for MVP

- Status: Accepted
- Decision: `DEC-002`
- Date: 2026-07-27
- Owners: Product Owner, Architecture

## Context

Pew Pew Assistant có nhiều capability nhưng MVP cần giữ deployment, debugging và transaction boundary đủ đơn giản cho nhóm nhỏ, đồng thời không được làm mất module ownership.

## Decision

Business tier dùng Modular Monolith với Clean Architecture theo module. Chỉ tách process/worker khi cần cô lập privilege, crash, resource hoặc long-running execution.

Module trao đổi qua Application contract, event hoặc published read model; không truy cập bảng nội bộ của module khác.

## Alternatives

- Microservices từ đầu: từ chối vì tăng vận hành, consistency và deployment cost.
- Monolith không module boundary: từ chối vì làm tăng coupling và phá data ownership.

## Consequences

- Một deployable business backend có boundary được bảo vệ bằng architecture tests.
- Worker/device process có thể tách mà không chuyển toàn hệ thống sang microservices.
- Mọi ngoại lệ dependency cần ADR riêng.

## Revisit

Chỉ tách service khi có evidence về isolation, scaling hoặc ownership mà modular deployment không đáp ứng.
