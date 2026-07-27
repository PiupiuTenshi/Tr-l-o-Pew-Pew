# System Context

## Actors

- Primary User.
- Trusted Device.
- External AI Provider.
- Local AI Runtime.
- Browser.
- Operating System.
- Messaging Platform.
- Home Assistant.
- Cloud Storage and Sync Services.

## System boundary

Pew Pew Assistant tiếp nhận lệnh bằng voice/text/UI trigger, lập kế hoạch, kiểm tra policy, thực thi skill trên device hoặc external integration, xác minh kết quả và ghi audit.

## Trust boundaries

1. User ↔ Device Agent.
2. Device Agent ↔ Cloud Backend.
3. Orchestrator ↔ External AI Provider.
4. Policy Engine ↔ Action Worker.
5. Browser Extension ↔ Web Content.
6. Home Assistant Integration ↔ Smart Devices.
7. Local Memory ↔ Cloud Memory.

## Rule

Dữ liệu đi qua trust boundary phải được xác thực, giới hạn quyền, mã hóa khi phù hợp và ghi audit cho hành động nhạy cảm.
