# Risk Register

| ID | Risk | Probability | Impact | Mitigation | Owner | Status |
|---|---|---:|---:|---|---|---|
| RSK-001 | AI thực thi sai hành động trên máy | Medium | Critical | Policy engine, confirmation, sandbox, audit | Security | Open |
| RSK-002 | Prompt injection từ web/email | High | High | Trust separation, tool policy, untrusted data labeling | Security | Open |
| RSK-003 | Agent chạy nền chiếm RAM/CPU | Medium | High | Lazy load, budgets, worker isolation, metrics | Runtime | Open |
| RSK-004 | Sync làm rò rỉ memory nhạy cảm | Medium | Critical | Encryption, privacy mode, explicit scope | Cloud | Open |
| RSK-005 | Vibe coding làm lệch kiến trúc | High | High | AGENTS, architecture tests, small tasks, ADR | Architecture | Mitigating |
| RSK-006 | Scope quá lớn, không ra MVP | High | High | Phase gates, MoSCoW, change requests | Product | Open |
