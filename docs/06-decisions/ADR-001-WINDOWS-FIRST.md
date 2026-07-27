# ADR-001 — Windows-first MVP

- Status: Accepted
- Decision: `DEC-001`
- Date: 2026-07-27
- Owners: Product Owner, Architecture

## Context

MVP cần wake word, desktop/tray UX, OS integration, local runtime và device automation. Hỗ trợ nhiều desktop OS ngay từ đầu sẽ làm tăng đáng kể test matrix, installer, permission và native integration scope.

## Decision

Windows là desktop/device-agent target ưu tiên của MVP. Web Console, Chromium Extension và Android Companion vẫn nằm trong roadmap theo phase; full macOS/Linux/iOS agent không thuộc MVP.

Platform-specific code phải nằm sau Application abstractions và Infrastructure/Device adapters. Domain/Application không phụ thuộc Windows API concrete implementation.

## Alternatives

- Cross-platform desktop ngay từ P00: từ chối vì mở rộng scope và test matrix.
- Web-only assistant: từ chối vì không đáp ứng local voice/device-agent outcome.

## Consequences

- CI, installer, accessibility và resource baseline ưu tiên Windows.
- Architecture vẫn bảo vệ khả năng thay adapter trong tương lai.
- Không dùng “Windows-first” để cho phép chạy Administrator mặc định.

## Revisit

Chỉ xem xét full desktop cross-platform sau MVP evidence và Change Request được phê duyệt.
