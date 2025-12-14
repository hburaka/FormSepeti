using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using FormSepeti.Services.Interfaces;

namespace FormSepeti.Services.Implementations
{
    public class EmailService : IEmailService
    {
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;
        private readonly string _fromName;
        private readonly string _baseUrl;
        private readonly IEmailLogRepository _emailLogRepository;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IConfiguration configuration,
            IEmailLogRepository emailLogRepository,
            ILogger<EmailService> logger)
        {
            _smtpHost = configuration["Smtp:Host"];
            _smtpPort = int.Parse(configuration["Smtp:Port"]);
            _smtpUsername = configuration["Smtp:Username"];
            _smtpPassword = configuration["Smtp:Password"];
            _fromEmail = configuration["Smtp:FromEmail"];
            _fromName = configuration["Smtp:FromName"];
            _baseUrl = configuration["Application:BaseUrl"];
            _emailLogRepository = emailLogRepository;
            _logger = logger;
        }

        public async Task<bool> SendActivationEmailAsync(string toEmail, string userName, string activationToken)
        {
            var activationLink = $"{_baseUrl}/Account/Activate?token={activationToken}";
            var subject = "Hesabınızı Aktive Edin";
            var body = GetActivationEmailTemplate(userName, activationLink);
            return await SendEmailAsync(toEmail, subject, body, "Activation");
        }

        public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string userName, string resetToken)
        {
            var resetLink = $"{_baseUrl}/Account/ResetPassword?token={resetToken}";
            var subject = "Şifre Sıfırlama Talebi";
            var body = GetPasswordResetEmailTemplate(userName, resetLink);
            return await SendEmailAsync(toEmail, subject, body, "PasswordReset");
        }

        public async Task<bool> SendWelcomeEmailAsync(string toEmail, string userName)
        {
            var subject = "Hoş Geldiniz!";
            var body = GetWelcomeEmailTemplate(userName);
            return await SendEmailAsync(toEmail, subject, body, "Welcome");
        }

        public async Task<bool> SendPackagePurchaseConfirmationAsync(string toEmail, string userName, string packageName, decimal amount)
        {
            var subject = "Paket Satın Alma Onayı";
            var body = GetPackagePurchaseEmailTemplate(userName, packageName, amount);
            return await SendEmailAsync(toEmail, subject, body, "PackagePurchase");
        }

        public async Task<bool> SendCustomEmailAsync(string toEmail, string subject, string body)
        {
            return await SendEmailAsync(toEmail, subject, body, "Custom");
        }

        private async Task<bool> SendEmailAsync(string toEmail, string subject, string body, string emailType)
        {
            try
            {
                using (var smtpClient = new SmtpClient(_smtpHost, _smtpPort))
                {
                    smtpClient.EnableSsl = true;
                    smtpClient.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(_fromEmail, _fromName),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true
                    };

                    mailMessage.To.Add(toEmail);
                    await smtpClient.SendMailAsync(mailMessage);
                    await LogEmail(null, toEmail, subject, emailType, true, null);
                    _logger.LogInformation($"Email sent successfully to {toEmail}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                await LogEmail(null, toEmail, subject, emailType, false, ex.Message);
                _logger.LogError(ex, $"Failed to send email to {toEmail}");
                return false;
            }
        }

        private async Task LogEmail(int? userId, string emailTo, string subject, string emailType, bool isSuccess, string errorMessage)
        {
            var log = new EmailLog
            {
                UserId = userId,
                EmailTo = emailTo,
                Subject = subject,
                EmailType = emailType,
                SentDate = DateTime.UtcNow,
                IsSuccess = isSuccess,
                ErrorMessage = errorMessage
            };
            await _emailLogRepository.CreateAsync(log);
        }

        private string GetActivationEmailTemplate(string userName, string activationLink)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }}
        .container {{ background-color: #ffffff; max-width: 600px; margin: 0 auto; padding: 40px; border-radius: 10px; }}
        .header {{ text-align: center; margin-bottom: 30px; }}
        .header h1 {{ color: #2c3e50; margin: 0; }}
        .content {{ line-height: 1.6; color: #555; }}
        .button {{ display: inline-block; background-color: #3498db; color: #ffffff; text-decoration: none; padding: 15px 30px; border-radius: 5px; margin-top: 20px; font-weight: bold; }}
        .footer {{ margin-top: 30px; text-align: center; color: #999; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'><h1>Hesabınızı Aktive Edin</h1></div>
        <div class='content'>
            <p>Merhaba <strong>{userName}</strong>,</p>
            <p>Kaydınız başarıyla oluşturuldu! Hesabınızı aktive etmek için aşağıdaki butona tıklayın:</p>
            <div style='text-align: center;'>
                <a href='{activationLink}' class='button'>Hesabımı Aktive Et</a>
            </div>
            <p style='margin-top: 20px;'>Link: {activationLink}</p>
        </div>
        <div class='footer'><p>&copy; 2024 FormSepeti</p></div>
    </div>
</body>
</html>";
        }

        private string GetPasswordResetEmailTemplate(string userName, string resetLink)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }}
        .container {{ background-color: #ffffff; max-width: 600px; margin: 0 auto; padding: 40px; border-radius: 10px; }}
        .header {{ text-align: center; margin-bottom: 30px; }}
        .header h1 {{ color: #e74c3c; margin: 0; }}
        .content {{ line-height: 1.6; color: #555; }}
        .button {{ display: inline-block; background-color: #e74c3c; color: #ffffff; text-decoration: none; padding: 15px 30px; border-radius: 5px; margin-top: 20px; font-weight: bold; }}
        .footer {{ margin-top: 30px; text-align: center; color: #999; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'><h1>Şifre Sıfırlama</h1></div>
        <div class='content'>
            <p>Merhaba <strong>{userName}</strong>,</p>
            <p>Şifrenizi sıfırlamak için bir talep aldık.</p>
            <div style='text-align: center;'>
                <a href='{resetLink}' class='button'>Şifremi Sıfırla</a>
            </div>
            <p style='margin-top: 20px;'>Link: {resetLink}</p>
        </div>
        <div class='footer'><p>&copy; 2024 FormSepeti</p></div>
    </div>
</body>
</html>";
        }

        private string GetWelcomeEmailTemplate(string userName)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }}
        .container {{ background-color: #ffffff; max-width: 600px; margin: 0 auto; padding: 40px; border-radius: 10px; }}
        .header {{ text-align: center; margin-bottom: 30px; }}
        .header h1 {{ color: #27ae60; margin: 0; }}
        .content {{ line-height: 1.6; color: #555; }}
        .footer {{ margin-top: 30px; text-align: center; color: #999; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'><h1>Hoş Geldiniz!</h1></div>
        <div class='content'>
            <p>Merhaba <strong>{userName}</strong>,</p>
            <p>Aramıza hoş geldiniz! Hesabınız başarıyla aktive edildi.</p>
        </div>
        <div class='footer'><p>&copy; 2024 FormSepeti</p></div>
    </div>
</body>
</html>";
        }

        private string GetPackagePurchaseEmailTemplate(string userName, string packageName, decimal amount)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }}
        .container {{ background-color: #ffffff; max-width: 600px; margin: 0 auto; padding: 40px; border-radius: 10px; }}
        .header {{ text-align: center; margin-bottom: 30px; }}
        .header h1 {{ color: #16a085; margin: 0; }}
        .content {{ line-height: 1.6; color: #555; }}
        .footer {{ margin-top: 30px; text-align: center; color: #999; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'><h1>✓ Ödeme Başarılı</h1></div>
        <div class='content'>
            <p>Merhaba <strong>{userName}</strong>,</p>
            <p>Paket: <strong>{packageName}</strong></p>
            <p>Tutar: <strong>{amount:C2} TL</strong></p>
        </div>
        <div class='footer'><p>&copy; 2024 FormSepeti</p></div>
    </div>
</body>
</html>";
        }
    }
}