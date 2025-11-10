using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace HoodLab.Api.Services;

public class EmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendPasswordResetEmailAsync(string email, string resetToken, string resetLink)
    {
        try
        {
            var smtpHost = _configuration["Email:SmtpHost"];
            var smtpPort = _configuration.GetValue<int>("Email:SmtpPort", 587);
            var smtpUsername = _configuration["Email:SmtpUsername"];
            var smtpPassword = _configuration["Email:SmtpPassword"];
            var fromEmail = _configuration["Email:FromEmail"] ?? "noreply@hoodlab.com";
            var fromName = _configuration["Email:FromName"] ?? "HoodLab";

            // If email is not configured, just log it (for development)
            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUsername))
            {
                _logger.LogWarning("⚠️ Email không được cấu hình. Token reset password: {Token}", resetToken);
                _logger.LogWarning("⚠️ Link reset password: {Link}", resetLink);
                return false; // Return false to indicate email was not sent
            }

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(smtpUsername, smtpPassword)
            };

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = "Đặt lại mật khẩu - HoodLab",
                Body = $@"
                    <html>
                    <body>
                        <h2>Yêu cầu đặt lại mật khẩu</h2>
                        <p>Xin chào,</p>
                        <p>Bạn đã yêu cầu đặt lại mật khẩu cho tài khoản của mình.</p>
                        <p>Vui lòng click vào link sau để đặt lại mật khẩu:</p>
                        <p><a href=""{resetLink}"" style=""background-color: #4F46E5; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;"">Đặt lại mật khẩu</a></p>
                        <p>Hoặc copy link sau vào trình duyệt:</p>
                        <p>{resetLink}</p>
                        <p>Link này sẽ hết hạn sau 1 giờ.</p>
                        <p>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
                        <br>
                        <p>Trân trọng,<br>Đội ngũ HoodLab</p>
                    </body>
                    </html>",
                IsBodyHtml = true
            };

            message.To.Add(email);

            await client.SendMailAsync(message);
            _logger.LogInformation("Email reset password đã được gửi đến {Email}", email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi email reset password đến {Email}", email);
            // Log the token for development purposes
            _logger.LogWarning("Token reset password (development): {Token}", resetToken);
            _logger.LogWarning("Link reset password (development): {Link}", resetLink);
            return false;
        }
    }
}

