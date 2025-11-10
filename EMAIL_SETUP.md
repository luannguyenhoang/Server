# Hướng dẫn cấu hình Email cho chức năng Quên mật khẩu

## Cách 1: Sử dụng Gmail (Khuyến nghị cho development)

### Bước 1: Tạo App Password cho Gmail

1. Đăng nhập vào tài khoản Gmail của bạn
2. Vào [Google Account Settings](https://myaccount.google.com/)
3. Chọn **Security** (Bảo mật)
4. Bật **2-Step Verification** (Xác minh 2 bước) nếu chưa bật
5. Tìm **App passwords** (Mật khẩu ứng dụng)
6. Chọn **Mail** và **Other (Custom name)**
7. Nhập tên: "HoodLab API"
8. Click **Generate**
9. Copy mật khẩu 16 ký tự được tạo (ví dụ: `abcd efgh ijkl mnop`)

### Bước 2: Cấu hình trong appsettings.json

Mở file `Backend/appsettings.json` và cập nhật phần Email:

```json
{
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "your-16-char-app-password",
    "FromEmail": "your-email@gmail.com",
    "FromName": "HoodLab"
  }
}
```

**Lưu ý:**
- `SmtpUsername`: Email Gmail của bạn
- `SmtpPassword`: App Password 16 ký tự (không có khoảng trắng)
- `FromEmail`: Có thể khác với SmtpUsername nếu muốn

## Cách 2: Sử dụng Outlook/Hotmail

```json
{
  "Email": {
    "SmtpHost": "smtp-mail.outlook.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-email@outlook.com",
    "SmtpPassword": "your-password",
    "FromEmail": "your-email@outlook.com",
    "FromName": "HoodLab"
  }
}
```

## Cách 3: Sử dụng SendGrid (Khuyến nghị cho production)

1. Đăng ký tài khoản tại [SendGrid](https://sendgrid.com/)
2. Tạo API Key
3. Cấu hình:

```json
{
  "Email": {
    "SmtpHost": "smtp.sendgrid.net",
    "SmtpPort": 587,
    "SmtpUsername": "apikey",
    "SmtpPassword": "your-sendgrid-api-key",
    "FromEmail": "noreply@yourdomain.com",
    "FromName": "HoodLab"
  }
}
```

## Cách 4: Sử dụng SMTP Server khác

Nếu bạn có SMTP server riêng:

```json
{
  "Email": {
    "SmtpHost": "smtp.yourdomain.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-username",
    "SmtpPassword": "your-password",
    "FromEmail": "noreply@yourdomain.com",
    "FromName": "HoodLab"
  }
}
```

## Development Mode (Không cấu hình email)

Nếu bạn chưa cấu hình email, hệ thống sẽ:
- Log token và link vào console
- Trả về token/link trong response (chỉ trong development)
- Hiển thị token/link trên trang forgot-password

**Lưu ý:** Trong production, bạn PHẢI cấu hình email để bảo mật!

## Test Email

Sau khi cấu hình, test bằng cách:
1. Vào trang `/forgot-password`
2. Nhập email của bạn
3. Kiểm tra hộp thư đến
4. Click vào link trong email để đặt lại mật khẩu

## Troubleshooting

### Lỗi: "The SMTP server requires a secure connection"
- Đảm bảo `SmtpPort` là 587 (TLS) hoặc 465 (SSL)
- Kiểm tra `EnableSsl = true` trong EmailService

### Lỗi: "Authentication failed"
- Kiểm tra lại username và password
- Với Gmail: Đảm bảo đã tạo App Password, không dùng mật khẩu thường
- Với Outlook: Có thể cần bật "Less secure app access"

### Email không đến
- Kiểm tra thư mục Spam
- Kiểm tra logs trong console để xem có lỗi gì không
- Test với email khác

