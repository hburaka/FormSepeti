using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;

namespace FormSepeti.Services.Interfaces
{
    public interface IGoogleSheetsService
    {
        Task<string> GetAuthorizationUrl(int userId);
        Task<bool> HandleOAuthCallback(int userId, string code);
        Task<string> CreateSpreadsheetForUserGroup(int userId, int groupId, string groupName);
        Task<bool> CreateSheetTabForForm(int userId, int groupId, string formName, List<string> headers);
        Task<int> AppendFormDataToSheet(int userId, int groupId, string formName, Dictionary<string, string> formData);
        Task<bool> RefreshAccessToken(int userId);
        Task<UserGoogleSheet> GetUserGoogleSheetAsync(int userId, int groupId);
        Task<List<UserGoogleSheet>> GetUserSheetsAsync(int userId);
        Task<bool> TestConnectionAsync(int userId);
    }
}