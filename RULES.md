# Repository Rules

## R-001 — Source of truth

Tài liệu trong `docs/` là nguồn sự thật về sản phẩm, domain, kiến trúc và quy trình. Code phải phù hợp với tài liệu đã duyệt.

## R-002 — One task, one outcome

Mỗi task phải có một kết quả chính, acceptance criteria và evidence. Task quá lớn phải được chia nhỏ trước khi triển khai.

## R-003 — No silent assumptions

AI không được tự tạo business rule. Mọi giả định có ảnh hưởng đến dữ liệu, bảo mật, quyền hoặc trải nghiệm người dùng phải được ghi vào blocker hoặc decision request.

## R-004 — Architecture protection

Không tạo dependency trái với `DEPENDENCY_RULES.md`. Mọi ngoại lệ cần ADR được duyệt.

## R-005 — Security first

Không lưu secret vào repository. Không chạy quyền cao mặc định. Không cho model thực thi shell trực tiếp. Không gửi dữ liệu local lên cloud khi chưa qua policy.

## R-006 — Verification before completion

Không dùng các cụm từ “đã hoàn thành”, “đã fix” hoặc “đã chạy tốt” nếu chưa có bằng chứng build, test hoặc kiểm tra thực tế.

## R-007 — State continuity

Mỗi phiên phải để lại `NEXT_ACTION.md` đủ rõ để agent khác tiếp tục mà không phải suy đoán.

## R-008 — Controlled scope change

Feature ngoài scope phải đi qua `CHANGE_REQUEST_TEMPLATE.md`. Không chèn feature mới vào task đang chạy.

## R-009 — Small reversible changes

Ưu tiên thay đổi nhỏ, có thể review và rollback. Không trộn feature, refactor và migration lớn trong cùng một commit.

## R-010 — User control

Mọi khả năng automation phải giữ quyền kiểm soát cuối cùng cho người dùng, đặc biệt với gửi tin nhắn, terminal, tệp, thiết bị nhà thông minh và cloud sync.
