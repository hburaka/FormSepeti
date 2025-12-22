using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FormSepeti.Data.Entities;
using FormSepeti.Services.Interfaces;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace FormSepeti.Services.Implementations
{
    public class ExportService : IExportService
    {
        public ExportService()
        {
            // EPPlus lisans ayarı (Non-Commercial kullanım için)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }
        #region Excel Exports


        public byte[] ExportLogsToExcel(List<AuditLog> logs)
        {
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Audit Logs");

            // Header
            worksheet.Cells[1, 1].Value = "Tarih";
            worksheet.Cells[1, 2].Value = "Kullanıcı Tipi";
            worksheet.Cells[1, 3].Value = "Kullanıcı";
            worksheet.Cells[1, 4].Value = "Action";
            worksheet.Cells[1, 5].Value = "Entity Type";
            worksheet.Cells[1, 6].Value = "Entity ID";
            worksheet.Cells[1, 7].Value = "Detaylar";
            worksheet.Cells[1, 8].Value = "IP Adresi";

            // Header styling
            using (var range = worksheet.Cells[1, 1, 1, 8])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }

            // Data
            int row = 2;
            foreach (var log in logs.OrderByDescending(l => l.CreatedDate))
            {
                worksheet.Cells[row, 1].Value = log.CreatedDate.ToString("dd.MM.yyyy HH:mm:ss");
                worksheet.Cells[row, 2].Value = log.AdminId.HasValue ? "Admin" : "User";
                worksheet.Cells[row, 3].Value = log.AdminUser?.FullName ?? log.User?.Email ?? "-";
                worksheet.Cells[row, 4].Value = log.Action;
                worksheet.Cells[row, 5].Value = log.EntityType ?? "-";
                worksheet.Cells[row, 6].Value = log.EntityId?.ToString() ?? "-";
                worksheet.Cells[row, 7].Value = log.Details ?? "-";
                worksheet.Cells[row, 8].Value = log.IpAddress ?? "-";
                row++;
            }

            // Auto-fit columns
            worksheet.Cells.AutoFitColumns();

            return package.GetAsByteArray();
        }

        public byte[] ExportUsersToExcel(List<User> users)
        {
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Kullanıcılar");

            // Header
            worksheet.Cells[1, 1].Value = "ID";
            worksheet.Cells[1, 2].Value = "Email";
            worksheet.Cells[1, 3].Value = "Telefon";
            worksheet.Cells[1, 4].Value = "Google ID";
            worksheet.Cells[1, 5].Value = "Durum";
            worksheet.Cells[1, 6].Value = "Email Doğrulandı";
            worksheet.Cells[1, 7].Value = "Kayıt Tarihi";
            worksheet.Cells[1, 8].Value = "Son Giriş";

            // Header styling
            using (var range = worksheet.Cells[1, 1, 1, 8])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }

            // Data
            int row = 2;
            foreach (var user in users.OrderByDescending(u => u.CreatedDate))
            {
                worksheet.Cells[row, 1].Value = user.UserId;
                worksheet.Cells[row, 2].Value = user.Email;
                worksheet.Cells[row, 3].Value = user.PhoneNumber ?? "-";
                worksheet.Cells[row, 4].Value = user.GoogleId ?? "-";
                worksheet.Cells[row, 5].Value = user.IsActive ? "Aktif" : "Pasif";
                worksheet.Cells[row, 6].Value = user.IsActivated ? "Evet" : "Hayır";
                worksheet.Cells[row, 7].Value = user.CreatedDate.ToString("dd.MM.yyyy HH:mm");
                worksheet.Cells[row, 8].Value = user.LastLoginDate?.ToString("dd.MM.yyyy HH:mm") ?? "-";
                row++;
            }

            worksheet.Cells.AutoFitColumns();
            return package.GetAsByteArray();
        }

        public byte[] ExportFormSubmissionsToExcel(List<FormSubmission> submissions)
        {
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Form Gönderimler");

            // Header
            worksheet.Cells[1, 1].Value = "Tarih";
            worksheet.Cells[1, 2].Value = "Kullanıcı";
            worksheet.Cells[1, 3].Value = "Form";
            worksheet.Cells[1, 4].Value = "Grup";
            worksheet.Cells[1, 5].Value = "JotForm Submission ID";
            worksheet.Cells[1, 6].Value = "Durum";
            worksheet.Cells[1, 7].Value = "Google Sheet Row"; // ✅ DÜZELTİLDİ

            using (var range = worksheet.Cells[1, 1, 1, 7])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightYellow);
                range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }

            int row = 2;
            foreach (var submission in submissions.OrderByDescending(s => s.SubmittedDate))
            {
                worksheet.Cells[row, 1].Value = submission.SubmittedDate.ToString("dd.MM.yyyy HH:mm");
                worksheet.Cells[row, 2].Value = submission.User?.Email ?? "-";
                worksheet.Cells[row, 3].Value = submission.Form?.FormName ?? "-";
                worksheet.Cells[row, 4].Value = submission.FormGroup?.GroupName ?? "-";
                worksheet.Cells[row, 5].Value = submission.JotFormSubmissionId ?? "-";
                worksheet.Cells[row, 6].Value = submission.Status;
                worksheet.Cells[row, 7].Value = submission.GoogleSheetRowNumber?.ToString() ?? "-";
                row++;
            }

            worksheet.Cells.AutoFitColumns();
            return package.GetAsByteArray();
        }

        public byte[] ExportPackageSalesToExcel(List<UserPackage> userPackages)
        {
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Paket Satışları");

            // Header
            worksheet.Cells[1, 1].Value = "Satın Alma Tarihi";
            worksheet.Cells[1, 2].Value = "Kullanıcı";
            worksheet.Cells[1, 3].Value = "Paket";
            worksheet.Cells[1, 4].Value = "Grup";
            worksheet.Cells[1, 5].Value = "Tutar (TL)";
            worksheet.Cells[1, 6].Value = "Aktivasyon Tarihi";
            worksheet.Cells[1, 7].Value = "Bitiş Tarihi";
            worksheet.Cells[1, 8].Value = "Durum";

            using (var range = worksheet.Cells[1, 1, 1, 8])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightCoral);
                range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }

            int row = 2;
            foreach (var up in userPackages.OrderByDescending(u => u.PurchaseDate))
            {
                worksheet.Cells[row, 1].Value = up.PurchaseDate.ToString("dd.MM.yyyy HH:mm");
                worksheet.Cells[row, 2].Value = up.User?.Email ?? "-";
                worksheet.Cells[row, 3].Value = up.Package?.PackageName ?? "-";
                worksheet.Cells[row, 4].Value = up.FormGroup?.GroupName ?? "-";
                worksheet.Cells[row, 5].Value = up.PaymentAmount;
                worksheet.Cells[row, 6].Value = up.ActivationDate?.ToString("dd.MM.yyyy") ?? "-";
                worksheet.Cells[row, 7].Value = up.ExpiryDate?.ToString("dd.MM.yyyy") ?? "Süresiz";
                worksheet.Cells[row, 8].Value = up.IsActive ? "Aktif" : "Pasif";
                row++;
            }

            worksheet.Cells.AutoFitColumns();

            // Format currency column
            worksheet.Column(5).Style.Numberformat.Format = "#,##0.00";

            return package.GetAsByteArray();
        }

        #endregion

        #region CSV Exports

        public string ExportLogsToCsv(List<AuditLog> logs)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Tarih,Kullanıcı Tipi,Kullanıcı,Action,Entity Type,Entity ID,Detaylar,IP Adresi");

            foreach (var log in logs.OrderByDescending(l => l.CreatedDate))
            {
                csv.AppendLine($"\"{log.CreatedDate:dd.MM.yyyy HH:mm:ss}\"," +
                              $"\"{(log.AdminId.HasValue ? "Admin" : "User")}\"," +
                              $"\"{EscapeCsv(log.AdminUser?.FullName ?? log.User?.Email ?? "-")}\"," +
                              $"\"{EscapeCsv(log.Action)}\"," +
                              $"\"{EscapeCsv(log.EntityType ?? "-")}\"," +
                              $"\"{log.EntityId?.ToString() ?? "-"}\"," +
                              $"\"{EscapeCsv(log.Details ?? "-")}\"," +
                              $"\"{EscapeCsv(log.IpAddress ?? "-")}\"");
            }

            return csv.ToString();
        }

        public string ExportUsersToCsv(List<User> users)
        {
            var csv = new StringBuilder();
            csv.AppendLine("ID,Email,Telefon,Google ID,Durum,Email Doğrulandı,Kayıt Tarihi,Son Giriş");

            foreach (var user in users.OrderByDescending(u => u.CreatedDate))
            {
                csv.AppendLine($"{user.UserId}," +
                              $"\"{EscapeCsv(user.Email)}\"," +
                              $"\"{EscapeCsv(user.PhoneNumber ?? "-")}\"," +
                              $"\"{EscapeCsv(user.GoogleId ?? "-")}\"," +
                              $"\"{(user.IsActive ? "Aktif" : "Pasif")}\"," +
                              $"\"{(user.IsActivated ? "Evet" : "Hayır")}\"," +
                              $"\"{user.CreatedDate:dd.MM.yyyy HH:mm}\"," +
                              $"\"{user.LastLoginDate?.ToString("dd.MM.yyyy HH:mm") ?? "-"}\"");
            }

            return csv.ToString();
        }

        public string ExportFormSubmissionsToCsv(List<FormSubmission> submissions)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Tarih,Kullanıcı,Form,Grup,JotForm Submission ID,Durum,Google Sheet Row");

            foreach (var submission in submissions.OrderByDescending(s => s.SubmittedDate))
            {
                csv.AppendLine($"\"{submission.SubmittedDate:dd.MM.yyyy HH:mm}\"," +
                              $"\"{EscapeCsv(submission.User?.Email ?? "-")}\"," +
                              $"\"{EscapeCsv(submission.Form?.FormName ?? "-")}\"," +
                              $"\"{EscapeCsv(submission.FormGroup?.GroupName ?? "-")}\"," +
                              $"\"{EscapeCsv(submission.JotFormSubmissionId ?? "-")}\"," +
                              $"\"{EscapeCsv(submission.Status)}\"," +
                              $"\"{submission.GoogleSheetRowNumber?.ToString() ?? "-"}\"");
            }

            return csv.ToString();
        }

        private string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\"", "\"\"");
        }

        #endregion
    }
}