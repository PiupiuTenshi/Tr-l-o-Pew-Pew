# ADR-004 — Default-deny permission model

- Status: Accepted
- Decision: `DEC-004`
- Date: 2026-07-27
- Owners: Product Owner, Security, Architecture

## Context

Trợ lý có thể gửi dữ liệu, chạy workflow, điều khiển ứng dụng và thiết bị. Quyền mơ hồ hoặc wildcard có thể biến model error thành side effect nghiêm trọng.

## Decision

Permission mặc định là deny và phải có subject, device, tool, action, resource, constraint, validity và revocation state. Sensitive action cần confirmation gắn user/session/device/plan/target/payload hash/expiry/single-use nonce, trừ trusted routine có scope chính xác.

Level 3 action bị chặn mặc định trong MVP.

## Alternatives

- Allow-by-default: từ chối do không kiểm soát được blast radius.
- UI hiding thay authorization: từ chối vì không bảo vệ server/business boundary.
- Global `allow_all`: từ chối trong production behavior.

## Consequences

- Denied, expired, revoked và replay paths là test bắt buộc.
- Permission/confirmation events phải audit nhưng không log secret/full sensitive payload.
- Usability phải giải quyết bằng scoped routine, không nới invariant.

## Revisit

Security exception cần owner, expiry, scope và decision/ADR riêng; không thay policy mặc định.
