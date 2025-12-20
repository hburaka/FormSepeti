using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using FormSepeti.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

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
        private readonly bool _enableSsl;
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
            _enableSsl = bool.Parse(configuration["Smtp:EnableSsl"] ?? "true");
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

        public async Task<bool> SendAccountExistsNotificationAsync(string toEmail)
        {
            try
            {
                var subject = "Hesap Kaydı Denemesi - FormSepeti";
                var forgotPasswordUrl = $"{_baseUrl}/Account/ForgotPassword";
                
                var body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                            <h2 style='color: #6366f1;'>Hesap Kaydı Denemesi</h2>
                            <p>Merhaba,</p>
                            <p>Bu e-posta adresi ile FormSepeti'de zaten bir hesabınız bulunmaktadır.</p>
                            <p>Eğer bu işlemi siz yapmadıysanız, hesabınızın güvenliği için şifrenizi değiştirmenizi öneririz.</p>
                            <p>Şifrenizi unuttuysanız, <a href='{forgotPasswordUrl}'>şifre sıfırlama</a> sayfasını kullanabilirsiniz.</p>
                            <br>
                            <p style='color: #64748b; font-size: 12px;'>
                                Bu e-posta otomatik olarak gönderilmiştir. Lütfen yanıtlamayın.
                            </p>
                        </div>
                    </body>
                    </html>";

                return await SendEmailAsync(toEmail, subject, body, "AccountExists");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending account exists notification to {Email}", toEmail);
                return false;
            }
        }

        public async Task<bool> SendFormSubmissionNotificationAsync(string toEmail, string formTitle, int submissionCount)
        {
            var subject = "Yeni Form Yanıtı Bildirimi";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #6366f1;'>Yeni Form Yanıtı</h2>
                        <p>Formunuz (<strong>{formTitle}</strong>) için yeni bir yanıt alındı.</p>
                        <p>Toplam yanıt sayısı: <strong>{submissionCount}</strong></p>
                        <br>
                        <p style='color: #64748b; font-size: 12px;'>
                            Bu e-posta otomatik olarak gönderilmiştir. Lütfen yanıtlamayın.
                        </p>
                    </div>
                </body>
                </html>";
            return await SendEmailAsync(toEmail, subject, body, "FormSubmission");
        }

        private async Task<bool> SendEmailAsync(string toEmail, string subject, string body, string emailType)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_fromName, _fromEmail));
                message.To.Add(new MailboxAddress("", toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = body
                };
                message.Body = bodyBuilder.ToMessageBody();

                using (var smtp = new SmtpClient())
                {
                    smtp.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
                    await smtp.ConnectAsync(_smtpHost, _smtpPort, SecureSocketOptions.None);
                    await smtp.AuthenticateAsync(_smtpUsername, _smtpPassword);
                    await smtp.SendAsync(message);
                    await smtp.DisconnectAsync(true);
                }

                await LogEmail(null, toEmail, subject, emailType, true, null);
                _logger.LogInformation("Email sent successfully to {Email}", toEmail);
                return true;
            }
            catch (SmtpCommandException ex)
            {
                var errorMsg = $"SMTP Command Error: {ex.Message}, StatusCode: {ex.StatusCode}";
                await LogEmail(null, toEmail, subject, emailType, false, errorMsg);
                _logger.LogError(ex, "SMTP Command Error for {Email}", toEmail);
                return false;
            }
            catch (SmtpProtocolException ex)
            {
                var errorMsg = $"SMTP Protocol Error: {ex.Message}";
                await LogEmail(null, toEmail, subject, emailType, false, errorMsg);
                _logger.LogError(ex, "SMTP Protocol Error for {Email}", toEmail);
                return false;
            }
            catch (Exception ex)
            {
                await LogEmail(null, toEmail, subject, emailType, false, ex.Message);
                _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
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
            <p style='color: #999; font-size: 12px; margin-top: 20px;'>
                <strong>Not:</strong> Bu bağlantı 1 saat geçerlidir. Eğer bu talebi siz yapmadıysanız, bu e-postayı görmezden gelebilirsiniz.
            </p>
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