# Working Memory

> Chỉ lưu tóm tắt ngắn hạn cần cho phiên kế tiếp. Không sao chép toàn bộ log hoặc tài liệu dài.

## Current implementation state

- Phase hiện tại: Phase 00 — Governance.
- Local Git metadata tồn tại trên nhánh `main`, nhưng repository chưa có commit nền.
- `PewPew.sln` có 11 project, gồm root dev launcher `PewPew.csproj`; core chạy `net10.0`, Desktop/DeviceAgent/launcher chạy `net10.0-windows`; Desktop pin Avalonia 12.1.0.
- `.github/workflows/ci.yml` và `scripts/Invoke-Ci.ps1` đã có; local CI equivalent PASS nhưng GitHub Actions run chờ commit/push authorization. `tests/PewPew.Architecture.Tests` bảo vệ DR-001, DR-002, DR-005 và DR-011.
- Project Scope, Business Rules và Entity Lifecycles là approved baselines theo `DEC-009`/`ADR-009`.
- Execution roadmap P00–P07 có 123 Task ID duy nhất; mỗi task có size tối đa 2 ngày, status, dependency, explicit mode/risk và acceptance/evidence.
- 123 generated task specifications, 8 task indexes và 8 phase Git flow views đồng bộ với phase sources.
- `P00-T01`, `P00-T02`, `P00-T15`, `P00-T07`, `P00-T16` và `P00-T05` đã hoàn tất; `P00-T03` đang `VERIFY`, chờ GitHub Actions evidence.
- P00-T01 clean Release build PASS (0 warning/error); reference graph đã kiểm tra bằng `dotnet list ... reference`.
- Readiness vẫn chưa đủ do architecture tests, CI và secret/config baseline chưa có.

## Confirmed decisions

- `DEC-001` — Windows-first MVP.
- `DEC-002` — Modular Monolith cho MVP.
- `DEC-003` — Local–Cloud Hybrid.
- `DEC-004` — Default deny permission model.
- `DEC-005` — AI model không trực tiếp thực thi action.
- `DEC-007` — Session Result/output contract thống nhất.
- `DEC-008` — `net10.0` cho core, `net10.0-windows` cho desktop/Windows boundary, Avalonia và root namespace `PewPew`.
- `DEC-009` — Product Owner phê duyệt Project Scope, Business Rules và Entity Lifecycles as-is.
- ADR-001 đến ADR-005 đã được tạo từ các decision đã Accepted; không tạo quyết định sản phẩm mới.

## Recent work

- Sửa reference sai tên tài liệu Product/Domain, sau đó mở ACH-001/ACH-002 theo explicit approval `DEC-009`.
- Chuyển RSK-005 về `Mitigating` vì architecture tests/CI chưa tồn tại.
- Chuẩn hóa output contract giữa `AGENTS.md`, `.ai/OUTPUT_CONTRACT.md` và prompt pack.
- Bổ sung explicit `Mode`/`Risk` vào toàn bộ phase task tables; generator không còn suy đoán metadata.
- Generator đánh dấu generated files, từ chối ghi đè file unmanaged, đồng bộ acceptance/evidence của task `DONE`.
- Thêm PowerShell validators cho management structure và repository readiness; thêm baseline `.gitignore`.
- Chuẩn hóa prompt planning/implement dùng phase IDs `G00`–`G07` (goal) và `C00`–`C07` (constraints); nội dung được quản lý tại `prompts/GOAL_CONSTRAINT_MAP.md`.
- `DEC-006` đã Accepted; phase/task Git mappings có hiệu lực, nhưng không tự tạo commit, branch, push hay PR.
- P00-T01 tạo `PewPew.sln`, 10 project skeleton và pin Avalonia 12.1.0 tại `PewPew.Desktop`; một clean build trong sandbox bị chặn vì Avalonia ghi telemetry ngoài workspace, build lại ngoài sandbox PASS.
- User yêu cầu workflow UI như web; root dev launcher cho phép chạy từ root bằng `dotnet watch run`. Command đã khởi động được `PewPew.exe`; UI/app feature vẫn thuộc phase sau.
- P00-T02 thêm xUnit v3 architecture test project; Release suite PASS 2/2, gồm positive graph và negative fixture Application → Infrastructure bị DR-002 bắt.
- P00-T03 thêm CI chỉ có `contents: read`, checkout, .NET setup, restore/build/test. Workflow validator và local equivalent PASS; simulated failure exit 1; không tự commit/push để tạo GitHub run.

## Context to preserve

- Không tự phê duyệt Product Scope, Business Rules, Entity Lifecycles, Git policy hoặc technical choices.
- Không tạo commit/remote, push, merge, tag hoặc branch protection nếu chưa có authorization rõ.
- P04 đứng trước P05 để chốt local memory/delete/tombstone trước cloud sync; đây là execution ordering, không đổi scope.
- Phase `README.md` và `TASKS.md` là source của generated task/index/Git views; không sửa trực tiếp file có generated marker.
- Sau khi sửa phase sources, chạy `scripts/Generate-TaskFiles.ps1 -Force` rồi các validators.
- Technical baseline đã được chốt bởi `DEC-008`/`ADR-008`; `BLK-001..003` đã resolved.
