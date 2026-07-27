# Session Log

## Session template

### YYYY-MM-DD — <agent/person> — <task id>

**Goal:**  
...

**Changes:**
- ...

**Verification:**
- `<command>` → PASS/FAIL

**Decisions:**
- ...

**Blockers:**
- ...

**Next action:**
- ...

---

## 2026-07-27 — ChatGPT — Governance scaffold

**Goal:** Tạo cấu trúc quản lý vibe coding.

**Changes:**
- Tạo tài liệu quản trị, planning, execution, quality, decision và release.
- Tạo AI context, working memory, next action và guardrails.
- Tạo prompt templates và phase plans.

**Verification:**
- Kiểm tra cây thư mục và link nội bộ cơ bản.

**Next action:**
- Chốt target framework và desktop UI framework.

---

## 2026-07-27 — Codex — P00-T06

**Goal:** Đọc toàn bộ documentation baseline và chia execution roadmap thành các phase có thể giao việc, kiểm chứng và bàn giao.

**Changes:**
- Tạo `plans/README.md` làm chỉ mục, mapping 8 execution phase với 7 scope phase, dependency chain và AC coverage.
- Mở rộng `plans/phase-00` đến `plans/phase-07` với phase contract, non-goals, entry, deliverables, task, dependency, traceability, acceptance, gate, risk, exit và handoff.
- Đồng bộ `docs/03-planning/ROADMAP.md` với execution roadmap mà không đổi product scope.
- Cập nhật task board và AI handoff state; giữ technical decision blockers mở.

**Verification:**
- PowerShell plan validator → PASS.
- 9 plan files, 119 unique task IDs.
- Tất cả phase có 10 section bắt buộc.
- `AC-01` đến `AC-12` đều được trace.
- Tất cả local Markdown links trong `plans/` resolve.
- SHA-256 checksum được tạo cho 9 plan files.

**Decisions:**
- 8 execution phase là phân rã chi tiết của 7 scope phase, không phải scope baseline mới.
- P04 local memory/routine đứng trước P05 cloud sync để chốt deletion/tombstone semantics theo local-first.

**Blockers:**
- `BLK-001` đến `BLK-003` vẫn mở; P00-T01 chuyển `BLOCKED` cho đến khi P00-T07 được chốt.

**Next action:**
- `P00-T07` — chuẩn bị và xin phê duyệt technical baseline cho target framework, desktop UI và root namespace.

---

## 2026-07-27 — Codex — P00-T11

**Goal:** Chia từng execution phase thành các task nhỏ, có thể giao và kiểm chứng độc lập.

**Changes:**
- Tạo 8 file `TASKS.md`, một backlog chi tiết cho mỗi phase P00–P07.
- Giữ nguyên 119 Task ID để không phá dependency graph hoặc task board.
- Mỗi task có size tối đa 2 ngày, trạng thái, single outcome, dependency và acceptance/evidence.
- Liên kết từng phase `README.md` tới backlog chi tiết tương ứng.

**Verification:**
- Small-task validator → PASS.
- 8 task backlog files.
- 119 README task rows khớp chính xác 119 detailed task rows.
- 119 Task ID duy nhất; không orphan/missing task.
- Tất cả size nằm trong `0.5d` đến `2d`.
- Status enum và task dependency references hợp lệ.

**Decisions:**
- Phase `README.md` giữ source of truth về goal/scope/rules/gates.
- Phase `TASKS.md` giữ operational backlog; không được nới phase contract.
- Không tạo ID mới khi task hiện có đã đủ nhỏ; bổ sung size/status/acceptance để tránh dependency drift.

**Blockers:**
- Không có blocker mới.
- `BLK-001` đến `BLK-003` vẫn chờ P00-T07.

**Next action:**
- `P00-T07` — chốt target framework, desktop UI và root namespace.

## 2026-07-27 — Codex — P00-T13

**Goal:** Tạo một task specification độc lập cho từng Task ID trong execution roadmap.

**Changes:**
- Tạo 120 file task tại `plans/<phase>/tasks/Pxx-Tyy.md` và 8 file chỉ mục `tasks/README.md`.
- Mỗi task file có contract, phạm vi, source of truth, rule traceability, precondition, acceptance criteria, evidence, test plan, risk, stop condition và handoff.
- Liên kết toàn bộ Task ID trong 8 file `TASKS.md` tới task specification tương ứng.
- Thêm `scripts/Generate-TaskFiles.ps1` để tái tạo có kiểm soát; mặc định không ghi đè file hiện hữu nếu thiếu `-Force`.
- Thêm `P00-T13`, chuyển dependency của `P00-T12` sang `P00-T13`, và đồng bộ planning/execution/AI handoff state.

**Verification:**
- Lần chạy trực tiếp đầu tiên bằng Windows PowerShell 5.1 → FAIL do script UTF-8 không BOM bị đọc sai; không có task file nào được tạo ở lần này.
- Chuyển script sang UTF-8 BOM và chạy `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts\Generate-TaskFiles.ps1" -Force` → PASS; tạo 120 task files trong 8 phase.
- Task-file validator → PASS: 120 backlog links, 120 individual task files, 8 indexes, đủ 13 heading bắt buộc, metadata khớp backlog, dependency hợp lệ, mọi local Markdown link resolve và không còn placeholder.

**Decisions:**
- Phase `README.md` tiếp tục là source of truth về phase contract.
- Phase `TASKS.md` là operational backlog và nơi theo dõi trạng thái.
- File `tasks/Pxx-Tyy.md` là execution contract chi tiết của từng task; thay đổi status phải được đồng bộ với backlog.
- Task specification được sinh từ phase contract và backlog để hạn chế drift, sau đó có thể được làm giàu thủ công cho từng task.

**Blockers:**
- Không có blocker mới.
- `BLK-001` đến `BLK-003` vẫn chờ quyết định tại `P00-T07`.

**Next action:**
- `P00-T07` — chốt target framework, desktop UI và root namespace.

---

## 2026-07-27 — Codex — P00-T05

**Goal:** Viết Git flow tương ứng P00–P07 và gắn branch/PR contract vào từng individual task specification.

**Changes:**
- Mở rộng `docs/07-release/GIT_FLOW.md` thành policy trung tâm cho phase branch, task branch, PR, merge, release candidate, hotfix và recovery.
- Tạo 8 file `plans/<phase>/GIT_FLOW.md`, mỗi file có phase branch contract, task branch map, phase gate và close evidence.
- Bổ sung mục `Git workflow` cho 120 task files và liên kết phase flow từ phase README/task index.
- Cập nhật generator để sinh task/phase Git contracts nhất quán và thêm `scripts/Test-TaskGitFlow.ps1`.
- Ghi `DEC-006` ở trạng thái `Proposed`; chuyển P00-T05 sang `REVIEW`, không xem bản draft là approval.

**Verification:**
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts\Generate-TaskFiles.ps1" -Force` → PASS; 120 task files và 8 phase Git flows.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts\Test-TaskGitFlow.ps1"` → PASS; 8 phase, 120 backlog tasks, 120 task Git contracts, 8 indexes, branch/status/target/link và local Markdown links hợp lệ.
- Một truy vấn phụ `rg` dùng wildcard path kiểu Unix → FAIL trên Windows; chạy lại bằng `rg ... plans -g "README.md"` → PASS và xác nhận đủ 8 phase README links.
- Một truy vấn trạng thái `rg` có backtick trong double-quoted PowerShell pattern → FAIL do quoting; chạy lại bằng single-quoted pattern → PASS và xác nhận P00-T05/DEC-006/NEXT_ACTION đồng bộ.
- Không chạy build/test source vì repository chưa có solution và thay đổi phiên này chỉ là planning/process documentation.

**Decisions:**
- Đề xuất dùng một phase integration branch tại một thời điểm, task branch ngắn hạn và phase gate PR về `main`.
- Task PR dùng squash merge; phase/release PR dùng merge commit để giữ phase boundary.
- P07 chỉ tạo `release/{semver}` sau scope/version approval; workflow chưa có hiệu lực cho đến khi Product Owner phê duyệt `DEC-006`.

**Blockers:**
- Không có blocker kỹ thuật mới.
- P00-T05 chờ Product Owner review; repository chưa có Git metadata và không được tự khởi tạo.
- `BLK-001` đến `BLK-003` vẫn chờ P00-T07.

**Next action:**
- Product Owner review `docs/07-release/GIT_FLOW.md`; approve hoặc yêu cầu chỉnh sửa `DEC-006`.

---

## 2026-07-27 — Codex — P00-T14

**Goal:** Sửa bộ engineering prompts để phản ánh đúng product, architecture, security, phase/task workflow và evidence contract của Pew Pew Assistant.

**Changes:**
- Tạo `prompts/README.md` với project baseline, prompt routing, `{TOKEN}` schema, context order và output contract.
- Viết lại 9 prompt cho bootstrap, plan, implement, review, debug, refactor, security review, handoff và release.
- Bổ sung project guardrails: Windows-first, Modular Monolith/Clean Architecture, Local–Cloud Hybrid, Private Mode, default deny, Policy-before-Action, Level 3 deny và lifecycle/dependency gates.
- Thêm `scripts/Test-ProjectPrompts.ps1` để kiểm tra prompt inventory, schema, project concepts, legacy placeholder và local links.
- Thêm P00-T14, cập nhật dependency của phase verification và tái sinh individual task/Git contracts.

**Verification:**
- Các lần chạy validator đầu → FAIL do PowerShell provider filter không hỗ trợ glob character class như dự kiến và contract phrase chưa xuất hiện chính xác; đã sửa enumeration/case handling và thêm explicit phase-integration rule.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts\Test-ProjectPrompts.ps1"` → PASS; 9 prompts, usage guide, required structure, project coverage, token schema, no legacy placeholders và local links.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts\Generate-TaskFiles.ps1" -Force` → PASS; 121 task files và 8 phase Git flows.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts\Test-TaskGitFlow.ps1"` → PASS; 121 backlog/task/Git contracts đồng bộ.
- Không chạy source build/test vì repository chưa có solution; thay đổi là planning/operational documentation và validators.

**Decisions:**
- Prompt dùng `{TOKEN}` có tài liệu thay vì angle-bracket placeholder mơ hồ.
- Prompt chỉ điều phối việc đọc source of truth; không sao chép để thay thế business/lifecycle/dependency documents.
- Framework/UI/namespace chưa accepted không được hard-code vào implement prompt.
- Mỗi mode có stop condition và output/evidence contract riêng.

**Blockers:**
- Không có blocker mới.
- P00-T05/DEC-006 vẫn chờ Product Owner review.
- `BLK-001` đến `BLK-003` vẫn chờ P00-T07.

**Next action:**
- Product Owner review `docs/07-release/GIT_FLOW.md`; sau approval chuyển P00-T05 `DONE`, rồi tiếp tục P00-T07.

---

## 2026-07-27 — Codex — P00-T15

**Goal:** Reconcile toàn bộ lỗi cấu trúc/readiness có thể kiểm chứng và chuẩn bị repository cho bước phê duyệt trước implementation.

**Changes:**
- Sửa Product/Domain references, khóa ACH-001/ACH-002 cho đến explicit approval và chuyển RSK-005 về `Mitigating`.
- Tạo ADR-001..005 từ DEC-001..005 đã `Accepted`; thêm DEC-007 cho unified Session Result/output contract.
- Đồng bộ output contract giữa `AGENTS.md`, `.ai/OUTPUT_CONTRACT.md` và prompt usage.
- Bổ sung explicit `Mode`/`Risk` cho 123 task; sửa generator để không suy đoán metadata, không ghi đè file unmanaged và sinh acceptance/evidence nhất quán với status.
- Tạo `scripts/Test-ManagementStructure.ps1`, `scripts/Test-RepositoryReadiness.ps1` và safe `.gitignore`.
- Cập nhật README, task board, working memory, handoff, changelog và next action.
- Chuyển P00-T15 sang `DONE`; chuyển P00-T16 sang `READY` để Product Owner quyết định ba baseline đang `Proposed`.

**Verification:**
- Lần parse đầu của readiness script → FAIL do Windows PowerShell 5.1 đọc UTF-8 không BOM với literal có dấu; đã thay parser logic bằng ASCII-safe source.
- `scripts/Generate-TaskFiles.ps1 -Force -AdoptExisting` → PASS; one-time adoption tạo 123 task files và 8 phase Git flows có generated marker.
- `scripts/Generate-TaskFiles.ps1 -Force` → PASS sau khi đồng bộ P00-T15/P00-T16.
- PowerShell parser → PASS cho toàn bộ 5 file `.ps1`.
- `scripts/Test-ManagementStructure.ps1` → PASS; 45 required files, 14 directories, 8 phases, 208 Markdown files, ADR/reference/link checks pass.
- `scripts/Test-TaskGitFlow.ps1` → PASS; 123 backlog tasks/files/Git contracts, 123 explicit mode/risk, 5 `DONE` task acceptance sets và generated-file guard pass.
- `scripts/Test-ProjectPrompts.ps1` → PASS; 9 prompts, project contract, token, placeholder và link checks pass.
- Lần chạy readiness sau khi thêm commit detection → FAIL do native `git` stderr được PowerShell nâng thành error trên repository chưa có commit; đã thay bằng read-only `.git/HEAD`/refs inspection.
- `scripts/Test-RepositoryReadiness.ps1` → PASS với `IMPLEMENTATION_READY=NO`, `GIT_HAS_COMMIT=NO`, 0 solution/project/CI/architecture-test artifacts và 4 open blockers.
- `git status --short --branch` → PASS; nhánh `main` chưa có commit, toàn bộ repository đang untracked.
- Không chạy `dotnet build`/test vì chưa có `.sln` hoặc `.csproj`; readiness ghi rõ giới hạn này.

**Decisions:**
- Không tự phê duyệt Product Scope, Business Rules, Entity Lifecycles hoặc DEC-006.
- Generated task/index/phase-Git files chỉ là view; source chỉnh sửa là phase `README.md` và `TASKS.md`.
- Git mapping tiếp tục là draft cho đến khi DEC-006 được Product Owner chấp thuận.

**Blockers:**
- BLK-004: ba Product/Domain baselines vẫn `Proposed`.
- BLK-001..003: target framework, desktop UI và root namespace chưa chốt.
- DEC-006/P00-T05 vẫn chờ Product Owner review.
- Repository chưa có commit nền, source solution, CI hoặc architecture tests.

**Next action:**
- P00-T16 — Product Owner approve-as-is, yêu cầu sửa hoặc từ chối riêng Project Scope, Business Rules và Entity Lifecycles.

---

## 2026-07-28 — Codex — P00-T14 follow-up

**Goal:** Rút gọn ID input trong prompt planning/implement để người dùng dễ copy-paste mà không thay đổi project guardrail.

**Changes:**
- `prompts/01-plan-phase.md`: thay `{PLANNING_GOAL}`/`{CONSTRAINTS}` bằng ID `G00`/`C00` và thay placeholder Phase viết cứng bằng `{PHASE_ID}`/`{PHASE_PATH}`.
- `prompts/02-implement-task.md`: dùng ID `C00` cho ràng buộc bổ sung.
- `prompts/GOAL_CONSTRAINT_MAP.md`: tạo map riêng chứa goal/constraint IDs cho toàn bộ P00–P07 (`G00`–`G07`, `C00`–`C07`); planning/implement prompts resolve map trước khi làm việc.
- `prompts/README.md`: liên kết map và bổ sung cách dùng ID.
- `scripts/Test-ProjectPrompts.ps1`: cho phép hai token mới.

**Verification:**
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts\Test-ProjectPrompts.ps1"` → PASS; 9 prompts, token schema, links và project contract hợp lệ.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "scripts\Test-ManagementStructure.ps1"` → PASS; structure, ADR/reference và local links hợp lệ.

**Decisions:**
- `G00` và `C00` là ID input, được resolve từ map; không phải task ID hay decision ID.
- Không thay đổi scope, Git policy hoặc status của P00-T16.

**Next action:**
- P00-T16 vẫn chờ explicit Product Owner decision cho Scope, Business Rules và Entity Lifecycles.

---

## 2026-07-28 — Codex — P00-T07

**Goal:** Chốt target framework, desktop UI và root namespace trước khi tạo solution skeleton.

**Changes:**
- Ghi `DEC-008` và `ADR-008-TECHNICAL-BASELINE.md` theo quyết định Product Owner: `net10.0` core, `net10.0-windows` cho desktop/Windows boundary, Avalonia, `PewPew` root namespace.
- Đóng `BLK-001` đến `BLK-003`; chuyển P00-T07 sang `DONE` trong phase backlog và task board.
- Đồng bộ working memory, handoff và changelog; tái sinh task views từ source planning.

**Verification:**
- `Generate-TaskFiles.ps1 -Force` → PASS; tái sinh 123 task views và 8 phase Git flows.
- `Test-ManagementStructure.ps1` → PASS; ADR/reference/link checks pass.
- `Test-TaskGitFlow.ps1` → PASS; 123 task/Git contracts và 6 DONE acceptance sets pass.
- `Test-RepositoryReadiness.ps1` → PASS; 1 blocker còn mở (`BLK-004`).
- `Test-ProjectPrompts.ps1` → FAIL ngoài scope P00-T07: validator yêu cầu `P04`/`P05` trong prompt files nhưng bỏ qua `GOAL_CONSTRAINT_MAP.md`, nơi hai ID này được định nghĩa.
- Build/test: N/A cho P00-T07 vì task không tạo solution hoặc source project.

**Decisions:**
- Product Owner đã xác nhận technical baseline trong hội thoại ngày 2026-07-28.

**Blockers:**
- `BLK-004` vẫn mở; Product Scope, Business Rules và Entity Lifecycles vẫn cần explicit approval tại P00-T16.
- `DEC-006` vẫn Proposed; không commit/push/branch workflow.

**Next action:**
- `P00-T16` — phê duyệt Product/Domain baseline; sau đó re-evaluate P00-T01.

---

## 2026-07-28 — Codex — P00-T16

**Goal:** Ghi nhận explicit Product Owner approval cho Product Scope, Business Rules và Entity Lifecycles.

**Changes:**
- Ghi `DEC-009` và `ADR-009-PRODUCT-DOMAIN-BASELINE-APPROVAL.md` theo xác nhận approve-as-is của Product Owner.
- Đổi metadata ba baseline sang `Approved`; mở ACH-001 và ACH-002; đóng BLK-004.
- Chuyển P00-T16 sang `DONE`, đồng bộ task board, handoff, working memory, next action và changelog.
- Sửa `Test-ProjectPrompts.ps1` để coverage kiểm tra cả `GOAL_CONSTRAINT_MAP.md`, là file đã được prompt README chỉ định.

**Verification:**
- Regenerate task views và toàn bộ management/task/Git/prompt/readiness validators được chạy sau thay đổi.
- Build/test: N/A cho P00-T16 vì task chỉ phê duyệt và đồng bộ documentation baseline.

**Decisions:**
- Product Owner approve-as-is `PROJECT_SCOPE.md`, `BUSINESS_RULES.md` và `ENTITY_LIFECYCLES.md` trong hội thoại ngày 2026-07-28.

**Blockers:**
- Không còn blocker mở trong `.ai/BLOCKERS.md`.
- `DEC-006` vẫn Proposed nên Git branch/commit/PR flow chưa có hiệu lực.

**Next action:**
- `P00-T05` — Product Owner review Git workflow policy `DEC-006`.

---

## 2026-07-28 — Codex — P00-T05

**Goal:** Chốt policy Git/release phase-aligned trước khi dùng branch, commit hoặc PR workflow.

**Changes:**
- Product Owner approved `DEC-006` as-is; đổi policy status sang Accepted.
- Chuyển P00-T05 sang `DONE`; tái sinh task/phase Git views để bỏ draft mapping.
- Chuyển P00-T01 sang `READY` vì technical, Product/Domain và Git policy prerequisites đã có evidence.
- Đồng bộ task board, AI state, handoff và changelog.

**Verification:**
- Generator và management/task-Git/prompt/readiness validators được chạy sau thay đổi.
- Build/test: N/A cho P00-T05 vì task chỉ phê duyệt policy.

**Decisions:**
- Product Owner approved `DEC-006` in the 2026-07-28 conversation.

**Blockers:**
- Không còn decision blocker mở; repository vẫn chưa có baseline commit, solution, CI hoặc architecture tests.

**Next action:**
- `P00-T01` — tạo solution skeleton và project reference graph.

---

## 2026-07-28 — Codex — P00-T01

**Goal:** Tạo solution skeleton và project reference graph tối thiểu theo `DEC-008` và `DR-001`–`DR-011`.

**Changes:**
- Tạo `PewPew.sln`, `global.json` (SDK 10.0.204) và 10 project `PewPew.*` với assembly marker tối thiểu.
- Giữ SharedKernel, Domain, Contracts, Application, Infrastructure, Persistence, API và Worker trên `net10.0`; giới hạn `net10.0-windows` ở DeviceAgent và Desktop.
- Tạo Desktop Avalonia tối thiểu, pin `Avalonia` và `Avalonia.Desktop` 12.1.0; Desktop chỉ tham chiếu Contracts.
- Thiết lập references đúng matrix: Domain → SharedKernel; Application → Domain/SharedKernel; Infrastructure/Persistence → Application/Domain/SharedKernel; API chỉ composition references; DeviceAgent/Worker là outer runtime.

**Verification:**
- `dotnet build PewPew.sln --configuration Release --nologo` → PASS ngoài sandbox: 0 warning, 0 error.
- `dotnet list <project> reference` cho toàn bộ 10 project → PASS: graph khớp `DEPENDENCY_RULES.md`.
- `scripts/Test-TaskGitFlow.ps1` → PASS.
- `scripts/Test-ProjectPrompts.ps1` → PASS.
- Architecture test/security gate → N/A: P00-T02/P00-T08 chưa triển khai; không tắt hay bypass gate.
- Lần clean build đầu trong sandbox → FAIL do `AvaloniaStatsTask` không được ghi telemetry log ngoài workspace; build lại ngoài sandbox theo quyền đã cấp → PASS.

**Decisions:**
- Không có product, security hoặc architecture decision mới. Avalonia 12.1.0 được pin trong boundary Desktop theo `DEC-008`.

**Blockers:**
- Không có blocker mở. CI, architecture tests và secret/config baseline là các task P00 còn lại, không phải blocker của P00-T01.

**Next action:**
- `P00-T02` — thêm architecture tests cho DR-001, DR-002, DR-005 và DR-011.

---

## 2026-07-28 — Codex — Development launcher follow-up

**Goal:** Cho phép developer chạy Desktop shell từ repository root bằng đúng lệnh `dotnet watch run`.

**Changes:**
- Thêm root executable `PewPew.csproj` chỉ compile `Program.cs` và gọi public `PewPew.Desktop.DesktopHost`.
- Giữ UI implementation ở `src/PewPew.Desktop`; launcher không tham chiếu Domain, Application, Persistence hoặc API.
- Thêm hướng dẫn chạy và giới hạn Hot Reload vào `README.md`.

**Verification:**
- `dotnet build PewPew.sln --configuration Release --nologo` → PASS ngoài sandbox: 0 warning, 0 error.
- `dotnet watch run --no-hot-reload` từ root → PASS: watcher chạy và `PewPew.exe` được khởi động; tiến trình kiểm tra đã được dừng.

**Next action:**
- `P00-T02` vẫn là task READY tiếp theo.

---

## 2026-07-28 — Codex — P00-T02

**Goal:** Bảo vệ dependency graph bằng architecture tests có positive suite và negative fixture.

**Changes:**
- Tạo `tests/PewPew.Architecture.Tests` với Microsoft.NET.Test.Sdk 18.8.1, xUnit v3 3.2.2 và VSTest adapter 3.1.5.
- Thêm validator đọc `ProjectReference` từ project graph thật và kiểm tra DR-001 (Domain), DR-002 (Application), DR-005 (presentation → Persistence) và DR-011 (cycle).
- Thêm fixture test-only Application → Infrastructure để chứng minh DR-002 bị phát hiện mà không tạo dependency sai trong source.

**Verification:**
- `dotnet test tests/PewPew.Architecture.Tests/PewPew.Architecture.Tests.csproj --no-restore --nologo` → PASS: 2/2.
- `dotnet build PewPew.sln --configuration Release --nologo` → PASS: 0 warning, 0 error.
- `dotnet test PewPew.sln --configuration Release --no-build --nologo` → PASS: 2/2.
- `scripts/Test-ManagementStructure.ps1` và `scripts/Test-TaskGitFlow.ps1` → PASS.
- Initial sandbox restore → FAIL (TLS/NuGet sandbox restriction); rerun outside sandbox → restore PASS. Initial fixture path test → FAIL, corrected to repository-relative fixture path → PASS.

**Security / lifecycle:**
- Không đổi entity lifecycle, permission, action, secret hoặc external integration. Security gate ngoài scope là N/A.

**Next action:**
- `P00-T03` — thiết lập CI restore/build/test.

---

## 2026-07-28 — Codex — P00-T03

**Goal:** Thiết lập CI foundation chạy restore, Release build và test, với quyền tối thiểu.

**Changes:**
- Tạo `.github/workflows/ci.yml`: Windows runner, `actions/checkout@v6`, `actions/setup-dotnet@v5`, SDK 10.0.204, `contents: read`, timeout 15 phút và không secret/deploy.
- Tạo `scripts/Invoke-Ci.ps1` cho cùng pipeline restore → build → test; native command failure luôn throw/non-zero.
- Tạo `scripts/Test-CiWorkflow.ps1` kiểm tra workflow contract và từ chối secret/deployment.

**Verification:**
- `scripts/Test-CiWorkflow.ps1` → PASS.
- `scripts/Invoke-Ci.ps1` → PASS: Release build 0 warning/error; tests 2/2 PASS.
- `scripts/Invoke-Ci.ps1 -SimulateFailure` → expected FAIL, exit 1; chứng minh failure propagation.
- GitHub Actions clean run → NOT RUN: remote có nhưng repository không có commit baseline; commit/push chưa được Product Owner yêu cầu hoặc cho phép.

**Security / lifecycle:**
- Không thay đổi lifecycle, permission hay action. Workflow không dùng secret, deployment, quyền ghi repository hoặc quyền cao.

**Status / next action:**
- P00-T03 ở `VERIFY`, không `DONE` cho đến khi có GitHub Actions URL/status PASS sau authorization commit/push.
