# Definition of Done

Một task chỉ được coi là hoàn thành khi đáp ứng các mục áp dụng:

## Requirement

- Có Task ID và acceptance criteria.
- Không vượt scope.
- Business rule được truy vết.

## Implementation

- Code build được.
- Không tạo dependency bị cấm.
- Error handling và cancellation phù hợp.
- Side effect có audit/idempotency khi cần.

## Testing

- Unit tests cho logic mới.
- Integration tests cho boundary quan trọng.
- Architecture tests nếu thay dependency/module.
- Security tests cho permission, input và secret.
- Regression test cho bug fix.

## Quality

- Không có warning nghiêm trọng chưa giải thích.
- Không có secret hoặc dữ liệu cá nhân trong source/log.
- Không có TODO che giấu phần bắt buộc của task.

## Documentation

- Task board cập nhật.
- Session log cập nhật.
- API/domain/decision docs cập nhật nếu bị ảnh hưởng.
- Next action rõ ràng.

## Evidence

- Ghi command và kết quả kiểm tra.
- Với UI/automation, lưu evidence quan sát được.
- Không chỉ dựa vào suy luận của AI.
