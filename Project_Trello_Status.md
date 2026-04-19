# Project Trello Status - Spa Management System

## 📋 BACKLOG (Hạng mục tương lai)
- [ ] **Thống kê hiệu suất nhân viên (Advanced Staff Metrics)**: Xây dựng Dashboard chuyên sâu về năng suất làm việc, thu nhập và đánh giá của khách hàng cho từng kỹ thuật viên.
- [ ] **Hệ thống Thông báo thời gian thực (Real-time Notifications)**: Sử dụng SignalR để thông báo ngay lập tức cho Lễ tân khi có khách đặt lịch mới và cho Kỹ thuật viên khi được gán lịch.
- [ ] **Nhắc lịch tự động (Auto Reminders)**: Tích hợp dịch vụ SMS/Email để gửi reminder cho khách hàng 2 tiếng trước giờ hẹn.
- [ ] **Phân tích Tài chính chuyên sâu (Financial Analytics)**: Báo cáo biểu đồ doanh thu theo tháng/quý, so sánh hiệu quả giữa các chi nhánh.
- [ ] **Ứng dụng di động cho Kỹ thuật viên (Staff Mobile App)**: Phiên bản rút gọn để nhân viên xem lịch và cập nhật trạng thái dịch vụ nhanh hơn.

## 🗓️ TO DO (Sắp thực hiện / Cần hoàn thiện)
- [ ] **Hoàn thiện Cổng Lễ tân (Finalize Receptionist Portal)**: Kiểm tra và xóa bỏ toàn bộ các link "trap" (dẫn về trang public) để đảm bảo môi trường làm việc tập trung.
- [ ] **Cải thiện tính ổn định (Build Stability)**: Giải quyết triệt để các lỗi Build phát sinh trong log để đảm bảo CI/CD mượt mà.
- [ ] **Tối ưu hóa Responsive cho Dashboard**: Đảm bảo các bảng dữ liệu lớn (ManageBookings, ManageCustomers) hiển thị tốt trên máy tính bảng.
- [ ] **Hệ thống Quản lý Phòng nâng cao**: Thêm tính năng kéo-thả (drag-and-drop) để đổi phòng nhanh ngay trên Room Map.

## 🚧 IN PROGRESS (Đang thực hiện)
- [/] **Tối ưu hóa UI/UX Admin**: Đang chuyển đổi nốt các trang còn lại sang phong cách "Azure Enterprise" sang trọng.
- [/] **Hoàn thiện Logic Kho (Inventory Flow)**: Kiểm tra tính chính xác của việc trừ tồn kho khi dịch vụ hoàn thành.
- [/] **Refine Room Map**: Cải thiện micro-animations khi chuyển đổi trạng thái phòng (Trống <-> Đang sử dụng).

## ✅ DONE (Đã hoàn thành)
### 1. Hệ thống Đặt lịch (Booking & Success Flow)
- [x] Thiết kế giao diện "Compact Luxury" cho trang đặt lịch.
- [x] Luồng đặt lịch 4 bước hoàn chỉnh (Dịch vụ -> Chi nhánh -> Thời gian -> Thông tin).
- [x] Trang Success tối giản, đẳng cấp.

### 2. Quản lý Nhân viên & Lịch (Staff & Calendar)
- [x] Tích hợp FullCalendar vào Dashboard nhân viên.
- [x] Fix lỗi fetching dữ liệu lịch hẹn (Credential issue).
- [x] Hệ thống phân quyền truy cập chéo (Admin/Receptionist/Staff).

### 3. Sơ đồ Phòng (Room Map System)
- [x] Logic 10 phòng chuyên biệt cho mỗi dịch vụ.
- [x] Tự động gán phòng trống khi khách Check-in.
- [x] Hiển thị trạng thái Real-time (Xanh: Trống, Đỏ: Đang sử dụng).

### 4. Quản trị & Vận hành (Admin & Ops)
- [x] Hệ thống Audit Log (Ghi lại mọi hoạt động thêm/sửa/xóa).
- [x] Quản lý Chi nhánh (Branches) kèm hình ảnh.
- [x] Quản lý Danh mục Dịch vụ và Vật tư (Materials).
- [x] Logic thực hiện tuần tự (Kỹ thuật viên không bị trùng ca làm việc).

### 5. Cơ sở hạ tầng (Core Architecture)
- [x] Database Schema hoàn chỉnh cho Spa (SQlite/EF Core).
- [x] Hệ thống Identity xác thực an toàn.
- [x] Phân tách Area Admin và Area Staff rõ ràng.

---
*Cập nhật lần cuối: 14/04/2026 - Bởi Antigravity AI*
