# PROJECT SCOPE

## Pew Pew Assistant — Trợ lý Trí tuệ Nhân tạo Cá nhân Đa nền tảng

| Thuộc tính | Giá trị |
|---|---|
| Tên dự án | Pew Pew Assistant |
| Loại sản phẩm | Personal AI Assistant & Device Automation Platform |
| Tài liệu | Project Scope |
| Phiên bản phạm vi | 1.0 |
| Trạng thái | Approved Scope Baseline — Product Owner, 2026-07-28 (`DEC-009`) |
| Tài liệu liên quan | `PROJECT_VISION.md` |
| Đối tượng sử dụng | Product Owner, System Analyst, Architect, Developer, Security Engineer, QA và AI coding agents |

> **Một người dùng – Một trợ lý – Mọi thiết bị – Luôn trong tầm kiểm soát.**

---

## Mục lục

1. [Mục đích tài liệu](#1-mục-đích-tài-liệu)
2. [Tóm tắt phạm vi](#2-tóm-tắt-phạm-vi)
3. [Mục tiêu dự án](#3-mục-tiêu-dự-án)
4. [Nguyên tắc xác định phạm vi](#4-nguyên-tắc-xác-định-phạm-vi)
5. [Đối tượng sử dụng và bên liên quan](#5-đối-tượng-sử-dụng-và-bên-liên-quan)
6. [Phạm vi sản phẩm tổng thể](#6-phạm-vi-sản-phẩm-tổng-thể)
7. [Phạm vi MVP](#7-phạm-vi-mvp)
8. [Phạm vi chức năng chi tiết](#8-phạm-vi-chức-năng-chi-tiết)
9. [Phạm vi theo nền tảng](#9-phạm-vi-theo-nền-tảng)
10. [Phạm vi dữ liệu và memory](#10-phạm-vi-dữ-liệu-và-memory)
11. [Phạm vi AI Local–Cloud Hybrid](#11-phạm-vi-ai-localcloud-hybrid)
12. [Phạm vi tự động hóa và terminal](#12-phạm-vi-tự-động-hóa-và-terminal)
13. [Phạm vi quan sát và điều khiển giao diện](#13-phạm-vi-quan-sát-và-điều-khiển-giao-diện)
14. [Phạm vi Home Assistant](#14-phạm-vi-home-assistant)
15. [Phạm vi bảo mật và quyền riêng tư](#15-phạm-vi-bảo-mật-và-quyền-riêng-tư)
16. [Yêu cầu phi chức năng](#16-yêu-cầu-phi-chức-năng)
17. [Ngoài phạm vi](#17-ngoài-phạm-vi)
18. [Deliverables](#18-deliverables)
19. [Lộ trình triển khai theo phase](#19-lộ-trình-triển-khai-theo-phase)
20. [Ưu tiên phạm vi theo MoSCoW](#20-ưu-tiên-phạm-vi-theo-moscow)
21. [Giả định](#21-giả-định)
22. [Ràng buộc](#22-ràng-buộc)
23. [Phụ thuộc](#23-phụ-thuộc)
24. [Rủi ro phạm vi](#24-rủi-ro-phạm-vi)
25. [Tiêu chí nghiệm thu MVP](#25-tiêu-chí-nghiệm-thu-mvp)
26. [Definition of Done](#26-definition-of-done)
27. [Quản lý thay đổi phạm vi](#27-quản-lý-thay-đổi-phạm-vi)
28. [Ma trận truy vết cấp cao](#28-ma-trận-truy-vết-cấp-cao)
29. [Tuyên bố chốt phạm vi](#29-tuyên-bố-chốt-phạm-vi)
30. [Bản rút gọn cho README](#30-bản-rút-gọn-cho-readme)

---

## 1. Mục đích tài liệu

Tài liệu này chuyển hóa `Project Vision` thành một phạm vi có thể:

- Phân tích thành requirement và use case.
- Thiết kế kiến trúc hệ thống.
- Chia phase phát triển.
- Giao nhiệm vụ cho developer hoặc AI coding agent.
- Kiểm soát scope creep.
- Xác định rõ những gì phải hoàn thành trong MVP.
- Xác định những chức năng chỉ được triển khai ở các giai đoạn sau.
- Làm cơ sở nghiệm thu sản phẩm.

Tài liệu không thay thế cho:

- Software Requirements Specification.
- System Architecture.
- Threat Model.
- API Specification.
- Test Plan.
- Release Plan.

Các tài liệu trên phải được xây dựng từ scope baseline này và không được tự ý mở rộng phạm vi nếu chưa có quyết định thay đổi.

---

## 2. Tóm tắt phạm vi

Pew Pew Assistant là một trợ lý AI cá nhân 1:1 có khả năng:

- Nhận lệnh bằng giọng nói, văn bản hoặc phím tắt.
- Được gọi bằng wake word tùy chỉnh như “Hey Pew Pew”.
- Hiểu ngữ cảnh của người dùng và thiết bị đang hoạt động.
- Lựa chọn AI local hoặc AI cloud cho từng tác vụ.
- Ghi nhớ sở thích, ngữ cảnh và routine theo quyền của người dùng.
- Điều khiển một tập hành động được cấp phép trên hệ điều hành.
- Tương tác với trình duyệt và giao diện ứng dụng.
- Chạy workflow terminal đã định nghĩa trong môi trường giới hạn.
- Soạn hoặc thực hiện hành động giao tiếp có xác nhận.
- Tích hợp với Home Assistant.
- Đồng bộ một phần dữ liệu và ngữ cảnh giữa nhiều thiết bị.
- Ghi audit log và cho phép dừng khẩn cấp.

Phạm vi phát triển ban đầu không cố gắng xây dựng ngay một sản phẩm tương đương hoàn toàn với Siri hoặc Gemini. Dự án sẽ tạo một **nền tảng lõi an toàn và có thể mở rộng**, sau đó bổ sung dần nền tảng, skill, AI provider và khả năng tự động hóa.

---

## 3. Mục tiêu dự án

### 3.1. Mục tiêu kinh doanh

- Tạo một sản phẩm trợ lý cá nhân độc lập với một hệ sinh thái thiết bị duy nhất.
- Chứng minh khả năng kết hợp AI local và AI cloud trong cùng một luồng sử dụng.
- Tạo nền tảng có thể mở rộng thành sản phẩm cá nhân, mã nguồn mở, SaaS hoặc self-hosted trong tương lai.
- Giảm số thao tác thủ công khi người dùng làm việc trên máy tính và thiết bị thông minh.
- Xây dựng lợi thế cạnh tranh bằng khả năng tự động hóa an toàn và memory có kiểm soát.

### 3.2. Mục tiêu người dùng

Người dùng có thể:

- Gọi cùng một trợ lý trên nhiều thiết bị.
- Yêu cầu trợ lý thực hiện công việc bằng ngôn ngữ tự nhiên.
- Sử dụng các chức năng cơ bản khi mất Internet.
- Sử dụng AI mạnh hơn khi có Internet.
- Xem, sửa và xóa thông tin trợ lý ghi nhớ.
- Cấp hoặc thu hồi quyền theo từng skill.
- Theo dõi mọi hành động quan trọng mà trợ lý đã thực hiện.
- Dừng ngay tác vụ đang chạy.

### 3.3. Mục tiêu kỹ thuật

- Tách biệt AI reasoning khỏi lớp thực thi hành động.
- Không cấp shell toàn quyền cho mô hình AI.
- Xây dựng Action Engine dựa trên command có cấu trúc.
- Áp dụng local-first cho wake word và các lệnh cơ bản.
- Hỗ trợ provider abstraction để thay đổi mô hình AI.
- Hỗ trợ module/skill isolation.
- Duy trì mức sử dụng tài nguyên thấp khi ở trạng thái chờ.
- Có khả năng khôi phục khi worker, plugin hoặc AI provider gặp lỗi.

### 3.4. Mục tiêu bảo mật

- Áp dụng least privilege theo từng skill và thiết bị.
- Chặn hành động rủi ro cao theo mặc định.
- Không lưu bí mật trong prompt, log hoặc source code.
- Bảo vệ trước prompt injection từ website, email và tài liệu.
- Mã hóa memory và thông tin xác thực.
- Có audit trail đủ để điều tra hành động của trợ lý.
- Không cho phép tác vụ từ xa điều khiển thiết bị nếu thiết bị chưa được đăng ký và ủy quyền.

---

## 4. Nguyên tắc xác định phạm vi

### 4.1. Local-first nhưng không local-only

Các tác vụ có thể xử lý an toàn và hiệu quả trên máy phải ưu tiên local. Cloud chỉ được sử dụng khi:

- Tác vụ cần mô hình mạnh hơn.
- Người dùng cho phép gửi dữ liệu.
- Cần đồng bộ hoặc tích hợp Internet.
- Local runtime không đáp ứng được.

### 4.2. API-first, accessibility-second, vision-last

Khi điều khiển ứng dụng, thứ tự ưu tiên là:

1. API chính thức.
2. Integration hoặc plugin chính thức.
3. Accessibility API.
4. Browser DOM.
5. Computer vision.
6. Tọa độ chuột tuyệt đối chỉ là phương án cuối cùng.

### 4.3. Human-in-the-loop cho hành động nhạy cảm

Hành động gửi dữ liệu, thay đổi tệp, chạy terminal, điều khiển khóa cửa hoặc tác động bên ngoài phải được đánh giá rủi ro và yêu cầu xác nhận phù hợp.

### 4.4. Không biến AI thành quản trị viên hệ thống

AI chỉ lập kế hoạch và gọi tool có cấu trúc. Policy Engine quyết định công cụ có được chạy hay không.

### 4.5. Phát triển theo chiều dọc

Mỗi phase phải hoàn thành một luồng end-to-end có thể dùng được, thay vì xây dựng nhiều module rời rạc nhưng chưa tích hợp.

### 4.6. Bảo mật là tiêu chí nghiệm thu

Một chức năng chưa có permission, audit log, error handling hoặc rollback phù hợp được xem là chưa hoàn thành.

---

## 5. Đối tượng sử dụng và bên liên quan

### 5.1. Người dùng chính

Một cá nhân sử dụng trợ lý cho:

- Công việc hằng ngày.
- Học tập.
- Lập trình.
- Điều khiển máy tính.
- Điều khiển trình duyệt.
- Quản lý thông tin.
- Tự động hóa routine.
- Điều khiển nhà thông minh.

### 5.2. Các actor hệ thống

| Actor | Vai trò |
|---|---|
| Primary User | Chủ sở hữu trợ lý, memory và thiết bị |
| Trusted Device | Thiết bị đã đăng ký và được người dùng tin cậy |
| Desktop Agent | Thực thi hành động cục bộ trên máy tính |
| Mobile Companion | Giao tiếp, xem trạng thái, gửi lệnh và xác nhận |
| Web Console | Quản lý tài khoản, thiết bị, quyền, memory và log |
| Browser Extension | Tương tác có kiểm soát với trình duyệt |
| Local AI Runtime | Xử lý AI cục bộ |
| Cloud AI Provider | Xử lý tác vụ AI qua Internet |
| Home Assistant | Nền tảng điều khiển nhà thông minh |
| External Integration | Dịch vụ được kết nối qua API hoặc OAuth |
| Security/Policy Engine | Kiểm tra quyền và rủi ro trước thực thi |

### 5.3. Vai trò trong đội dự án

| Vai trò | Trách nhiệm |
|---|---|
| Product Owner | Chốt mục tiêu, ưu tiên và scope |
| System Analyst | Viết requirement, use case và business rule |
| Solution Architect | Thiết kế kiến trúc tổng thể |
| Security Engineer | Threat model, permission model và hardening |
| Backend Developer | API, identity, sync, memory và integration |
| Client Developer | Desktop, mobile, web và browser extension |
| AI Engineer | Wake word, STT/TTS, model routing và memory retrieval |
| QA Engineer | Functional, security, performance và recovery testing |
| DevOps Engineer | CI/CD, signing, deployment, secrets và observability |

Một người có thể đảm nhiệm nhiều vai trò trong giai đoạn đầu, nhưng trách nhiệm vẫn phải được phân biệt trong tài liệu và pull request.

---

## 6. Phạm vi sản phẩm tổng thể

Phạm vi sản phẩm dài hạn bao gồm các capability sau:

### 6.1. Personal Assistant Core

- Identity của trợ lý.
- Conversation orchestration.
- Context management.
- Intent recognition.
- Tool planning.
- Response generation.
- Error recovery.
- Confirmation workflow.

### 6.2. Multimodal Interaction

- Voice input.
- Text input.
- Push-to-talk.
- Wake word.
- Text-to-speech.
- Screen context theo yêu cầu.
- Notification và quick action.

### 6.3. Multi-device Platform

- Desktop agent.
- Mobile companion.
- Web console.
- Browser extension.
- Device registration.
- Device handoff.
- Cross-device notification.
- Đồng bộ trạng thái được chọn.

### 6.4. Local–Cloud AI

- Local model adapter.
- Cloud provider adapter.
- Routing policy.
- Privacy mode.
- Fallback.
- Cost and usage tracking.
- Context minimization trước khi gửi cloud.

### 6.5. Memory Platform

- Working memory.
- Local personal memory.
- Cloud long-term memory.
- Routine memory.
- Voice profile.
- Retrieval.
- Summarization.
- Retention policy.
- User memory control.

### 6.6. Action and Automation Platform

- Structured tools.
- Skill registry.
- OS automation.
- Browser automation.
- Terminal workflow.
- Messaging workflow.
- Scheduler.
- Routine engine.
- Event trigger.
- Verification and rollback.

### 6.7. Security Platform

- Device trust.
- Authentication.
- Authorization.
- Permission center.
- Secret storage.
- Sandboxing.
- Policy engine.
- Prompt-injection defense.
- Audit log.
- Emergency stop.
- Update signing.

### 6.8. Smart Home Integration

- Home Assistant authentication.
- Entity discovery.
- Service calls.
- Scene activation.
- Automation trigger.
- Confirmation policy cho thiết bị nhạy cảm.

---

## 7. Phạm vi MVP

### 7.1. Mục tiêu MVP

MVP phải chứng minh được một luồng hoàn chỉnh:

> Người dùng gọi trợ lý → hệ thống hiểu yêu cầu → chọn local hoặc cloud → kiểm tra quyền → thực hiện hành động → xác minh kết quả → ghi log → phản hồi người dùng.

### 7.2. Nền tảng ưu tiên trong MVP

MVP tập trung vào:

1. **Windows Desktop Agent** làm thiết bị thực thi chính.
2. **Web Console** để quản lý cấu hình, thiết bị, quyền, memory và log.
3. **Chromium Browser Extension** để tương tác với trình duyệt.
4. **Android Companion bản giới hạn** để gửi lệnh, nhận thông báo và xác nhận từ xa.
5. **Home Assistant Integration cơ bản**.

Các client macOS, Linux và iOS chưa nằm trong MVP.

### 7.3. Capability bắt buộc của MVP

- Một tài khoản cá nhân.
- Đăng nhập và đăng ký thiết bị.
- Wake word local với ít nhất một cụm từ cấu hình được.
- Push-to-talk.
- Giao tiếp bằng văn bản.
- Speech-to-text và text-to-speech cơ bản.
- Local–cloud routing.
- Private Mode.
- Working memory và local personal memory.
- Cloud sync tùy chọn ở mức tối thiểu.
- Permission Center.
- Audit Log.
- Emergency Stop.
- OS actions cơ bản.
- Browser actions cơ bản.
- Terminal workflow được định nghĩa trước.
- Home Assistant service call cơ bản.
- Routine thủ công.
- Xác nhận hành động nhạy cảm.
- Resource monitoring và worker recovery.

### 7.4. Giới hạn MVP

- Chỉ hỗ trợ một primary user trên mỗi account.
- Chỉ một Windows desktop node được thực thi lệnh đầy đủ tại một thời điểm.
- Android Companion không điều khiển toàn bộ giao diện Android.
- Không có plugin marketplace.
- Không cho phép arbitrary shell.
- Không tự động gửi tin nhắn nếu chưa xác nhận.
- Computer vision chỉ dùng khi DOM hoặc accessibility không đáp ứng và phải được người dùng cho phép.
- Cloud memory chỉ đồng bộ dữ liệu đã được lựa chọn.
- Routine học tự động chỉ dừng ở mức đề xuất, không tự kích hoạt.

---

## 8. Phạm vi chức năng chi tiết

### 8.1. Account và Identity

**Trong phạm vi:**

- Tạo tài khoản.
- Đăng nhập và đăng xuất.
- Quên hoặc đổi mật khẩu.
- Quản lý phiên đăng nhập.
- Đăng ký thiết bị bằng mã ghép nối.
- Đổi tên thiết bị.
- Thu hồi thiết bị.
- Xem lần hoạt động gần nhất.
- Thiết lập tên trợ lý.
- Thiết lập ngôn ngữ và giọng phản hồi.

**Ngoài MVP:**

- Family account.
- Multi-user shared assistant.
- Enterprise SSO.
- Delegated administration.
- Guest profile.

### 8.2. Voice Activation

**Trong phạm vi:**

- Wake word mặc định.
- Một wake word tùy chỉnh ở mức cấu hình.
- Thu một số mẫu giọng để cải thiện nhận diện.
- Điều chỉnh độ nhạy.
- Push-to-talk dự phòng.
- Âm báo hoặc UI indicator khi bắt đầu và kết thúc nghe.
- Xử lý wake word trên thiết bị.
- Tắt microphone listening nhanh.
- Xóa voice profile.

**Không thuộc MVP:**

- Nhận dạng người nói đạt chuẩn xác thực sinh trắc học.
- Luôn lưu bản ghi âm thô.
- Nhận diện nhiều người trong gia đình.
- Huấn luyện mô hình wake word lớn trên cloud.

### 8.3. Conversation và Context

**Trong phạm vi:**

- Conversation session.
- Multi-turn context.
- Tham chiếu ứng dụng, tab hoặc tác vụ vừa được nhắc tới.
- Chuyển đổi giữa voice và text trong cùng phiên.
- Hiển thị kế hoạch hành động trước khi thực hiện khi cần.
- Cho phép hủy, sửa hoặc xác nhận.
- Lưu tóm tắt phiên thay vì luôn lưu toàn bộ nội dung.
- Phản hồi lỗi rõ ràng.

### 8.4. Model Router

**Trong phạm vi:**

- Đăng ký ít nhất một local AI adapter.
- Đăng ký ít nhất một cloud AI provider.
- Chọn local, cloud hoặc auto.
- Quy tắc dựa trên privacy, capability, connectivity và resource.
- Fallback khi provider lỗi.
- Timeout và retry có giới hạn.
- Hiển thị nơi tác vụ được xử lý.
- Không gửi dữ liệu cloud trong Private Mode.
- Tối thiểu hóa context trước khi gọi API.

### 8.5. Operating System Actions

**MVP hỗ trợ tối thiểu:**

- Mở ứng dụng từ allowlist.
- Đóng ứng dụng được phép.
- Phát, tạm dừng, chuyển media.
- Điều chỉnh âm lượng.
- Mở thư mục hoặc tệp trong phạm vi cho phép.
- Tìm tệp theo tên trong thư mục đã cấu hình.
- Chuyển focus hoặc sắp xếp cửa sổ cơ bản.
- Chụp màn hình theo yêu cầu.
- Đọc trạng thái CPU, RAM và tiến trình ở mức cơ bản.
- Bật hoặc tắt chế độ tập trung nếu hệ điều hành hỗ trợ.
- Hiển thị notification.

**Hành động bị chặn mặc định:**

- Xóa hàng loạt.
- Chỉnh registry.
- Thay đổi firewall.
- Tạo tài khoản hệ thống.
- Thay đổi permission hệ thống.
- Định dạng ổ đĩa.
- Thao tác bootloader.
- Vô hiệu hóa phần mềm bảo mật.
- Cài driver.
- Chạy mã tải từ nguồn chưa tin cậy.

### 8.6. Browser Actions

**Trong phạm vi:**

- Liệt kê tab đang mở.
- Chuyển tab.
- Mở URL hoặc tìm kiếm.
- Đọc tiêu đề và nội dung chính của tab được cấp quyền.
- Tìm phần tử qua DOM hoặc accessibility.
- Click phần tử được xác định rõ.
- Điều khiển video cơ bản.
- Chọn video theo tiêu đề.
- Bấm nút “Bỏ qua” do nền tảng hiển thị khi nút hợp lệ xuất hiện.
- Điền biểu mẫu đơn giản nhưng không tự submit dữ liệu nhạy cảm.
- Tóm tắt trang theo yêu cầu.
- Hiển thị preview hành động.

**Giới hạn:**

- Không phá quảng cáo hoặc chặn cơ chế kiếm tiền của nền tảng.
- Không vượt CAPTCHA.
- Không vượt DRM.
- Không bypass paywall.
- Không tự đăng nhập bằng cách đọc mật khẩu.
- Không gửi biểu mẫu thanh toán.
- Không tương tác với website không được người dùng cấp quyền.
- Không xem nội dung của tab private/incognito nếu chưa bật quyền riêng biệt.

### 8.7. Messaging và Communication

**Trong MVP:**

- Tìm contact từ nguồn được kết nối.
- Xử lý trường hợp trùng tên.
- Soạn tin nhắn.
- Hiển thị người nhận, ứng dụng và nội dung.
- Yêu cầu xác nhận trước khi gửi.
- Ghi audit log của hành động gửi.
- Cho phép routine đáng tin cậy gửi các mẫu thông báo rủi ro thấp nếu người dùng bật rõ ràng.

**Không thuộc MVP:**

- Gọi điện tự động.
- Gửi tin hàng loạt.
- Tự động trả lời mọi cuộc trò chuyện.
- Mạo danh người dùng.
- Truy cập nội dung mã hóa đầu cuối nếu nền tảng không cung cấp API hợp lệ.

### 8.8. Routine

**Trong phạm vi:**

- Người dùng tạo routine thủ công.
- Routine gồm nhiều step có cấu trúc.
- Step có timeout, retry và điều kiện thất bại.
- Routine có thể yêu cầu xác nhận ở một step.
- Chạy routine bằng câu lệnh, phím tắt hoặc lịch.
- Dừng routine.
- Xem lịch sử chạy.
- Gợi ý routine khi phát hiện mẫu lặp lại.
- Chỉ lưu gợi ý khi người dùng phê duyệt.

**Ngoài MVP:**

- Tự thay đổi routine không cần phê duyệt.
- Agent tự tạo workflow không giới hạn.
- Marketplace routine công khai.
- Workflow doanh nghiệp phức tạp.

### 8.9. Audit và User Control

**Trong phạm vi:**

- Log thời gian.
- Thiết bị.
- Nguồn yêu cầu.
- Model hoặc routing mode.
- Skill được gọi.
- Permission được sử dụng.
- Hành động dự kiến.
- Kết quả.
- Lỗi.
- Trạng thái xác nhận.
- Dữ liệu có được gửi cloud hay không.
- Cho phép lọc và xóa log theo retention policy.
- Emergency Stop trên desktop và mobile.

---

## 9. Phạm vi theo nền tảng

### 9.1. Windows Desktop Agent

**MVP đầy đủ nhất:**

- Background service hoặc tray application.
- Wake word.
- Push-to-talk.
- Overlay.
- Text command.
- Local AI adapter.
- Action worker.
- Permission prompt.
- Audit.
- Emergency Stop.
- Auto-start tùy chọn.
- Secure update.
- Resource monitor.
- Browser extension bridge.
- Home Assistant bridge.

### 9.2. Web Console

**MVP quản trị:**

- Đăng nhập.
- Xem thiết bị.
- Thu hồi thiết bị.
- Cấu hình AI provider.
- Quản lý permission.
- Quản lý memory.
- Quản lý routine.
- Xem audit log.
- Cấu hình privacy mode.
- Xem trạng thái kết nối.

Web Console không được trực tiếp thực thi hành động hệ thống khi desktop node không online hoặc chưa xác nhận.

### 9.3. Android Companion

**MVP giới hạn:**

- Đăng nhập.
- Gửi lệnh text hoặc voice.
- Nhận trạng thái tác vụ.
- Nhận yêu cầu xác nhận.
- Dừng tác vụ.
- Xem notification.
- Xem một phần memory và log.
- Handoff tác vụ đến desktop node.

**Ngoài MVP:**

- Điều khiển toàn bộ UI Android.
- Accessibility automation toàn thiết bị.
- Wake word chạy liên tục trên mọi cấu hình máy.
- Quản lý SMS/call toàn diện.

### 9.4. Browser Extension

**MVP:**

- Chromium-based browser.
- Permission theo domain.
- DOM extraction có giới hạn.
- Tab listing.
- Tab switch.
- Element action.
- YouTube control cơ bản.
- Secure bridge với Desktop Agent.
- Không truyền toàn bộ trang cho cloud theo mặc định.

### 9.5. Home Assistant

**MVP:**

- Kết nối một Home Assistant instance.
- Lưu token an toàn.
- Đồng bộ entity được chọn.
- Gọi service.
- Kích hoạt scene.
- Truy vấn state.
- Xác nhận cho entity nhạy cảm.

### 9.6. Nền tảng sau MVP

- Linux Desktop Agent.
- macOS Desktop Agent.
- iOS Companion.
- Native smart speaker.
- Wearable.
- In-car integration.
- Multi-node execution nâng cao.

---

## 10. Phạm vi dữ liệu và memory

### 10.1. Loại dữ liệu được quản lý

- Account profile.
- Device metadata.
- Assistant settings.
- Conversation summaries.
- Working context.
- User preferences.
- Trusted contacts.
- Routine definitions.
- Skill permissions.
- Audit events.
- AI provider configuration.
- Home Assistant entity mapping.
- Voice profile features.
- Optional cloud sync records.

### 10.2. Working Memory

- Chỉ phục vụ phiên hiện tại hoặc tác vụ đang chạy.
- Có TTL.
- Không mặc định đồng bộ cloud.
- Tự giải phóng khi phiên kết thúc.
- Không dùng làm nguồn quyền hạn.

### 10.3. Local Personal Memory

- Lưu thông tin có ích ở dạng tóm tắt hoặc record có cấu trúc.
- Mã hóa at rest.
- Có quota.
- Có retention policy.
- Truy xuất theo nhu cầu, không tải toàn bộ vào RAM.
- Người dùng có thể xem, sửa, xóa hoặc pin.
- Mỗi memory record phải có nguồn và thời điểm tạo.

### 10.4. Cloud Long-term Memory

- Opt-in.
- Chỉ đồng bộ category người dùng cho phép.
- Không tự đồng bộ secret.
- Hỗ trợ xóa từ mọi thiết bị.
- Có version hoặc conflict handling cơ bản.
- Có trạng thái pending sync khi offline.
- Mã hóa truyền tải.
- Thiết kế cho phép bổ sung client-side encryption ở phase sau.

### 10.5. Routine Memory

- Lưu command có cấu trúc.
- Không lưu shell string tùy ý làm routine mặc định.
- Có version.
- Có owner.
- Có permission snapshot.
- Khi quyền thay đổi, routine phải được kiểm tra lại.
- Có lịch sử lần chạy gần nhất.

### 10.6. Voice Profile

- Ưu tiên embedding hoặc feature cần thiết thay vì bản ghi dài.
- Không dùng làm xác thực duy nhất.
- Có thể xóa.
- Không chia sẻ giữa người dùng nếu chưa đồng ý.
- Không đưa vào cloud trong Private Mode.

### 10.7. Dữ liệu không được lưu trong memory thông thường

- Password.
- API key dạng rõ.
- Refresh token dạng rõ.
- Private key.
- Số thẻ thanh toán.
- Mã OTP.
- Secret từ clipboard.
- Nội dung nhạy cảm bị đánh dấu không lưu.
- Toàn bộ màn hình hoặc microphone stream kéo dài.

---

## 11. Phạm vi AI Local–Cloud Hybrid

### 11.1. Local Processing

Local mode xử lý tối thiểu:

- Wake word.
- Basic voice activity detection.
- Lệnh hệ thống đơn giản.
- Intent routing cho tool rõ ràng.
- Retrieval từ local memory.
- Một số STT/TTS tùy năng lực thiết bị.
- Routine execution.
- Home Assistant trong LAN.
- Privacy-sensitive requests khi model local đáp ứng được.

### 11.2. Cloud Processing

Cloud mode xử lý:

- Suy luận phức tạp.
- Lập kế hoạch nhiều bước.
- Tóm tắt tài liệu lớn.
- Tìm kiếm hoặc tích hợp Internet.
- Đồng bộ liên thiết bị.
- Mô hình không có sẵn trên local.
- Các chức năng người dùng đã cấp quyền.

### 11.3. Auto Routing

Router phải xem xét:

- Trạng thái mạng.
- Private Mode.
- Data sensitivity.
- Model capability.
- Device resource.
- Latency target.
- Cost policy.
- User preference.
- Provider availability.

### 11.4. Fallback

- Cloud lỗi có thể chuyển local nếu tác vụ phù hợp.
- Local model lỗi có thể chuyển cloud nếu người dùng cho phép.
- Hành động không được tự thực hiện chỉ vì fallback thay đổi model.
- Hệ thống phải thông báo khi khả năng bị giảm.
- Không retry vô hạn.

### 11.5. Provider Abstraction

Provider adapter phải tách khỏi domain logic để:

- Thay đổi nhà cung cấp.
- Dùng self-hosted model.
- Cấu hình endpoint tương thích.
- Áp dụng timeout và rate limit riêng.
- Chuẩn hóa usage metadata.
- Chặn provider khỏi truy cập trực tiếp Action Engine.

---

## 12. Phạm vi tự động hóa và terminal

### 12.1. Nguyên tắc

Terminal là capability rủi ro cao. MVP chỉ cho phép **workflow được định nghĩa trước** hoặc command template có schema.

Ví dụ hợp lệ:

```json
{
  "tool": "project.start",
  "arguments": {
    "projectId": "shop-app",
    "services": ["backend", "frontend"]
  }
}
```

Thay vì:

```text
Hãy tự nghĩ lệnh shell rồi chạy toàn quyền.
```

### 12.2. Trong phạm vi MVP

- Đăng ký project root.
- Định nghĩa command template.
- Allowlist executable.
- Giới hạn working directory.
- Giới hạn environment variables.
- Timeout.
- Giới hạn output.
- Stream log.
- Kill process.
- Theo dõi process tree.
- Chạy build, test, start hoặc stop theo workflow đã cấu hình.
- Hiển thị command preview cho hành động nhạy cảm.
- Lưu exit code và kết quả.
- Không cho phép worker kế thừa quyền Administrator mặc định.

### 12.3. Hành động terminal bị cấm hoặc chặn mặc định

- Recursive delete ngoài workspace.
- Disk format.
- Boot configuration.
- Firewall disable.
- Security service disable.
- Credential dumping.
- Persistence không được khai báo.
- Tải và chạy binary chưa xác minh.
- Chạy script từ nội dung website.
- Sửa quyền hệ thống diện rộng.
- Exfiltration dữ liệu.
- Reverse shell.
- Lệnh làm mất dữ liệu không có backup hoặc xác nhận đặc biệt.

### 12.4. Môi trường thực thi

Mỗi workflow nên chạy trong worker riêng với:

- User privilege thấp.
- Resource limit.
- Network policy.
- Filesystem scope.
- Timeout.
- Process kill.
- Output sanitization.
- Structured audit.

Containerization hoặc OS sandbox có thể được dùng tùy loại tác vụ, nhưng không bắt buộc mọi tác vụ phải chạy trong container ở MVP.

---

## 13. Phạm vi quan sát và điều khiển giao diện

### 13.1. Nguồn context

- Active window metadata.
- Accessibility tree.
- Browser DOM.
- Selected text.
- User-triggered screenshot.
- Application integration.
- Clipboard theo yêu cầu.
- UI element bounds.

### 13.2. Privacy boundary

- Không ghi màn hình liên tục.
- Không chụp screenshot nếu skill không cần.
- Không lưu screenshot mặc định.
- Không gửi screenshot lên cloud nếu chưa được phép.
- Cho phép che vùng nhạy cảm.
- Incognito/private window có permission riêng.
- Password field không được đọc.
- Nội dung từ màn hình được xem là untrusted input.

### 13.3. UI Action Verification

Sau khi click hoặc nhập dữ liệu, hệ thống phải kiểm tra ít nhất một tín hiệu:

- URL thay đổi.
- Element state thay đổi.
- Notification xuất hiện.
- Accessibility state thay đổi.
- API trả kết quả.
- Người dùng xác nhận.

### 13.4. Computer Vision

Trong MVP, vision:

- Chỉ là fallback.
- Chỉ chạy theo yêu cầu hoặc automation đã cấp quyền.
- Không dùng để vượt cơ chế bảo vệ.
- Không tự click khi độ tin cậy thấp.
- Có thể yêu cầu người dùng chọn mục tiêu trên overlay.
- Không lưu dataset màn hình để huấn luyện nếu chưa có opt-in riêng.

---

## 14. Phạm vi Home Assistant

### 14.1. Trong MVP

- Kết nối bằng endpoint và token.
- Kiểm tra kết nối.
- Liệt kê entity được chọn.
- Đọc state.
- Gọi service.
- Bật/tắt đèn và ổ cắm.
- Kích hoạt scene.
- Điều chỉnh một số thuộc tính đơn giản.
- Chạy routine có Home Assistant step.
- Ghi audit.
- Yêu cầu xác nhận theo entity classification.

### 14.2. Phân loại entity

| Mức | Ví dụ | Chính sách |
|---|---|---|
| Thấp | Đèn, quạt, media player | Có thể tự chạy khi đã cấp quyền |
| Trung bình | Điều hòa, ổ cắm công suất lớn | Xác nhận theo cấu hình |
| Cao | Khóa cửa, báo động, camera privacy mode | Xác nhận mạnh, bị giới hạn trong MVP |
| Không hỗ trợ | Hệ thống an toàn sinh mạng | Ngoài phạm vi |

### 14.3. Ngoài MVP

- Tự tạo YAML automation phức tạp không kiểm duyệt.
- Toàn quyền camera.
- Mở khóa cửa từ xa chỉ bằng giọng nói.
- Điều khiển thiết bị y tế.
- Bỏ qua chính sách bảo mật của Home Assistant.
- Public exposure tự động của Home Assistant instance.

---

## 15. Phạm vi bảo mật và quyền riêng tư

### 15.1. Authentication

- Password hoặc external identity provider được hỗ trợ.
- Session có thời hạn.
- Device pairing.
- Device revocation.
- Step-up authentication cho hành động rủi ro cao.
- Không coi voice match là xác thực duy nhất.

### 15.2. Authorization

Permission được phân theo:

- User.
- Device.
- Skill.
- Resource.
- Domain.
- Directory.
- Home Assistant entity.
- Action risk level.
- Thời gian.
- Local hoặc remote execution.

### 15.3. Action Risk Levels

| Level | Mô tả | Ví dụ | Chính sách |
|---|---|---|---|
| 0 | Read-only | Đọc trạng thái, tóm tắt | Có thể chạy khi đã cấp quyền |
| 1 | Low risk, reversible | Mở app, đổi tab, bật đèn | Có thể auto-run |
| 2 | Sensitive | Gửi tin, sửa tệp, chạy workflow | Xác nhận hoặc trusted routine |
| 3 | High risk | Xóa dữ liệu, thay đổi bảo mật | Chặn mặc định hoặc step-up đặc biệt |

### 15.4. Secret Management

- Dùng OS secret store hoặc vault.
- Không commit secret.
- Không log secret.
- Không đưa secret vào model context.
- Redact header và token.
- Rotate hoặc revoke credential.
- Tách secret theo integration.

### 15.5. Prompt Injection Defense

- Phân tách user instruction và external content.
- External content không được thay đổi policy.
- Tool call phải dựa trên user intent đã xác minh.
- Không thực thi lệnh lấy từ website.
- Không gửi dữ liệu sang domain khác chỉ vì nội dung trang yêu cầu.
- Gắn source và trust level cho context.
- Cảnh báo khi trang cố hướng dẫn trợ lý thực hiện hành động ngoài yêu cầu.

### 15.6. Data Protection

- Encryption in transit.
- Encryption at rest cho memory nhạy cảm.
- Data minimization.
- Retention control.
- Export và delete.
- Private Mode.
- Cloud sync opt-in.
- Screenshot và audio temporary storage policy.
- Sensitive field redaction.

### 15.7. Software Supply Chain

- Dependency scanning.
- Signed build hoặc signed release.
- Hash verification cho update.
- Restricted plugin loading.
- Không tải code động từ nguồn không tin cậy.
- SBOM hoặc dependency inventory ở giai đoạn hardening.
- CI security checks.

### 15.8. Audit và Incident Response

- Log hành động quan trọng.
- Không log secret.
- Tamper-evident strategy ở phase hardening.
- Remote device revoke.
- Kill switch.
- Safe mode.
- Disable integration.
- Export diagnostic bundle có redaction.

---

## 16. Yêu cầu phi chức năng

Các giá trị dưới đây là **mục tiêu kỹ thuật ban đầu**, cần được đo trên cấu hình máy tham chiếu và điều chỉnh sau benchmark.

### 16.1. Hiệu năng

- Desktop Agent ở trạng thái chờ không được tải full local LLM vào RAM.
- Mục tiêu RAM idle của core agent: không quá khoảng 250–300 MB, không tính model runtime bên ngoài.
- Mục tiêu CPU wake-word listener trung bình khi idle: dưới khoảng 5% trên máy tham chiếu.
- Wake word phải phản hồi UI hoặc âm báo gần như tức thời sau khi phát hiện.
- Lệnh local đơn giản nên bắt đầu thực thi trong khoảng 2 giây sau khi nhận transcript hoàn chỉnh.
- Cloud request phải có acknowledgement sớm và timeout rõ ràng.
- Memory retrieval không tải toàn bộ database vào RAM.
- Browser extension không được làm chậm đáng kể trang khi không hoạt động.

### 16.2. Độ tin cậy

- Worker crash không làm sập toàn bộ agent.
- Tác vụ có trạng thái `queued`, `running`, `waiting_confirmation`, `completed`, `failed`, `cancelled`.
- Retry có giới hạn.
- Action phải idempotent khi có thể.
- Routine có step-level error handling.
- Agent tự khởi động lại worker bị lỗi trong giới hạn an toàn.
- Không tự chạy lại hành động gửi hoặc xóa nếu trạng thái kết quả không rõ.

### 16.3. Khả năng sử dụng

- Trạng thái nghe, suy luận, chờ xác nhận và thực thi phải hiển thị rõ.
- Người dùng có thể dừng tác vụ bằng UI và phím tắt.
- Permission prompt phải mô tả hành động, dữ liệu và phạm vi.
- Không sử dụng thuật ngữ kỹ thuật bắt buộc với người dùng phổ thông.
- Có lịch sử để giải thích “trợ lý vừa làm gì”.
- Có onboarding cho wake word, permission và Private Mode.

### 16.4. Khả năng mở rộng

- Skill interface có version.
- Provider adapter tách biệt.
- Platform-specific code không xâm nhập domain core.
- Event và command có contract.
- Có thể bổ sung client mới mà không thay đổi toàn bộ backend.
- Có migration cho memory schema và routine schema.

### 16.5. Khả năng bảo trì

- Module boundary rõ.
- Logging có cấu trúc.
- Configuration validation.
- Error code chuẩn hóa.
- Unit test cho policy và routing.
- Integration test cho tool.
- Architecture Decision Record cho quyết định lớn.
- Không để business rule quan trọng chỉ tồn tại trong prompt.

### 16.6. Khả năng tương thích

MVP ưu tiên:

- Windows phiên bản còn được nhà phát triển mục tiêu hỗ trợ.
- Trình duyệt dựa trên Chromium.
- Android đủ khả năng chạy companion app.
- Home Assistant instance có API hợp lệ.

Không cam kết mọi phiên bản hệ điều hành hoặc mọi ứng dụng đều hoạt động trong MVP.

### 16.7. Accessibility

- Keyboard navigation cho Web Console.
- Text alternative cho trạng thái voice.
- Không bắt buộc voice để sử dụng sản phẩm.
- Push-to-talk và text luôn là fallback.
- Font và contrast có thể đọc được.

### 16.8. Observability

- Local diagnostic log.
- Health status.
- Provider latency.
- Tool execution duration.
- Error rate.
- Resource usage.
- Crash report opt-in.
- Không gửi telemetry chứa nội dung người dùng theo mặc định.

---

## 17. Ngoài phạm vi

Các nội dung sau không thuộc MVP hoặc bị loại khỏi phạm vi dự án hiện tại.

### 17.1. AI tự trị toàn quyền

- Agent tự quyết định mục tiêu dài hạn.
- Agent tự mở rộng quyền.
- Agent tự cài plugin.
- Agent tự thay đổi security policy.
- Agent tự chạy shell không giới hạn.

### 17.2. Hành động tài chính và pháp lý

- Chuyển tiền.
- Mua hàng tự động.
- Ký hợp đồng.
- Gửi hồ sơ pháp lý.
- Quyết định đầu tư.
- Thực hiện giao dịch không thể hoàn tác.

### 17.3. Surveillance

- Ghi âm liên tục.
- Ghi hình liên tục.
- Theo dõi người khác.
- Theo dõi camera bí mật.
- Thu thập dữ liệu giọng nói không có consent.
- Keylogging.

### 17.4. Vượt cơ chế nền tảng

- Bypass CAPTCHA.
- Bypass DRM.
- Bypass paywall.
- Tự động vượt quảng cáo bằng thủ thuật.
- Chiếm quyền phiên.
- Dùng credential không được phép.
- Tự động spam hoặc thao túng nền tảng.

### 17.5. Đa người dùng nâng cao

- Family shared memory.
- Workspace doanh nghiệp.
- Organization RBAC.
- Team workflow.
- Multi-tenant enterprise administration.

### 17.6. Nền tảng chưa ưu tiên

- Full macOS agent.
- Full Linux agent.
- Full iOS agent.
- Full Android UI automation.
- Smart speaker hardware riêng.
- Automotive integration.

### 17.7. AI model development

- Huấn luyện LLM nền tảng từ đầu.
- Huấn luyện STT/TTS quy mô lớn.
- Xây dựng data center AI.
- Cam kết chạy model lớn trên mọi máy cấu hình thấp.

### 17.8. Chức năng có rủi ro cao khác

- Tắt antivirus.
- Tắt firewall.
- Điều khiển thiết bị y tế.
- Điều khiển hệ thống an toàn sinh mạng.
- Tự mở khóa cửa từ xa chỉ bằng wake word.
- Truy cập camera hoặc micro của thiết bị khác không có chỉ báo.

---

## 18. Deliverables

### 18.1. Deliverables sản phẩm MVP

1. Windows Desktop Agent.
2. Web Console.
3. Chromium Browser Extension.
4. Android Companion bản giới hạn.
5. Backend/API.
6. Identity và Device Registry.
7. Local–Cloud Model Router.
8. Memory Service.
9. Action Engine.
10. Skill Registry.
11. Permission and Policy Engine.
12. Routine Engine.
13. Audit Service.
14. Home Assistant Integration.
15. Secure Configuration and Secret Storage.
16. Installer hoặc package thử nghiệm.
17. CI/CD pipeline.
18. Test suite cốt lõi.

### 18.2. Deliverables tài liệu

- `PROJECT_VISION.md`
- `PROJECT_SCOPE.md`
- `PRODUCT_REQUIREMENTS.md`
- `SYSTEM_REQUIREMENTS.md`
- `USE_CASES.md`
- `SYSTEM_ARCHITECTURE.md`
- `SECURITY_ARCHITECTURE.md`
- `THREAT_MODEL.md`
- `DATA_AND_MEMORY_DESIGN.md`
- `PERMISSION_MODEL.md`
- `API_SPECIFICATION.md`
- `SKILL_CONTRACT.md`
- `ROUTINE_SCHEMA.md`
- `TEST_STRATEGY.md`
- `RELEASE_PLAN.md`
- `DEPLOYMENT_GUIDE.md`
- `USER_GUIDE.md`
- `PRIVACY_POLICY_DRAFT.md`
- `AGENTS.md`
- `RULES.md`
- `GIT_FLOW.md`

### 18.3. Deliverables kiểm thử

- Unit tests.
- Integration tests.
- End-to-end tests cho luồng chính.
- Permission tests.
- Prompt-injection tests.
- Terminal sandbox tests.
- Resource benchmark.
- Crash recovery tests.
- Offline tests.
- Network interruption tests.
- Device revocation tests.
- Security checklist.
- MVP acceptance report.

---

## 19. Lộ trình triển khai theo phase

### Phase 0 — Discovery và Security Baseline

**Mục tiêu:** Chốt requirement, threat model và kiến trúc lõi trước khi cấp quyền điều khiển thiết bị.

**Deliverables:**

- Scope baseline.
- User journey.
- Use case.
- System context.
- Threat model.
- Action risk matrix.
- Permission model.
- Technology evaluation.
- Prototype wake word.
- Prototype local tool call.

**Không chuyển phase nếu:**

- Chưa có policy chặn arbitrary shell.
- Chưa phân biệt AI plan và tool execution.
- Chưa xác định cách lưu secret.

### Phase 1 — Local Desktop Core

**Mục tiêu:** Trợ lý chạy local trên Windows và thực hiện lệnh rủi ro thấp.

**Phạm vi:**

- Desktop Agent.
- Text command.
- Push-to-talk.
- Wake word.
- Local intent routing.
- OS skill cơ bản.
- Working memory.
- Local personal memory.
- Permission Center cục bộ.
- Audit cục bộ.
- Emergency Stop.
- Resource monitoring.

**Demo cuối phase:**

> “Hey Pew Pew, mở VS Code, mở thư mục dự án và bật nhạc.”

### Phase 2 — Browser và Action Engine

**Mục tiêu:** Tương tác an toàn với trình duyệt và xây dựng tool execution chuẩn hóa.

**Phạm vi:**

- Browser extension.
- Secure bridge.
- Domain permission.
- Tab context.
- YouTube control.
- Skip button interaction hợp lệ.
- Action verification.
- Routine thủ công.
- Predefined terminal workflow.
- Worker isolation.

**Demo cuối phase:**

> “Mở tab YouTube, chọn video ASP.NET Core và phát từ đoạn đang xem.”

### Phase 3 — Cloud, Web và Multi-device

**Mục tiêu:** Bổ sung cloud AI, quản lý trên web và thiết bị companion.

**Phạm vi:**

- Backend/API.
- Account.
- Device pairing.
- Cloud provider.
- Auto routing.
- Private Mode.
- Web Console.
- Android Companion.
- Remote confirmation.
- Cloud sync tùy chọn.
- Device revocation.

**Demo cuối phase:**

> Gửi lệnh từ Android, desktop yêu cầu xác nhận, thực thi và trả trạng thái về điện thoại.

### Phase 4 — Memory và Routine Intelligence

**Mục tiêu:** Cá nhân hóa có kiểm soát.

**Phạm vi:**

- Memory categorization.
- Memory review UI.
- Retention.
- Cross-device memory.
- Routine suggestion.
- Routine versioning.
- Context handoff.
- Memory conflict handling.

**Demo cuối phase:**

> Trợ lý đề xuất routine “Bắt đầu làm việc” sau khi phát hiện chuỗi thao tác lặp lại, nhưng chỉ lưu khi người dùng phê duyệt.

### Phase 5 — Home Assistant và Integration

**Mục tiêu:** Kết nối thiết bị nhà thông minh.

**Phạm vi:**

- Home Assistant connection.
- Entity permission.
- Read state.
- Service call.
- Scene.
- Routine integration.
- Sensitive entity confirmation.

**Demo cuối phase:**

> “Pew Pew, bật đèn bàn và kích hoạt chế độ làm việc.”

### Phase 6 — Hardening và Private Beta

**Mục tiêu:** Chuẩn bị sử dụng thực tế.

**Phạm vi:**

- Security testing.
- Prompt-injection testing.
- Performance benchmark.
- Signed update.
- Recovery.
- Logging redaction.
- Installer.
- User onboarding.
- Privacy controls.
- Beta acceptance.

---

## 20. Ưu tiên phạm vi theo MoSCoW

### 20.1. Must Have

- Windows Desktop Agent.
- Wake word hoặc push-to-talk.
- Text command.
- Local–cloud routing.
- Private Mode.
- Permission Engine.
- Structured Action Engine.
- OS actions cơ bản.
- Browser Extension.
- Predefined terminal workflow.
- Local memory.
- Audit log.
- Emergency Stop.
- Worker isolation.
- Secret storage.
- Device registration.
- Error recovery.

### 20.2. Should Have

- Android Companion.
- Web Console.
- Cloud sync tùy chọn.
- Home Assistant.
- Routine editor.
- Routine suggestion.
- Memory review UI.
- Provider fallback.
- Cost/usage display.
- Voice response.
- Cross-device confirmation.

### 20.3. Could Have

- Computer vision fallback.
- Multiple cloud providers.
- Multiple local runtimes.
- Scheduled routine nâng cao.
- Contact integration mở rộng.
- Plugin SDK nội bộ.
- Offline Home Assistant discovery.
- Desktop overlay nâng cao.
- Natural-language routine builder.

### 20.4. Won’t Have trong MVP

- Arbitrary shell.
- Financial transaction.
- Full autonomous agent.
- iOS full client.
- macOS/Linux full agent.
- Plugin marketplace.
- Shared family memory.
- Enterprise workspace.
- Continuous screen/audio recording.
- Bypass CAPTCHA, DRM hoặc paywall.
- Voice-only high-risk authorization.

---

## 21. Giả định

- Người dùng chính sở hữu và kiểm soát thiết bị đã đăng ký.
- Máy tính Windows có microphone cho chức năng voice.
- Người dùng có thể dùng text hoặc push-to-talk khi wake word không phù hợp.
- Một số AI cloud yêu cầu API key hoặc tài khoản riêng.
- Local AI capability phụ thuộc cấu hình phần cứng.
- Người dùng chấp nhận rằng không phải mọi ứng dụng đều có API hoặc accessibility tốt.
- Home Assistant đã được người dùng cài đặt và cấu hình trước.
- Internet không phải lúc nào cũng có sẵn.
- Người dùng hiểu và xác nhận các hành động nhạy cảm.
- MVP được phát triển ưu tiên cho một primary user.
- Các ứng dụng bên thứ ba có thể thay đổi giao diện hoặc chính sách.
- Dữ liệu memory có thể được giới hạn hoặc tóm tắt để phù hợp tài nguyên.

---

## 22. Ràng buộc

### 22.1. Ràng buộc kỹ thuật

- Tài nguyên local có giới hạn.
- Wake word phải chạy nhẹ.
- Không thể đảm bảo cùng một local model chạy tốt trên mọi máy.
- Trình duyệt và hệ điều hành hạn chế quyền extension hoặc background service.
- UI automation có thể không ổn định khi giao diện thay đổi.
- Một số ứng dụng nhắn tin không cung cấp API phù hợp.
- Mobile OS hạn chế background listening và automation.

### 22.2. Ràng buộc bảo mật

- Không chạy quyền Administrator mặc định.
- Không cấp toàn bộ filesystem.
- Không cấp network unrestricted cho mọi worker.
- Không lưu secret trong memory.
- Không thực hiện Level 3 action bằng routine thông thường.
- Remote action phải qua device trust và authorization.

### 22.3. Ràng buộc phạm vi

- MVP ưu tiên chất lượng và an toàn hơn số lượng skill.
- Mỗi phase chỉ thêm integration khi core platform ổn định.
- Không triển khai đồng thời toàn bộ desktop OS.
- Mọi chức năng mới phải có owner, permission và audit strategy.

### 22.4. Ràng buộc pháp lý và nền tảng

- Tích hợp phải tuân thủ API, điều khoản dịch vụ và quyền của hệ điều hành.
- Không thiết kế chức năng để vượt quảng cáo, DRM, CAPTCHA hoặc paywall.
- Việc xử lý voice, contact và cloud memory phải có consent rõ ràng.
- Quy định bảo vệ dữ liệu có thể khác nhau theo khu vực triển khai.

---

## 23. Phụ thuộc

### 23.1. Phụ thuộc nền tảng

- Windows APIs.
- Browser extension APIs.
- Android application APIs.
- Home Assistant APIs.
- OS secret storage.
- Accessibility APIs.
- Audio input/output APIs.

### 23.2. Phụ thuộc AI

- Local inference runtime.
- Wake word engine.
- STT engine.
- TTS engine.
- Cloud AI provider.
- Embedding hoặc retrieval component.
- Model licensing.

### 23.3. Phụ thuộc vận hành

- Backend hosting nếu bật cloud.
- Database.
- Object storage nếu cần.
- Identity provider hoặc authentication service.
- CI/CD.
- Code signing certificate ở giai đoạn phát hành.
- Monitoring và crash reporting opt-in.

### 23.4. Phụ thuộc người dùng

- Cấp permission.
- Ghép nối thiết bị.
- Cung cấp API key nếu BYOK.
- Xác định thư mục được phép.
- Xác định contact hoặc Home Assistant entity đáng tin cậy.
- Phê duyệt routine.

---

## 24. Rủi ro phạm vi

| Rủi ro | Ảnh hưởng | Biện pháp |
|---|---|---|
| Scope quá rộng | Không hoàn thành MVP | Chia phase và khóa Must Have |
| Hỗ trợ quá nhiều OS | Tăng chi phí bảo trì | Windows-first |
| UI bên thứ ba thay đổi | Automation hỏng | API/DOM/accessibility-first |
| Local AI dùng nhiều RAM | Trải nghiệm kém | Load on demand, model budget |
| Wake word kích hoạt nhầm | Hành động ngoài ý muốn | Confirmation, sensitivity, push-to-talk |
| Prompt injection | Rò rỉ dữ liệu hoặc tool misuse | Trust boundary và policy engine |
| Terminal quá mạnh | Mất dữ liệu hoặc chiếm quyền | Structured workflow và sandbox |
| Cloud provider lỗi | Tác vụ gián đoạn | Fallback và timeout |
| Memory tích lũy quá lớn | Tốn disk/RAM và giảm chất lượng | Quota, summarization, TTL |
| Đồng bộ sai | Mất hoặc xung đột dữ liệu | Versioning và conflict handling |
| Remote control bị lạm dụng | Rủi ro nghiêm trọng | Device trust, revoke, step-up auth |
| Người dùng không hiểu permission | Cấp quyền quá rộng | Permission UX và default deny |
| Home Assistant entity nhạy cảm | Ảnh hưởng an toàn vật lý | Entity classification |
| Extension bị lạm dụng | Đọc dữ liệu web | Domain permission và redaction |
| AI đưa kế hoạch sai | Thực hiện sai | Verification và human-in-the-loop |

---

## 25. Tiêu chí nghiệm thu MVP

MVP chỉ được nghiệm thu khi đáp ứng đồng thời các tiêu chí sau.

### AC-01 — Kích hoạt và tương tác

- Người dùng có thể kích hoạt bằng push-to-talk.
- Wake word hoạt động trên máy tham chiếu.
- Có chỉ báo rõ khi đang nghe.
- Người dùng có thể hủy trước khi thực thi.

### AC-02 — Lệnh local ngoại tuyến

Khi không có Internet, trợ lý thực hiện được tối thiểu các nhóm lệnh:

- Mở ứng dụng.
- Điều khiển media.
- Điều chỉnh âm lượng.
- Mở thư mục được phép.
- Chạy routine local.
- Đọc trạng thái Home Assistant trong LAN nếu cấu hình cho phép.

### AC-03 — Local–cloud routing

- Người dùng chọn được local, cloud hoặc auto.
- Private Mode chặn cloud request.
- Hệ thống hiển thị tác vụ đã dùng local hay cloud.
- Provider lỗi không làm agent crash.

### AC-04 — Browser automation

- Extension chỉ hoạt động trên domain được cấp quyền.
- Có thể chuyển tab và chọn video theo tiêu đề.
- Có thể bấm nút “Bỏ qua” hợp lệ khi xuất hiện.
- Không đọc password field.
- Hành động được xác minh sau thực thi.

### AC-05 — Terminal workflow

- Có thể chạy ít nhất một workflow build/test/start được định nghĩa trước.
- Workflow bị giới hạn working directory.
- Có timeout.
- Có thể dừng process.
- Command nguy hiểm ngoài policy bị chặn.
- Exit code và log được lưu.

### AC-06 — Memory

- Người dùng xem được memory.
- Có thể sửa và xóa.
- Local memory không được tải toàn bộ vào RAM.
- Private record không tự đồng bộ cloud.
- Memory record có source và timestamp.

### AC-07 — Permission

- Skill không thể truy cập resource ngoài permission.
- Hành động Level 2 yêu cầu xác nhận hoặc trusted routine.
- Hành động Level 3 bị chặn mặc định.
- Permission có thể thu hồi.

### AC-08 — Audit và kiểm soát

- Mỗi action quan trọng có audit record.
- Audit không chứa secret.
- Emergency Stop dừng được task và process con.
- Người dùng xem được lý do task thất bại.

### AC-09 — Multi-device tối thiểu

- Một desktop node được ghép nối với account.
- Web Console xem được trạng thái thiết bị.
- Android Companion gửi được lệnh hoặc xác nhận.
- Thiết bị bị thu hồi không thể gửi lệnh mới.

### AC-10 — Home Assistant

- Kết nối thành công một instance.
- Đọc được state của entity được chọn.
- Gọi được service cho entity rủi ro thấp.
- Entity nhạy cảm yêu cầu xác nhận.

### AC-11 — Tài nguyên

- Agent không tải full local LLM khi idle.
- Resource usage được đo và ghi lại trên máy tham chiếu.
- Worker bị lỗi không làm sập toàn bộ agent.
- Browser extension không chạy xử lý nặng liên tục khi không dùng.

### AC-12 — Security baseline

- Secret được lưu ngoài source code và log.
- Prompt injection test cơ bản không thể tự gọi tool trái permission.
- Không có arbitrary shell.
- Không có remote action từ thiết bị chưa đăng ký.
- Dependency và secret scanning được chạy trong CI.

---

## 26. Definition of Done

Một feature chỉ được xem là hoàn thành khi:

1. Có requirement hoặc use case được chấp thuận.
2. Có acceptance criteria.
3. Có thiết kế permission và risk level.
4. Có implementation.
5. Có validation đầu vào.
6. Có error handling.
7. Có timeout hoặc cancellation nếu thực thi dài.
8. Có audit khi cần.
9. Không log secret.
10. Có unit test cho logic cốt lõi.
11. Có integration test cho tool hoặc API.
12. Có kiểm thử hành vi bị từ chối.
13. Có tài liệu cấu hình.
14. Có migration nếu thay đổi schema.
15. Có rollback hoặc recovery strategy.
16. Đã review bảo mật khi feature có quyền truy cập thiết bị.
17. Đã chạy lint, build và test trong CI.
18. Không phá Private Mode.
19. Không tự mở rộng permission.
20. Đã demo end-to-end trên nền tảng mục tiêu.

---

## 27. Quản lý thay đổi phạm vi

### 27.1. Scope Baseline

Sau khi tài liệu được chốt, mọi feature mới phải được phân loại:

- Trong scope hiện tại.
- Thay thế feature khác.
- Chuyển sang phase sau.
- Change Request.

### 27.2. Nội dung Change Request

Mỗi đề xuất thay đổi phải có:

- Mô tả.
- Lý do.
- Giá trị người dùng.
- Phase đề xuất.
- Tác động kiến trúc.
- Tác động bảo mật.
- Tác động dữ liệu.
- Tác động kiểm thử.
- Tác động timeline và nguồn lực.
- Feature nào bị loại hoặc lùi để giữ phạm vi.

### 27.3. Quy tắc chống scope creep

- Không thêm nền tảng mới trong MVP nếu Windows flow chưa ổn định.
- Không thêm integration mới nếu Permission Engine chưa hỗ trợ.
- Không thêm tool write-action nếu chưa có audit và confirmation.
- Không thêm AI provider nếu provider abstraction chưa hoàn chỉnh.
- Không thêm routine tự động nếu routine thủ công chưa an toàn.
- Không tăng quyền terminal để sửa một workflow thiết kế kém.
- Không xem prototype là production-ready.

### 27.4. Quyền phê duyệt

Product Owner chốt thay đổi sản phẩm. Architect và Security Engineer có quyền từ chối thiết kế tạo rủi ro không thể kiểm soát trong scope hiện tại.

---

## 28. Ma trận truy vết cấp cao

| Mục tiêu | Capability | Deliverable | Tiêu chí |
|---|---|---|---|
| Gọi trợ lý tự nhiên | Wake word, push-to-talk | Desktop Agent | AC-01 |
| Hoạt động offline | Local routing, OS skill | Local AI Adapter | AC-02 |
| Dùng AI mạnh khi online | Cloud provider, router | Backend/Router | AC-03 |
| Điều khiển trình duyệt | Browser skill | Extension | AC-04 |
| Tự động hóa lập trình | Structured terminal workflow | Action Worker | AC-05 |
| Ghi nhớ cá nhân | Memory service | Memory UI/Store | AC-06 |
| An toàn theo quyền | Policy Engine | Permission Center | AC-07 |
| Có thể kiểm tra | Audit và kill switch | Audit Service | AC-08 |
| Đa thiết bị | Pairing và companion | Web/Android | AC-09 |
| Nhà thông minh | Home Assistant integration | HA Adapter | AC-10 |
| Chạy nhẹ | Resource budget | Benchmark Report | AC-11 |
| Chống lạm dụng | Security baseline | Threat Model/Test | AC-12 |

---

## 29. Tuyên bố chốt phạm vi

> **Pew Pew Assistant MVP sẽ xây dựng một trợ lý AI cá nhân 1:1 chạy chính trên Windows, có thể được kích hoạt bằng giọng nói hoặc văn bản, xử lý tác vụ bằng AI local hoặc cloud, ghi nhớ có kiểm soát, thực thi một tập hành động hệ điều hành, trình duyệt, terminal và Home Assistant thông qua Permission Engine, sandbox, confirmation và audit log.**

MVP được xem là thành công khi chứng minh được:

- Luồng lệnh end-to-end.
- Khả năng hoạt động offline cơ bản.
- Khả năng mở rộng cloud.
- Tương tác trình duyệt ổn định trong phạm vi đã chọn.
- Terminal automation không cấp shell toàn quyền.
- Memory có thể quản lý.
- Điều khiển đa thiết bị ở mức tối thiểu.
- Bảo mật và quyền riêng tư là một phần của kiến trúc, không phải bổ sung sau cùng.

---

## 30. Bản rút gọn cho README

```md
## Project Scope

Pew Pew Assistant là một trợ lý AI cá nhân 1:1 hoạt động theo mô hình
Local–Cloud Hybrid. MVP tập trung vào Windows Desktop Agent, Web Console,
Chromium Browser Extension, Android Companion giới hạn và tích hợp Home
Assistant cơ bản.

### In scope

- Wake word, push-to-talk và text command.
- AI local, AI cloud, Auto Mode và Private Mode.
- Working memory, local personal memory và cloud sync tùy chọn.
- Điều khiển hệ điều hành ở mức an toàn.
- Điều khiển tab và phần tử trình duyệt qua extension.
- Chọn video YouTube và bấm nút “Bỏ qua” hợp lệ khi nền tảng hiển thị.
- Chạy terminal workflow được định nghĩa trước trong môi trường giới hạn.
- Routine thủ công và gợi ý routine có phê duyệt.
- Permission Engine, confirmation, audit log và Emergency Stop.
- Đăng ký, ghép nối và thu hồi thiết bị.
- Điều khiển entity Home Assistant đã được cấp quyền.

### Out of scope for MVP

- Shell toàn quyền cho AI.
- AI tự trị và tự mở rộng quyền.
- Chuyển tiền hoặc mua hàng tự động.
- Ghi âm hoặc ghi màn hình liên tục.
- Bypass CAPTCHA, DRM, paywall hoặc cơ chế quảng cáo.
- Full macOS, Linux và iOS agent.
- Plugin marketplace và enterprise multi-user.
- Hành động rủi ro cao không có xác nhận.

**Scope principle:** AI có thể đề xuất và lập kế hoạch, nhưng chỉ Policy
Engine và Action Engine được phép thực thi hành động trong phạm vi người
dùng đã cấp quyền.
```
