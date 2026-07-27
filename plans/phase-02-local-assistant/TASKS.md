# P02 — Small Task Backlog

## Task contract

Mỗi task tạo một outcome local/offline có thể đo hoặc test trong tối đa 2 ngày. Task kế thừa privacy, voice, mode, action và resource rules từ [phase contract](README.md). Text/push-to-talk phải luôn là fallback; wake word không bao giờ là authentication.

| ID | Size | Status | Single outcome | Depends on | Acceptance / evidence |
|---|---:|---|---|---|---|
| [P02-T01](tasks/P02-T01.md) | 1–2d | BACKLOG | Activation/offline/privacy specs được duyệt | P01 | Voice/text/cancel/failure flows có AC |
| [P02-T02](tasks/P02-T02.md) | 1–2d | BACKLOG | Desktop/Tray shell hiển thị state và nhận text | P02-T01 | UI smoke + keyboard/accessibility evidence |
| [P02-T03](tasks/P02-T03.md) | 1–2d | BACKLOG | Push-to-talk/audio session có consent indicator | P02-T02 | Start/stop/cancel/buffer-cleanup tests |
| [P02-T04](tasks/P02-T04.md) | 1–2d | BACKLOG | `VoiceWakeProfile` consent/revoke/delete hoạt động | P02-T03 | Lifecycle + raw-audio TTL tests |
| [P02-T05](tasks/P02-T05.md) | 1–2d | BACKLOG | Local wake-word adapter hoạt động qua interface | P02-T03, P02-T04 | Reference-machine detection/false-trigger report |
| [P02-T06](tasks/P02-T06.md) | 1–2d | BACKLOG | Local STT tạo transcript hoặc clarification | P02-T03 | Deterministic adapter + low-confidence tests |
| [P02-T07](tasks/P02-T07.md) | 1d | BACKLOG | Local TTS có cancel và text fallback | P02-T02 | Interrupt/failure/accessibility tests |
| [P02-T08](tasks/P02-T08.md) | 1–2d | BACKLOG | Local intent router chỉ phát structured proposal | P02-T06, P01 action contracts | Invalid/prompt-injected output bị reject |
| [P02-T09](tasks/P02-T09.md) | 1–2d | BACKLOG | Local model lazy-load/unload theo budget | P02-T08 | Lifecycle/memory-pressure/idle tests |
| [P02-T10](tasks/P02-T10.md) | 1d | BACKLOG | Local/Private mode hiển thị và fail closed | P02-T02, P02-T08 | Network spy chứng minh không cloud call |
| [P02-T11](tasks/P02-T11.md) | 1–2d | BACKLOG | App/media/volume skills chạy trong allowlist | P02-T08, P01 Policy/Action | Permission/verification/cancel tests |
| [P02-T12](tasks/P02-T12.md) | 1–2d | BACKLOG | Allowed-folder find/open không thoát root | P02-T08 | Canonical path/traversal/symlink tests |
| [P02-T13](tasks/P02-T13.md) | 1d | BACKLOG | Emergency Stop dừng local owner task/process | P02-T11, P02-T12 | Stop tree/no-resume/audit tests |
| [P02-T14](tasks/P02-T14.md) | 1d | BACKLOG | Idle/listening/action resource baseline được đo | P02-T05, P02-T09, P02-T11 | RAM/CPU/latency report có máy tham chiếu |
| [P02-T15](tasks/P02-T15.md) | 1–2d | BACKLOG | Offline assistant journey được nghiệm thu | P02-T01..P02-T14 | AC-01/02/11 + denied/cancel evidence |

## Execution waves

1. UX/audio: P02-T01–P02-T04.
2. Voice/model: P02-T05–P02-T10.
3. Local actions/control: P02-T11–P02-T13.
4. Measure/accept: P02-T14, P02-T15.

