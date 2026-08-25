# Workflow Task và Progress

Tài liệu này là hợp đồng workflow hiện tại của Task và báo cáo tiến độ.

## Quyền sở hữu trạng thái

Trạng thái Task và báo cáo tiến độ là hai khái niệm riêng. Báo cáo bị từ chối không tạo ra trạng thái Task bị từ chối.

| Thành phần | Trạng thái |
| --- | --- |
| Task | `NotStarted`, `InProgress`, `Submitted`, `Approved` |
| Progress | `InProgress`, `Submitted`, `Approved`, `Rejected` |
| Review | Quyết định duyệt/từ chối bất biến, liên kết one-to-one với một báo cáo tiến độ đã gửi |

## Ma trận chuyển trạng thái Task

| Từ | Sang | Tác nhân kích hoạt | Chủ thể thực tế | Điều kiện bắt buộc |
| --- | --- | --- | --- | --- |
| `NotStarted` | `InProgress` | Báo cáo tiến độ một phần | User được giao | Còn quyền truy cập Task; dependency đã hoàn thành |
| `NotStarted` | `Submitted` | Báo cáo 100% cần review | User được giao | Còn quyền truy cập Task; đáp ứng quy tắc evidence; dependency đã hoàn thành |
| `NotStarted` | `Approved` | Báo cáo 100% không cần review | Quy tắc hệ thống do User được giao khởi tạo | Mọi assignee bắt buộc đã hoàn thành; dependency đã hoàn thành |
| `InProgress` | `Submitted` | Báo cáo 100% cần review | User được giao | Còn quyền truy cập Task; đáp ứng quy tắc evidence; dependency đã hoàn thành |
| `InProgress` | `Approved` | Lần hoàn thành bắt buộc cuối cùng | Quy tắc hệ thống | Mọi assignee bắt buộc đã hoàn thành; dependency đã hoàn thành |
| `Submitted` | `Approved` | Manager duyệt báo cáo bắt buộc cuối cùng | Manager của phòng ban hiện tại | Đúng phạm vi quản lý; mọi assignee bắt buộc đã hoàn thành |
| `Submitted` | `InProgress` | Manager từ chối, hoặc lần duyệt chưa đủ để hoàn thành | Manager của phòng ban hiện tại | Đúng phạm vi quản lý; từ chối phải có lý do |

`Submitted -> Submitted` có thể xảy ra dưới dạng no-op khi vẫn còn báo cáo đã gửi khác đang chờ duyệt. Trường hợp này không được lưu thành một lần chuyển trạng thái. `Approved` là trạng thái cuối vì hệ thống hiện chưa có workflow mở lại.

## Ma trận chuyển trạng thái Progress

| Từ | Sang | Tác nhân kích hoạt | Chủ thể | Điều kiện bắt buộc |
| --- | --- | --- | --- | --- |
| Mới | `InProgress` | Báo cáo một phần | User được giao | Còn quyền truy cập Task; dependency đã hoàn thành |
| Mới | `Submitted` | Báo cáo 100% cần review | User được giao | Có evidence khi bắt buộc; dependency đã hoàn thành |
| Mới | `Approved` | Báo cáo 100% không cần review | User được giao + quy tắc hoàn thành của hệ thống | Đạt quy tắc dependency và chống hoàn thành trùng |
| `Submitted` | `Approved` | Review chấp thuận | Manager của phòng ban hiện tại | Báo cáo chưa có review trước đó |
| `Submitted` | `Rejected` | Review từ chối | Manager của phòng ban hiện tại | Lý do không rỗng; báo cáo chưa có review trước đó |

## Quyền sở hữu API

- `POST /api/progress` là command duy nhất tạo báo cáo tiến độ và tự suy ra trạng thái Task ở server.
- `POST /api/review` là command duy nhất để duyệt hoặc từ chối.
- Không có endpoint chung cho phép sửa trực tiếp trạng thái Task.
- Các command Task `/start`, `/submit`, `/approve` hoặc `/reject` cố ý không được thêm vì chúng sẽ bỏ qua quyền sở hữu evidence và review của Progress.

## Quyền sở hữu implementation

- `TaskWorkflowPolicy` chứa ma trận chuyển trạng thái rõ ràng và các yêu cầu theo ngữ cảnh. Đây là policy riêng của domain, không phải workflow engine dùng chung.
- `TaskWorkflowService` là application service duy nhất thay đổi trạng thái hoàn thành Task, trạng thái Progress đã review, `ActualHours` đã duyệt và lịch sử chuyển trạng thái.
- `ProgressService` chịu trách nhiệm về danh tính người báo cáo, phạm vi assignment, chống hoàn thành trùng, evidence và điều kiện dependency trước khi chuyển giao việc đổi trạng thái.
- `ReviewService` kiểm tra role Manager và phạm vi phòng ban hiện tại trước khi chuyển giao quyết định review.
- Từ chối phải có lý do không rỗng. Request không hợp lệ hoặc không đủ quyền thất bại trước khi thay đổi trạng thái đã lưu.
- Mỗi lần chuyển trạng thái Task/Progress được lưu đều ghi `TaskHistory` cùng progress id liên quan và lý do đã giới hạn độ dài.
- `Progress.RowVersion`, `TaskItem.RowVersion`, unique index của review và transaction bao quanh bảo đảm các request review cạnh tranh chỉ lưu một quyết định và một lần cộng approved hours.
