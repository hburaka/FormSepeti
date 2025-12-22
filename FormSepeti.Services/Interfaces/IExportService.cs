using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;

namespace FormSepeti.Services.Interfaces
{
    public interface IExportService
    {
        // Excel Exports
        byte[] ExportLogsToExcel(List<AuditLog> logs);
        byte[] ExportUsersToExcel(List<User> users);
        byte[] ExportFormSubmissionsToExcel(List<FormSubmission> submissions);
        byte[] ExportPackageSalesToExcel(List<UserPackage> userPackages);
        
        // CSV Exports
        string ExportLogsToCsv(List<AuditLog> logs);
        string ExportUsersToCsv(List<User> users);
        string ExportFormSubmissionsToCsv(List<FormSubmission> submissions);
    }
}