# Pew Pew Assistant — Vibe Coding Management System

Bộ khung quản lý phát triển bằng AI cho dự án trợ lý AI cá nhân đa nền tảng.
Mục tiêu của bộ khung là giúp Codex, Claude, Gemini hoặc developer con người làm việc theo cùng một nguồn sự thật, không mất ngữ cảnh, không tự ý mở rộng phạm vi và luôn để lại bằng chứng kiểm chứng được.

## Nguyên tắc vận hành

1. Documentation là nguồn sự thật trước code.
2. Mỗi phiên chỉ thực hiện một mục tiêu nhỏ có tiêu chí hoàn thành rõ ràng.
3. Không code khi feature chưa có specification hoặc acceptance criteria.
4. AI phải đọc `AGENTS.md`, `docs/03-planning/CURRENT_PHASE.md` và `.ai/NEXT_ACTION.md` trước khi sửa code.
5. Domain và dependency rules không được phá vỡ để đổi lấy tốc độ.
6. Mọi thay đổi phải có test, bằng chứng kiểm tra và cập nhật trạng thái.
7. Khi thiếu thông tin ảnh hưởng đến nghiệp vụ, bảo mật hoặc kiến trúc, AI phải dừng và tạo blocker thay vì tự đoán.
8. Không cho AI chạy lệnh phá hủy, nâng quyền hoặc sửa dữ liệu thật nếu chưa có phê duyệt cụ thể.

## Luồng chuẩn

```text
Vision / Scope / Business Rules
              ↓
Architecture / Domain Lifecycle
              ↓
Roadmap → Phase → Feature Spec → Task
              ↓
Plan → Implement → Test → Review → Verify
              ↓
Update logs → Handoff → Next action
              ↓
Merge → Release → Retrospective
```

## Điểm bắt đầu mỗi phiên

```text
1. Đọc AGENTS.md.
2. Đọc .ai/CONTEXT.md.
3. Đọc docs/03-planning/CURRENT_PHASE.md.
4. Đọc .ai/NEXT_ACTION.md và .ai/BLOCKERS.md.
5. Đọc spec, business rule, lifecycle và dependency rule liên quan.
6. Xác nhận phạm vi của đúng một task.
7. Chỉ bắt đầu code sau khi acceptance criteria có thể kiểm thử.
```

## Điểm kết thúc mỗi phiên

```text
1. Chạy build, test và các quality gate liên quan.
2. Ghi kết quả vào docs/04-execution/SESSION_LOG.md.
3. Cập nhật TASK_BOARD.md.
4. Cập nhật .ai/WORKING_MEMORY.md ở dạng tóm tắt.
5. Ghi chính xác NEXT_ACTION.md cho phiên tiếp theo.
6. Ghi blocker hoặc quyết định mới nếu có.
7. Không tuyên bố hoàn thành nếu chưa có evidence.
```

## Cấu trúc thư mục

```text
.
├── AGENTS.md
├── RULES.md
├── README.md
├── .ai/                       # Trạng thái ngắn hạn cho AI
├── docs/
│   ├── 00-product/            # Vision, scope, business rules
│   ├── 01-domain/             # Entity lifecycle, domain model
│   ├── 02-architecture/       # Dependency rules, module map, context
│   ├── 03-planning/           # Roadmap, phases, current phase
│   ├── 04-execution/          # Task, feature spec, session, handoff
│   ├── 05-quality/            # DoD, tests, security, quality gates
│   ├── 06-decisions/          # ADR, risk, decision log
│   └── 07-release/            # Git flow, release, changelog
├── plans/                     # Kế hoạch chi tiết từng phase
├── prompts/                   # Prompt vận hành có thể tái sử dụng
├── src/
├── tests/
├── scripts/
└── .github/
```

## Trạng thái task

```text
BACKLOG → READY → IN_PROGRESS → REVIEW → VERIFY → DONE
                         ↘ BLOCKED ↗
```

Một task chỉ được chuyển sang `DONE` khi đáp ứng Definition of Done và có evidence tương ứng.

## Kiểm tra repository trên Windows

Chạy từ repository root bằng Windows PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts\Test-ManagementStructure.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts\Test-TaskGitFlow.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts\Test-ProjectPrompts.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts\Test-RepositoryReadiness.ps1"
```

Ba validator đầu phải trả về `PASS`. Readiness validator luôn kiểm tra được trạng thái hiện tại; `IMPLEMENTATION_READY=NO` là kết quả đúng khi còn baseline/decision blocker hoặc chưa có solution, CI và architecture tests.

## Chạy Desktop khi phát triển

Từ repository root, chạy:

```powershell
dotnet watch run
```

Root launcher `PewPew.csproj` khởi động `PewPew.Desktop`; bạn không cần thêm `--project`. `dotnet watch` theo dõi thay đổi code UI; đổi project file, package hoặc composition root có thể cần restart.
