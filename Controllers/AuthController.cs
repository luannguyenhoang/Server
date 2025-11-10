using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HoodLab.Api.Data;
using HoodLab.Api.Models;
using HoodLab.Api.Services;
using BCrypt.Net;
using System.Security.Cryptography;

namespace HoodLab.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly JwtService _jwtService;
    private readonly EmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ApplicationDbContext context, 
        JwtService jwtService, 
        EmailService emailService,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<LoginResponse>> Register(RegisterRequest request)
    {
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            return BadRequest(new { message = "Email đã tồn tại" });
        }

        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = request.FullName,
            Phone = request.Phone,
            Address = request.Address,
            Role = "Customer"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = _jwtService.GenerateToken(user.Id, user.Email, user.Role);
        var userDto = new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            Address = user.Address,
            Role = user.Role,
            IsActive = user.IsActive
        };

        return Ok(new LoginResponse { Token = token, User = userDto });
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng" });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new { message = "Tài khoản đã bị khóa" });
        }

        var token = _jwtService.GenerateToken(user.Id, user.Email, user.Role);
        var userDto = new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            Address = user.Address,
            Role = user.Role,
            IsActive = user.IsActive
        };

        return Ok(new LoginResponse { Token = token, User = userDto });
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        // Always return success to prevent email enumeration
        if (user == null)
        {
            return Ok(new { message = "Nếu email tồn tại, chúng tôi đã gửi link đặt lại mật khẩu đến email của bạn." });
        }

        if (!user.IsActive)
        {
            return Ok(new { message = "Nếu email tồn tại, chúng tôi đã gửi link đặt lại mật khẩu đến email của bạn." });
        }

        // Invalidate old tokens for this user
        var oldTokens = await _context.PasswordResetTokens
            .Where(t => t.UserId == user.Id && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();
        
        foreach (var oldToken in oldTokens)
        {
            oldToken.IsUsed = true;
        }

        // Generate new reset token
        var token = GenerateResetToken();
        var resetToken = new PasswordResetToken
        {
            UserId = user.Id,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddHours(1), // Token expires in 1 hour
            IsUsed = false
        };

        _context.PasswordResetTokens.Add(resetToken);
        await _context.SaveChangesAsync();

        // Generate reset link
        var frontendUrl = _configuration["Frontend:Url"] ?? "http://localhost:3000";
        var resetLink = $"{frontendUrl}/reset-password?token={token}";

        // Send email
        var emailSent = await _emailService.SendPasswordResetEmailAsync(user.Email, token, resetLink);

        // In development, if email is not configured, return the token in response
        var isDevelopment = _configuration["ASPNETCORE_ENVIRONMENT"] == "Development" || 
                            string.IsNullOrEmpty(_configuration["Email:SmtpHost"]);
        
        if (isDevelopment && !emailSent)
        {
            _logger.LogWarning("⚠️ DEVELOPMENT MODE: Email chưa được cấu hình. Token reset password: {Token}", token);
            _logger.LogWarning("⚠️ Link reset password: {Link}", resetLink);
            return Ok(new { 
                message = "Nếu email tồn tại, chúng tôi đã gửi link đặt lại mật khẩu đến email của bạn.",
                developmentToken = token, // Chỉ trả về trong development
                developmentLink = resetLink // Chỉ trả về trong development
            });
        }

        return Ok(new { message = "Nếu email tồn tại, chúng tôi đã gửi link đặt lại mật khẩu đến email của bạn." });
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult> ResetPassword(ResetPasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmPassword)
        {
            return BadRequest(new { message = "Mật khẩu mới và xác nhận mật khẩu không khớp." });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự." });
        }

        var resetToken = await _context.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == request.Token);

        if (resetToken == null)
        {
            return BadRequest(new { message = "Token không hợp lệ hoặc đã hết hạn." });
        }

        if (resetToken.IsUsed)
        {
            return BadRequest(new { message = "Link đặt lại mật khẩu đã được sử dụng." });
        }

        if (resetToken.ExpiresAt < DateTime.UtcNow)
        {
            return BadRequest(new { message = "Token đã hết hạn." });
        }

        if (resetToken.User == null)
        {
            return BadRequest(new { message = "Người dùng không tồn tại." });
        }

        // Update password
        resetToken.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        resetToken.User.UpdatedAt = DateTime.UtcNow;

        // Mark token as used
        resetToken.IsUsed = true;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Mật khẩu đã được đặt lại thành công. Vui lòng đăng nhập với mật khẩu mới." });
    }

    private string GenerateResetToken()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        var token = Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
        return token.Length > 32 ? token.Substring(0, 32) : token;
    }
}


