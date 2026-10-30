# CTU Stay

Nền tảng tìm và quản lý phòng trọ sinh viên Cần Thơ. [Tài liệu kiến trúc, ERD, use cases và API](docs/architecture.md).

## Tình trạng đã kiểm chứng
Frontend React/Vite: `npm run build` thành công; dev server trả HTTP 200. Backend ASP.NET Core 8: `dotnet restore` và `dotnet build` thành công, Swagger trả HTTP 200 và liệt kê 30 đường dẫn; EF Core tạo được migration `InitialCreate` và SQL script với 7 bảng nghiệp vụ, ràng buộc CHECK và indexes. Chưa chạy migration trên SQL Server hoặc kiểm thử API với dữ liệu thật vì môi trường bị HTTP 403 khi tải image SQL Server từ Microsoft Container Registry. Chưa hoàn thiện toàn bộ chức năng đồ án; xem phần việc còn lại bên dưới.

## Chạy frontend
```sh
cd frontend
npm ci
npm run dev
npm run build
```
Vite proxy `/api` đến `http://localhost:5050`.

## Chạy backend
Cần .NET SDK 8 và SQL Server. Đặt `ConnectionStrings__Housing`, `Jwt__Key` (ít nhất 32 byte), `Cloudinary__CloudName`, `Cloudinary__ApiKey`, `Cloudinary__ApiSecret` bằng biến môi trường hoặc user secrets. Không commit giá trị thật. Để tạo tài khoản Admin lần đầu, đặt thêm `Bootstrap__AdminEmail` và `Bootstrap__AdminPassword` (ít nhất 12 ký tự) khi chạy API, sau đó gỡ các biến này khỏi phiên làm việc. Tài khoản Admin không thể được tạo qua `/api/auth/register`.
```sh
cd backend/StudentHousing.Api
dotnet restore
dotnet tool install --global dotnet-ef --version 8.0.11
dotnet ef database update
dotnet run --urls http://localhost:5050
```
Swagger ở `/swagger` khi `ASPNETCORE_ENVIRONMENT=Development`. Nếu dùng SQL Server container ở máy cá nhân, cần bật cổng và đặt mật khẩu SA riêng bằng cấu hình cục bộ.

## Phạm vi cần hoàn thiện trước khi demo
- Chạy migration trên SQL Server, kiểm tra API với dữ liệu thật, thêm dữ liệu demo và tests xác thực, lọc, workflow, ownership.
- Tách logic trong controller thành services và repositories theo kiến trúc đã thiết kế; bổ sung validation và xử lý lỗi nhất quán.
- Hoàn thiện lọc tiện ích trên giao diện, dashboard biểu đồ và các trang còn thiếu thao tác; kiểm tra responsive, accessibility và trạng thái phiên đăng nhập.
- Tích hợp frontend/backend và sửa lỗi từ smoke tests. Hiện chưa thể coi dự án là bản fullstack hoàn chỉnh.
