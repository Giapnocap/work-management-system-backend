# Hợp đồng lỗi API

Backend trả về một cấu trúc JSON thống nhất cho mọi lỗi.

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "Thông báo lỗi dễ hiểu",
  "status": 400,
  "detail": "",
  "instance": "/api/resource",
  "code": "business_error",
  "message": "Thông báo lỗi dễ hiểu",
  "traceId": "0HN...",
  "errors": {}
}
```

Kiểu nội dung của response là `application/problem+json`. Lỗi validation dùng cùng cấu trúc và điền `errors` bằng tên field cùng mảng thông báo tương ứng.

## Mã lỗi

- `validation_error`: dữ liệu request không hợp lệ.
- `business_error`: request đúng cú pháp nhưng vi phạm quy tắc nghiệp vụ.
- `not_found`: tài nguyên được yêu cầu không tồn tại hoặc không còn quyền truy cập.
- `forbidden`: người dùng đã xác thực không có quyền thực hiện thao tác.
- `unauthorized`: request thiếu hoặc chứa thông tin định danh không hợp lệ.
- `request_too_large`: file tải lên hoặc request body vượt quá giới hạn của server.
- `bad_request`: dữ liệu request sai định dạng.
- `concurrency_conflict`: row version gửi lên đã cũ.
- `duplicate_data`: vi phạm unique constraint trong database.
- `rate_limit_exceeded`: caller vượt quá rate limit của endpoint.
- `internal_server_error`: lỗi không mong đợi phía server.

## Ánh xạ exception

- `BusinessException` -> HTTP 400.
- `NotFoundException` -> HTTP 404.
- `ForbiddenException` -> HTTP 403.
- `UnauthorizedAccessException` -> HTTP 403 để tương thích với code cũ.
- `BadHttpRequestException` do kích thước request -> HTTP 413.
- Xung đột concurrency của EF Core và vi phạm unique constraint -> HTTP 409.
- Request bị rate limit từ chối -> HTTP 429.
- Exception không mong đợi -> HTTP 500.

## Lưu ý

- Mọi response đều có `X-Correlation-ID`. Giá trị an toàn do client cung cấp sẽ được tái sử dụng; nếu không, server trả về trace identifier.
- `traceId` trong JSON, correlation header của response và structured request log dùng chung một identifier.
- Service nên ném application exception cụ thể thay vì `Exception` chung chung.
- Controller chuyển lỗi ứng dụng cho global exception middleware xử lý.
- Controller yêu cầu xác thực phải lấy current user id từ token claim và trả về `unauthorized` khi id thiếu hoặc không hợp lệ.
- Lỗi validation DTO/model trả về cùng cấu trúc JSON với `code = validation_error` và `errors` theo từng field.
- Chi tiết exception nội bộ chỉ được trả về trong môi trường Development.
- Request bị client ngắt kết nối được xem là cancellation và không bị chuyển thành HTTP 500.
- Swagger mô tả các response phổ biến `400`, `401`, `403`, `404`, `409` và `500` tại endpoint phù hợp, nhưng tài liệu này vẫn là nguồn chuẩn cho cấu trúc response body.

## Mã trạng thái thành công

- Tạo tài nguyên trả về HTTP `201 Created`.
- Xóa, lưu trữ và command không có response body trả về HTTP `204 No Content`.
- Đọc, cập nhật, review và command có dữ liệu trả về dùng HTTP `200 OK`.
