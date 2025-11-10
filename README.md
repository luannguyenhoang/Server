# HoodLab API

Dự án backend .NET Web API cho hệ thống e-commerce.

## Tính năng

### Quản trị/Nhân viên:
- Quản lý khách hàng, nhân viên, tài khoản hệ thống
- Quản lý sản phẩm, danh mục, thương hiệu, màu sắc, kích cỡ
- Quản lý đơn hàng (xác nhận, hủy, cập nhật trạng thái)
- Thống kê doanh thu theo thời gian

### Khách hàng:
- Đăng ký/Đăng nhập, quản lý thông tin cá nhân
- Tìm kiếm sản phẩm theo tên, danh mục, thương hiệu, giá
- Xem chi tiết sản phẩm với size, màu sắc, hình ảnh
- Quản lý giỏ hàng (thêm, sửa, xóa, cập nhật số lượng)
- Thanh toán trực tuyến qua MoMo (QR code) hoặc Ship

## Yêu cầu

- .NET 8.0 SDK
- SQL Server hoặc SQL Server LocalDB

## Cài đặt và chạy

1. Khôi phục các package:
```bash
dotnet restore
```

2. Tạo database và migration:
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

3. Chạy ứng dụng:
```bash
dotnet run
```

4. Truy cập Swagger UI:
- HTTP: http://localhost:5000/swagger
- HTTPS: https://localhost:5001/swagger

## Cấu hình

### Database Connection
Chỉnh sửa `appsettings.json` để cấu hình connection string:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=HoodLabDb;..."
  }
}
```

### JWT Authentication
Cấu hình JWT trong `appsettings.json`:
```json
{
  "Jwt": {
    "Key": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
    "Issuer": "HoodLab",
    "Audience": "HoodLab"
  }
}
```

### MoMo Payment
Cấu hình thông tin MoMo trong `appsettings.json`:
```json
{
  "MoMo": {
    "PartnerCode": "YOUR_PARTNER_CODE",
    "AccessKey": "YOUR_ACCESS_KEY",
    "SecretKey": "YOUR_SECRET_KEY",
    "ApiEndpoint": "https://test-payment.momo.vn/v2/gateway/api/create"
  }
}
```

## API Endpoints

### Authentication
- `POST /api/auth/register` - Đăng ký
- `POST /api/auth/login` - Đăng nhập

### Users
- `GET /api/users/profile` - Lấy thông tin cá nhân
- `PUT /api/users/profile` - Cập nhật thông tin cá nhân
- `GET /api/users` - Lấy danh sách người dùng (Admin/Staff)
- `PUT /api/users/{id}` - Cập nhật người dùng (Admin/Staff)
- `DELETE /api/users/{id}` - Xóa người dùng (Admin)

### Products
- `GET /api/products` - Lấy danh sách sản phẩm (có filter: search, categoryId, brandId, minPrice, maxPrice)
- `GET /api/products/{id}` - Lấy chi tiết sản phẩm
- `POST /api/products` - Tạo sản phẩm (Admin/Staff)
- `PUT /api/products/{id}` - Cập nhật sản phẩm (Admin/Staff)
- `DELETE /api/products/{id}` - Xóa sản phẩm (Admin)

### Categories
- `GET /api/categories` - Lấy danh sách danh mục
- `GET /api/categories/{id}` - Lấy chi tiết danh mục
- `POST /api/categories` - Tạo danh mục (Admin/Staff)
- `PUT /api/categories/{id}` - Cập nhật danh mục (Admin/Staff)
- `DELETE /api/categories/{id}` - Xóa danh mục (Admin)

### Brands
- `GET /api/brands` - Lấy danh sách thương hiệu
- `GET /api/brands/{id}` - Lấy chi tiết thương hiệu
- `POST /api/brands` - Tạo thương hiệu (Admin/Staff)
- `PUT /api/brands/{id}` - Cập nhật thương hiệu (Admin/Staff)
- `DELETE /api/brands/{id}` - Xóa thương hiệu (Admin)

### Colors & Sizes
- `GET /api/colors` - Lấy danh sách màu sắc
- `POST /api/colors` - Tạo màu sắc (Admin/Staff)
- `GET /api/sizes` - Lấy danh sách kích cỡ
- `POST /api/sizes` - Tạo kích cỡ (Admin/Staff)

### Cart
- `GET /api/cart` - Lấy giỏ hàng
- `POST /api/cart` - Thêm sản phẩm vào giỏ hàng
- `PUT /api/cart/{id}` - Cập nhật số lượng
- `DELETE /api/cart/{id}` - Xóa sản phẩm khỏi giỏ hàng

### Orders
- `POST /api/orders` - Tạo đơn hàng
- `GET /api/orders` - Lấy danh sách đơn hàng
- `GET /api/orders/{id}` - Lấy chi tiết đơn hàng
- `PUT /api/orders/{id}/status` - Cập nhật trạng thái đơn hàng (Admin/Staff)
- `POST /api/orders/{id}/cancel` - Hủy đơn hàng

### Payment
- `POST /api/payment/momo` - Tạo thanh toán MoMo
- `POST /api/payment/ship` - Tạo thanh toán Ship COD
- `POST /api/payment/momo/callback` - Callback từ MoMo

### Statistics
- `GET /api/statistics` - Lấy thống kê doanh thu (Admin/Staff)

## Authentication

Hầu hết các API yêu cầu JWT token. Sau khi đăng nhập, thêm token vào header:
```
Authorization: Bearer YOUR_JWT_TOKEN
```

## Roles

- **Customer**: Khách hàng
- **Staff**: Nhân viên
- **Admin**: Quản trị viên

## Cấu trúc dự án

```
HoodLab.Api/
├── Controllers/          # API Controllers
│   ├── AuthController.cs
│   ├── UsersController.cs
│   ├── ProductsController.cs
│   ├── CategoriesController.cs
│   ├── BrandsController.cs
│   ├── ColorsController.cs
│   ├── SizesController.cs
│   ├── CartController.cs
│   ├── OrdersController.cs
│   ├── PaymentController.cs
│   └── StatisticsController.cs
├── Models/              # Data Models
├── Data/                # DbContext
├── Services/            # Services (JWT, etc.)
├── Program.cs           # Entry point và cấu hình
└── appsettings.json     # Cấu hình ứng dụng
```
