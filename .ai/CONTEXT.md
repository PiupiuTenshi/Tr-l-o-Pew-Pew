# AI Context

## Product

Pew Pew Assistant là trợ lý AI cá nhân 1:1 hoạt động trên nhiều thiết bị, kết hợp local AI và cloud AI, có wake word, memory phân tầng, automation hệ điều hành/trình duyệt/terminal và tích hợp Home Assistant.

## MVP direction

- Windows-first desktop device agent.
- Web console quản lý tài khoản, memory, permission và audit.
- Chromium browser extension.
- Local–Cloud Hybrid AI routing.
- Local memory giới hạn tài nguyên.
- Structured terminal workflows.
- Security mặc định từ chối và quyền tối thiểu.

## Architectural shape

3-tier ở cấp triển khai:

1. Presentation Tier.
2. Business Tier, bên trong có API, Application, Domain, Infrastructure và Device Agent.
3. Data Tier.

MVP triển khai theo Modular Monolith, tách worker/process ở các vị trí cần cô lập.

## Required references

- Product: `docs/00-product/`
- Domain: `docs/01-domain/`
- Architecture: `docs/02-architecture/`
- Current phase: `docs/03-planning/CURRENT_PHASE.md`
- Work state: `.ai/NEXT_ACTION.md`, `.ai/BLOCKERS.md`
