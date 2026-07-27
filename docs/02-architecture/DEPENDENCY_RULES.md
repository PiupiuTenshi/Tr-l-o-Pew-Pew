# Dependency Rules — Pew Pew Assistant

> **Architecture style:** 3-Tier Deployment Architecture kết hợp Clean Architecture theo module
>
> **Nguyên tắc cốt lõi:** UI/Client không truy cập trực tiếp Database. Domain không phụ thuộc Framework. Infrastructure triển khai các abstraction được định nghĩa ở Application hoặc Domain.

---

## 1. Mục tiêu kiến trúc

Kiến trúc của Pew Pew Assistant phải đáp ứng đồng thời các yêu cầu:

- Hoạt động đa nền tảng: Windows, Web, Mobile, Browser Extension và Home Assistant.
- Hỗ trợ Local AI, Cloud AI và Hybrid Mode.
- Cho phép chạy nền, wake word, terminal automation và UI automation.
- Giới hạn quyền truy cập của AI và plugin.
- Dễ thay đổi AI provider, database, message broker và công nghệ giao diện.
- Có thể tách một số module thành service độc lập khi hệ thống phát triển.
- Không để business rule phụ thuộc trực tiếp vào EF Core, ASP.NET Core, hệ điều hành hoặc SDK bên thứ ba.

---

## 2. Mô hình 3-Tier tổng thể

```text
┌──────────────────────────────────────────────────────────────┐
│ TIER 1 — PRESENTATION / CLIENT                               │
│                                                              │
│ Desktop App | Web App | Mobile App | Browser Extension       │
│ Voice UI    | Tray UI | CLI Client | Home Assistant UI       │
└──────────────────────────────┬───────────────────────────────┘
                               │ HTTPS / WebSocket / gRPC
                               v
┌──────────────────────────────────────────────────────────────┐
│ TIER 2 — APPLICATION / BUSINESS                              │
│                                                              │
│ API Layer                                                    │
│ Application Layer                                            │
│ Domain Layer                                                 │
│ Infrastructure Adapters                                      │
│ Background Workers / Device Agent / Policy Engine            │
└──────────────────────────────┬───────────────────────────────┘
                               │ Repository / Queue / Cache
                               v
┌──────────────────────────────────────────────────────────────┐
│ TIER 3 — DATA / PERSISTENCE                                  │
│                                                              │
│ SQL Database | Vector Store | Local SQLite | Redis | Files    │
│ Audit Store  | Secret Vault | Object Storage | Outbox         │
└──────────────────────────────────────────────────────────────┘
```

### 2.1 Tier 1 — Presentation Tier

Chịu trách nhiệm tương tác với người dùng và thiết bị giao diện.

Bao gồm:

- Windows Desktop App.
- Web Application.
- Mobile Companion App.
- Browser Extension.
- Voice overlay, tray icon và push-to-talk UI.
- CLI client dành cho developer.
- Home Assistant dashboard hoặc integration UI.

Tier này chỉ được:

- Nhận input.
- Hiển thị output.
- Thu thập context được người dùng cho phép.
- Gọi API hoặc local IPC endpoint.
- Quản lý UI state ngắn hạn.

Tier này không được:

- Truy cập trực tiếp database.
- Chứa business rule cốt lõi.
- Tự quyết định permission cho hành động nhạy cảm.
- Tự thực thi terminal command ngoài Action Engine.
- Lưu secret dạng plain text.

### 2.2 Tier 2 — Application / Business Tier

Đây là tầng xử lý trung tâm của hệ thống.

Bao gồm các layer nội bộ:

1. API Layer.
2. Application Layer.
3. Domain Layer.
4. Infrastructure Layer.
5. Background Processing Layer.
6. Security and Policy Layer.

Tier này chịu trách nhiệm:

- Xác thực và phân quyền.
- Phân tích intent.
- Điều phối use case.
- Quản lý action plan.
- Kiểm tra permission và risk level.
- Quản lý memory.
- Điều phối Local AI và Cloud AI.
- Thực thi skill thông qua adapter.
- Điều phối terminal workflow.
- Đồng bộ đa thiết bị.
- Ghi audit log.

### 2.3 Tier 3 — Data Tier

Chịu trách nhiệm lưu trữ và truy xuất dữ liệu.

Bao gồm:

- SQL Server hoặc PostgreSQL cho cloud data.
- SQLite cho local device data.
- Vector database cho semantic memory.
- Redis cho cache, distributed lock và temporary state.
- File system hoặc object storage cho tài liệu.
- Secret store cho token và API key.
- Append-only audit store.
- Transactional outbox.

Tier này không được chứa business rule nghiệp vụ. Database constraint chỉ là lớp bảo vệ bổ sung, không thay thế Domain rule.

---

## 3. Các project/layer đề xuất

```text
src/
├── PewPew.Api/
├── PewPew.Application/
├── PewPew.Domain/
├── PewPew.Infrastructure/
├── PewPew.Persistence/
├── PewPew.Contracts/
├── PewPew.SharedKernel/
├── PewPew.DeviceAgent/
├── PewPew.Worker/
├── PewPew.Desktop/
├── PewPew.Web/
├── PewPew.Mobile/
├── PewPew.BrowserExtension/
└── PewPew.HomeAssistant/

tests/
├── PewPew.Domain.Tests/
├── PewPew.Application.Tests/
├── PewPew.Infrastructure.Tests/
├── PewPew.Api.IntegrationTests/
├── PewPew.Security.Tests/
└── PewPew.EndToEndTests/
```

---

## 4. Vai trò từng layer

## 4.1 `PewPew.Api`

Chịu trách nhiệm tiếp nhận request và trả response.

Được chứa:

- Controller, Minimal API endpoint hoặc GraphQL endpoint.
- Authentication middleware.
- Authorization policy.
- Request validation ở boundary.
- Exception mapping.
- Rate limiting.
- API versioning.
- Swagger/OpenAPI.
- WebSocket hoặc SignalR endpoint.
- Mapping giữa API contract và Application command/query.

Không được chứa:

- Business rule.
- EF Core query trực tiếp.
- Logic chọn AI provider.
- Logic xử lý memory.
- Logic permission ở mức nghiệp vụ.
- Code điều khiển terminal hoặc browser.

Dependency hợp lệ:

```text
Api -> Application
Api -> Contracts
Api -> Infrastructure   chỉ tại Composition Root
Api -> Persistence      chỉ tại Composition Root
```

---

## 4.2 `PewPew.Application`

Chịu trách nhiệm thực hiện use case.

Được chứa:

- Command và Query.
- Handler.
- Application service.
- DTO nội bộ.
- Use case validation.
- Transaction boundary.
- Orchestration.
- Interface cho repository, AI provider, message bus, clock, file storage.
- Mapping giữa Domain model và Application result.
- Authorization rule theo use case.

Ví dụ use case:

- `ProcessVoiceCommand`
- `CreateActionPlan`
- `ConfirmActionPlan`
- `ExecuteTrustedRoutine`
- `StoreMemory`
- `SyncDeviceMemory`
- `SendOutboundMessage`
- `RunTerminalWorkflow`
- `ControlHomeAssistantEntity`

Không được chứa:

- EF Core implementation.
- SQL query cụ thể.
- HTTP client cụ thể.
- Windows API cụ thể.
- Browser DOM implementation.
- Secret thực tế.

Dependency hợp lệ:

```text
Application -> Domain
Application -> SharedKernel
Application -> Contracts   khi cần dùng shared contract ổn định
```

Application không được tham chiếu ngược đến Api, Infrastructure hoặc Persistence.

---

## 4.3 `PewPew.Domain`

Chứa business logic cốt lõi và state machine của hệ thống.

Được chứa:

- Entity.
- Aggregate Root.
- Value Object.
- Domain Event.
- Domain Service thuần nghiệp vụ.
- Business Rule.
- State transition.
- Domain exception.
- Specification thuần domain.
- Enum có ý nghĩa nghiệp vụ.

Các aggregate quan trọng:

- `UserAccount`
- `AssistantProfile`
- `DeviceNode`
- `InteractionSession`
- `ActionPlan`
- `ActionTask`
- `PermissionGrant`
- `ConfirmationRequest`
- `MemoryRecord`
- `RoutineDefinition`
- `RoutineRun`
- `TerminalWorkflowDefinition`
- `SkillPackage`
- `SecurityIncident`

Domain phải:

- Không phụ thuộc framework.
- Không phụ thuộc EF Core.
- Không phụ thuộc ASP.NET Core.
- Không phụ thuộc SDK AI.
- Không đọc environment variable.
- Không gọi network.
- Không truy cập filesystem.
- Không dùng system clock trực tiếp nếu thời gian là yếu tố nghiệp vụ.

Dependency hợp lệ:

```text
Domain -> SharedKernel
```

Domain là layer độc lập nhất.

---

## 4.4 `PewPew.Infrastructure`

Triển khai các adapter kỹ thuật và integration bên ngoài.

Được chứa:

- OpenAI, Gemini hoặc local model adapter.
- Speech-to-text adapter.
- Text-to-speech adapter.
- Wake word engine adapter.
- Browser automation adapter.
- Windows automation adapter.
- Home Assistant client.
- Message platform client.
- File storage adapter.
- Message broker adapter.
- Encryption service.
- Secret vault adapter.
- Email, SMS hoặc push notification adapter.
- System resource monitor.

Infrastructure phải implement interface được định nghĩa ở Application hoặc Domain.

Dependency hợp lệ:

```text
Infrastructure -> Application
Infrastructure -> Domain
Infrastructure -> SharedKernel
```

Infrastructure không được gọi trực tiếp API controller hoặc UI.

---

## 4.5 `PewPew.Persistence`

Chịu trách nhiệm lưu trữ dữ liệu.

Được chứa:

- `DbContext`.
- EF Core configuration.
- Migration.
- Repository implementation.
- Unit of Work implementation.
- Database transaction.
- Query optimization.
- Outbox persistence.
- Entity-to-table mapping.
- Local SQLite implementation.
- Vector store implementation.

Dependency hợp lệ:

```text
Persistence -> Application
Persistence -> Domain
Persistence -> SharedKernel
```

Không được chứa:

- Business rule.
- Controller.
- Use case orchestration.
- Permission decision.
- UI logic.

---

## 4.6 `PewPew.Contracts`

Chứa các contract ổn định dùng ở boundary.

Được chứa:

- API request/response.
- Integration event contract.
- WebSocket message contract.
- Device command contract.
- Versioned message schema.

Không được chứa:

- Entity.
- EF Core annotation.
- Business rule.
- Service implementation.

Contracts phải được version hóa khi đã được external client sử dụng.

---

## 4.7 `PewPew.SharedKernel`

Chỉ chứa các primitive thật sự dùng chung.

Ví dụ:

- `EntityId`
- `Result<T>`
- `Error`
- `Guard`
- `IClock`
- `DomainEvent`
- `ValueObject`

Không được biến SharedKernel thành thư mục chứa mọi helper.

---

## 4.8 `PewPew.DeviceAgent`

Là runtime cục bộ trên máy người dùng.

Chịu trách nhiệm:

- Wake word.
- Push-to-talk.
- Local IPC.
- Device registration.
- Local command execution.
- UI accessibility.
- Browser extension bridge.
- Local model lifecycle.
- Resource budget.
- Emergency stop.

Device Agent không được tự quyết định business rule. Nó phải nhận `ActionTask` đã được Policy Engine phê duyệt.

Dependency hợp lệ:

```text
DeviceAgent -> Application abstractions
DeviceAgent -> Infrastructure
DeviceAgent -> Contracts
```

---

## 4.9 `PewPew.Worker`

Chạy các tác vụ nền:

- Memory summarization.
- Device synchronization.
- Outbox dispatching.
- Routine scheduling.
- Audit archival.
- Retry job.
- Cleanup job.
- Model preloading theo policy.

Worker không được bỏ qua Application use case để ghi trực tiếp Domain state.

---

## 5. Dependency direction tổng quát

```text
Presentation
     |
     v
Application
     |
     v
Domain

Infrastructure ------> Application abstractions
Persistence ---------> Application abstractions

Composition Root tạo object graph và nối implementation với interface.
```

Quy tắc bắt buộc:

```text
Dependency luôn hướng vào bên trong.
Domain không biết Infrastructure tồn tại.
Application không biết EF Core hoặc ASP.NET Core tồn tại.
UI không biết database schema tồn tại.
```

---

## 6. Dependency matrix

| Project nguồn | Có thể phụ thuộc | Không được phụ thuộc |
|---|---|---|
| Desktop/Web/Mobile | Contracts, SDK client | Domain, Persistence trực tiếp |
| Browser Extension | Contracts, browser SDK | Database, Domain trực tiếp |
| API | Application, Contracts | UI project |
| Application | Domain, SharedKernel | API, Infrastructure, Persistence |
| Domain | SharedKernel | Tất cả framework và outer layer |
| Infrastructure | Application, Domain, SharedKernel | API, UI |
| Persistence | Application, Domain, SharedKernel | API, UI |
| DeviceAgent | Contracts, Application abstraction, Infrastructure | Cloud DB trực tiếp |
| Worker | Application, Infrastructure, Persistence | UI |
| Tests | Project được test | Production project không liên quan |

---

## 7. Dependency rules bắt buộc

### DR-001 — Domain Independence

`PewPew.Domain` không được tham chiếu bất kỳ project kỹ thuật hoặc UI nào.

### DR-002 — Application Independence

`PewPew.Application` không được tham chiếu `PewPew.Api`, `PewPew.Infrastructure`, `PewPew.Persistence`, Desktop, Web hoặc Mobile.

### DR-003 — Interface Ownership

Interface phục vụ use case phải được đặt ở layer sở hữu nhu cầu, thường là Application.

Ví dụ:

```csharp
public interface IAssistantModelGateway
{
    Task<ModelResponse> GenerateAsync(ModelRequest request, CancellationToken ct);
}
```

Implementation nằm tại Infrastructure.

### DR-004 — Repository Ownership

Repository interface nằm tại Application hoặc Domain. Repository implementation nằm tại Persistence.

### DR-005 — No Direct Database Access from Presentation

Desktop, Web, Mobile, Browser Extension và API endpoint không được query database trực tiếp.

### DR-006 — API as Boundary

API chỉ chuyển đổi transport request thành Application command/query và chuyển kết quả thành transport response.

### DR-007 — No Framework Types in Domain

Entity và Value Object không được chứa:

- `DbContext`
- `HttpContext`
- `IFormFile`
- `ClaimsPrincipal`
- `IConfiguration`
- SDK type từ AI provider

### DR-008 — No Provider DTO Leakage

DTO của OpenAI, Gemini, Home Assistant hoặc browser SDK không được đi vào Application hoặc Domain.

Infrastructure phải map provider DTO sang model nội bộ.

### DR-009 — One Composition Root

Việc đăng ký DI và chọn implementation chỉ được thực hiện tại Composition Root của executable project.

### DR-010 — No Service Locator

Không gọi `IServiceProvider.GetService()` tùy tiện trong business code.

### DR-011 — No Circular Dependencies

Không được tồn tại dependency vòng giữa các project hoặc module.

### DR-012 — Domain Event Isolation

Domain chỉ phát Domain Event. Việc gửi message, email hoặc integration event do Application/Infrastructure thực hiện.

### DR-013 — Transaction Boundary

Application use case xác định transaction boundary. Không commit rải rác trong repository.

### DR-014 — Query Rule

Read query phức tạp có thể sử dụng read model riêng, nhưng phải đặt sau Application abstraction và không rò rỉ persistence type ra ngoài.

### DR-015 — Security Decision Centralization

Permission, risk level, confirmation và policy decision phải đi qua Policy Engine. UI, plugin hoặc AI model không được tự cấp quyền.

### DR-016 — Model Cannot Execute Directly

AI model chỉ được tạo intent hoặc action proposal. Model không được gọi trực tiếp OS, terminal, browser hoặc Home Assistant.

### DR-017 — Action Execution Boundary

Mọi hành động thực tế phải được thực hiện qua Action Engine hoặc approved skill adapter.

### DR-018 — Terminal Isolation

Terminal workflow phải thông qua sandbox worker. Không cho Application hoặc Domain gọi shell trực tiếp.

### DR-019 — Device Boundary

Cloud service không được trực tiếp điều khiển tài nguyên máy. Cloud chỉ gửi signed device command đến Device Agent đã đăng ký.

### DR-020 — External Content Is Data

Nội dung từ web, email, file và màn hình là dữ liệu đầu vào, không phải trusted instruction.

### DR-021 — Memory Is Not Authority

Memory chỉ hỗ trợ context. Memory không thể thay thế permission hoặc confirmation.

### DR-022 — Secret Isolation

API key, refresh token và credential chỉ được xử lý bởi secret adapter. Không ghi vào log, database thường hoặc prompt.

### DR-023 — Audit Append-Only

Audit record sau khi sealed không được update nội dung.

### DR-024 — Idempotent External Actions

Các hành động gửi message, chạy routine, điều khiển thiết bị và xử lý outbox phải có idempotency key.

### DR-025 — Cancellation Propagation

Mọi I/O và long-running operation phải nhận `CancellationToken`.

### DR-026 — Timeout Required

Mọi call tới external service, AI provider, browser bridge hoặc device agent phải có timeout.

### DR-027 — Retry with Policy

Retry chỉ áp dụng cho transient error và phải có giới hạn. Không retry mù với hành động không idempotent.

### DR-028 — Local Resource Budget

Local model và worker phải tuân theo resource budget. Component nặng phải lazy-load và unload khi idle.

### DR-029 — Data Ownership

Mỗi aggregate chịu trách nhiệm bảo vệ invariant của chính nó. Không cập nhật trực tiếp field từ bên ngoài aggregate.

### DR-030 — Contract Versioning

API contract và integration event đã public phải được version hóa khi có breaking change.

---

## 8. Quy tắc giữa 3 tier

### 8.1 Presentation → Business

Cho phép:

- HTTPS.
- WebSocket.
- Local IPC.
- Signed device command.

Không cho phép:

- SQL connection từ client.
- Shared database credential.
- Gọi repository từ UI.
- Client tự đặt `riskLevel`, `isTrusted` hoặc `permissionGranted`.

### 8.2 Business → Data

Cho phép:

- Repository abstraction.
- Unit of Work.
- Query service.
- Cache abstraction.
- Outbox abstraction.

Không cho phép:

- Domain entity phụ thuộc table name.
- Application handler chứa raw SQL tùy tiện.
- Infrastructure bypass transaction boundary.

### 8.3 Data → Business

Data tier chỉ trả dữ liệu qua contract nội bộ hoặc Domain model theo mapping được kiểm soát.

Data tier không được gọi ngược Application use case.

---

## 9. Module boundary đề xuất

Trong Business Tier, nên chia theo bounded context hoặc feature module.

```text
Modules/
├── Identity/
├── Devices/
├── Conversations/
├── Voice/
├── Memory/
├── Actions/
├── Permissions/
├── Routines/
├── Skills/
├── TerminalAutomation/
├── BrowserAutomation/
├── Messaging/
├── HomeAutomation/
├── AiOrchestration/
├── Audit/
└── Security/
```

Mỗi module nên có:

```text
<Module>/
├── Domain/
├── Application/
├── Infrastructure/
└── Contracts/
```

Module khác không được truy cập database table nội bộ của module.

Giao tiếp giữa module thông qua:

- Public Application interface.
- Domain event.
- Integration event.
- Explicit module contract.

---

## 10. Luồng xử lý chuẩn

### 10.1 Voice command

```text
Voice UI
  -> Device Agent
  -> Speech-to-Text Adapter
  -> API/Application Use Case
  -> Intent Orchestrator
  -> Domain Policy Evaluation
  -> Clarification hoặc Confirmation
  -> Action Engine
  -> Skill Adapter
  -> Verification
  -> Audit
  -> Response về UI
```

### 10.2 Terminal workflow

```text
User Request
  -> Application Use Case
  -> Workflow Definition Load
  -> Permission Check
  -> Confirmation
  -> Sandbox Worker
  -> Structured Command Adapter
  -> Process Monitor
  -> Result Verification
  -> Audit
```

### 10.3 Browser automation

```text
User Request
  -> Application
  -> Browser Action Plan
  -> Policy Engine
  -> Browser Extension Bridge
  -> DOM/Accessibility Adapter
  -> Target Verification
  -> Click/Input
  -> Result Verification
```

---

## 11. Rule cho local và cloud

### Local runtime

Local runtime được phép:

- Lắng nghe wake word.
- Chạy STT/TTS local.
- Quản lý local memory.
- Thực thi approved device action.
- Chạy local model theo resource budget.

Local runtime không được:

- Tự đồng bộ secret chưa mã hóa.
- Chấp nhận remote command chưa ký.
- Bỏ qua Policy Engine vì đang offline.

### Cloud runtime

Cloud runtime được phép:

- Đồng bộ dữ liệu.
- Cung cấp long-term memory.
- Gọi cloud AI.
- Điều phối nhiều thiết bị.

Cloud runtime không được:

- Có shell trực tiếp trên máy người dùng.
- Tự thực thi device action khi Device Agent chưa verify command.
- Gửi dữ liệu private mode ra ngoài.

---

## 12. Composition Root

Composition Root là nơi duy nhất nối interface với implementation.

Ví dụ:

```csharp
services.AddApplication();
services.AddInfrastructure(configuration);
services.AddPersistence(configuration);
services.AddSecurityPolicies();
services.AddDeviceCommunication();
```

`AddApplication()` không được gọi ngược `AddInfrastructure()`.

---

## 13. Dependency validation

CI phải kiểm tra dependency bằng automated architecture test.

Ví dụ với NetArchTest hoặc ArchUnitNET:

```csharp
[Fact]
public void Domain_Must_Not_Depend_On_Infrastructure()
{
    var result = Types
        .InAssembly(typeof(DomainAssemblyMarker).Assembly)
        .ShouldNot()
        .HaveDependencyOn("PewPew.Infrastructure")
        .GetResult();

    Assert.True(result.IsSuccessful);
}
```

Các rule cần test:

- Domain không phụ thuộc outer layer.
- Application không phụ thuộc Infrastructure/Persistence/API.
- Controller không gọi DbContext.
- Provider DTO không xuất hiện trong Application.
- UI không tham chiếu Persistence.
- Module không truy cập internal type của module khác.

---

## 14. Naming rules

| Loại | Quy tắc |
|---|---|
| Command | Động từ + đối tượng, ví dụ `ExecuteRoutineCommand` |
| Query | Mô tả dữ liệu cần lấy, ví dụ `GetDeviceStatusQuery` |
| Handler | Tên command/query + `Handler` |
| Domain event | Sự kiện quá khứ, ví dụ `ActionPlanConfirmed` |
| Integration event | Tên nghiệp vụ + `IntegrationEvent` |
| Repository | `I<Aggregate>Repository` |
| Adapter | `<Provider><Capability>Adapter` |
| API contract | `<Action>Request`, `<Action>Response` |
| Value object | Danh từ nghiệp vụ, không dùng hậu tố DTO |

---

## 15. Anti-pattern bị cấm

- Controller gọi `DbContext` trực tiếp.
- Domain entity dùng `[Table]`, `[Column]` hoặc provider-specific attribute.
- Application handler khởi tạo `HttpClient` trực tiếp.
- AI provider trả object SDK xuyên qua các layer.
- Service chung chứa hàng chục trách nhiệm.
- Repository trả `IQueryable` ra Presentation.
- UI truyền `isAdmin`, `isTrusted` hoặc `skipConfirmation` và server tin trực tiếp.
- Model AI tự tạo shell command và thực thi ngay.
- Plugin được chạy cùng quyền với core agent.
- Global static mutable state.
- SharedKernel chứa code nghiệp vụ của module.
- Circular reference giữa project.

---

## 16. Kiến trúc triển khai MVP đề xuất

```text
[Windows Desktop]
      |
      | Local IPC
      v
[Device Agent]
      |
      | HTTPS/WebSocket
      v
[ASP.NET Core API]
      |
      +--> Application
      +--> Domain
      +--> Infrastructure
      +--> Persistence
      |
      +--> SQL Database
      +--> Redis
      +--> Vector Store
      +--> AI Providers
      +--> Home Assistant
```

Trong MVP, Tier 2 có thể triển khai dưới dạng **Modular Monolith** để giảm độ phức tạp vận hành.

Không nên bắt đầu bằng microservices. Chỉ tách service khi có bằng chứng rõ ràng về:

- Nhu cầu scale độc lập.
- Security boundary riêng.
- Deployment lifecycle riêng.
- Fault isolation bắt buộc.
- Ownership bởi team khác.

---

## 17. Dependency Rule Summary

```text
1. Presentation chỉ gọi Application qua API hoặc IPC.
2. Application điều phối use case và chỉ phụ thuộc Domain.
3. Domain độc lập hoàn toàn với framework.
4. Infrastructure và Persistence triển khai abstraction hướng vào trong.
5. Database không bao giờ được client truy cập trực tiếp.
6. AI model chỉ đề xuất; Policy Engine quyết định; Action Engine thực thi.
7. Terminal, browser và device action phải qua adapter và sandbox.
8. Secret, permission, audit và confirmation là cross-cutting concern bắt buộc.
9. Không có dependency vòng.
10. Mọi dependency rule phải được kiểm tra tự động trong CI.
```

---

## 18. Cấu trúc reference đề xuất

```text
PewPew.Domain
  -> PewPew.SharedKernel

PewPew.Application
  -> PewPew.Domain
  -> PewPew.SharedKernel

PewPew.Infrastructure
  -> PewPew.Application
  -> PewPew.Domain
  -> PewPew.SharedKernel

PewPew.Persistence
  -> PewPew.Application
  -> PewPew.Domain
  -> PewPew.SharedKernel

PewPew.Api
  -> PewPew.Application
  -> PewPew.Contracts
  -> PewPew.Infrastructure      composition only
  -> PewPew.Persistence         composition only

PewPew.DeviceAgent
  -> PewPew.Contracts
  -> PewPew.Application abstractions
  -> PewPew.Infrastructure

PewPew.Desktop / Web / Mobile
  -> PewPew.Contracts
```

---

## 19. Quyết định kiến trúc chính

- Sử dụng 3-tier ở cấp triển khai.
- Sử dụng Clean Architecture ở bên trong Business Tier.
- Sử dụng Modular Monolith cho MVP.
- Windows-first cho Device Agent.
- Local database dùng SQLite.
- Cloud database ưu tiên PostgreSQL hoặc SQL Server.
- Mọi external integration đi qua adapter.
- Mọi hành động có side effect đi qua Policy Engine và Action Engine.
- Mọi dependency được xác minh bằng architecture tests.

