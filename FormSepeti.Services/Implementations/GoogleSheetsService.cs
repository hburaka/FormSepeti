using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using FormSepeti.Services.Interfaces;

namespace FormSepeti.Services.Implementations
{
    public class GoogleSheetsService : IGoogleSheetsService
    {
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _redirectUri;
        private readonly IUserRepository _userRepository;
        private readonly IUserGoogleSheetsRepository _sheetsRepository;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<GoogleSheetsService> _logger; // ✅ EKLENDI

        private static readonly string[] Scopes = {
            SheetsService.Scope.Spreadsheets,
            DriveService.Scope.DriveFile
        };

        public GoogleSheetsService(
            IConfiguration configuration,
            IUserRepository userRepository,
            IUserGoogleSheetsRepository sheetsRepository,
            IEncryptionService encryptionService,
            ILogger<GoogleSheetsService> logger) // ✅ EKLENDI
        {
            _clientId = configuration["Google:ClientId"];
            _clientSecret = configuration["Google:ClientSecret"];
            _redirectUri = configuration["Google:RedirectUri"];
            _userRepository = userRepository;
            _sheetsRepository = sheetsRepository;
            _encryptionService = encryptionService;
            _logger = logger; // ✅ EKLENDI
        }

        public Task<string> GetAuthorizationUrl(int userId)
        {
            var clientId = _clientId;
            var redirect = _redirectUri;
            var scopes = "https://www.googleapis.com/auth/spreadsheets https://www.googleapis.com/auth/drive.file";

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirect))
            {
                _logger.LogWarning("GetAuthorizationUrl: ClientId or RedirectUri is empty");
                return Task.FromResult(string.Empty);
            }

            var url =
                "https://accounts.google.com/o/oauth2/v2/auth" +
                "?response_type=code" +
                "&access_type=offline" +
                "&prompt=consent" +
                "&client_id=" + Uri.EscapeDataString(clientId) +
                "&redirect_uri=" + Uri.EscapeDataString(redirect) +
                "&scope=" + Uri.EscapeDataString(scopes) +
                "&state=" + Uri.EscapeDataString(userId.ToString());

            _logger.LogInformation($"GetAuthorizationUrl: Generated for UserId={userId}");
            return Task.FromResult(url);
        }

        public async Task<bool> HandleOAuthCallback(int userId, string code)
        {
            try
            {
                _logger.LogInformation($"HandleOAuthCallback: Starting for UserId={userId}");

                var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = new ClientSecrets
                    {
                        ClientId = _clientId,
                        ClientSecret = _clientSecret
                    },
                    Scopes = Scopes
                });

                var tokenResponse = await flow.ExchangeCodeForTokenAsync(
                    userId.ToString(),
                    code,
                    _redirectUri,
                    System.Threading.CancellationToken.None
                );

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogError($"HandleOAuthCallback: User not found UserId={userId}");
                    return false;
                }

                user.GoogleAccessToken = _encryptionService.Encrypt(tokenResponse.AccessToken);
                user.GoogleRefreshToken = _encryptionService.Encrypt(tokenResponse.RefreshToken);
                user.GoogleTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresInSeconds ?? 3600);

                await _userRepository.UpdateAsync(user);
                _logger.LogInformation($"HandleOAuthCallback: Success for UserId={userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"HandleOAuthCallback failed for UserId={userId}");
                return false;
            }
        }

        public async Task<string> CreateSpreadsheetForUserGroup(int userId, int groupId, string groupName)
        {
            try
            {
                _logger.LogInformation($"CreateSpreadsheetForUserGroup: Starting UserId={userId}, GroupId={groupId}");

                var existingSheet = await _sheetsRepository.GetByUserAndGroupAsync(userId, groupId);
                if (existingSheet != null)
                {
                    _logger.LogInformation($"CreateSpreadsheetForUserGroup: Already exists, returning URL");
                    return existingSheet.SpreadsheetUrl;
                }

                var service = await GetSheetsService(userId);
                if (service == null)
                {
                    _logger.LogError($"CreateSpreadsheetForUserGroup: SheetsService is null");
                    return null;
                }

                var user = await _userRepository.GetByIdAsync(userId);
                var spreadsheetName = $"{user.Email} - {groupName} Formları - {DateTime.Now:yyyy-MM-dd}";

                var spreadsheet = new Spreadsheet
                {
                    Properties = new SpreadsheetProperties
                    {
                        Title = spreadsheetName
                    },
                    Sheets = new List<Sheet>
                    {
                        new Sheet
                        {
                            Properties = new SheetProperties
                            {
                                Title = "Genel Bilgi",
                                GridProperties = new GridProperties
                                {
                                    FrozenRowCount = 1
                                }
                            }
                        }
                    }
                };

                var createRequest = service.Spreadsheets.Create(spreadsheet);
                var createdSpreadsheet = await createRequest.ExecuteAsync();

                await WriteInfoToSheet(service, createdSpreadsheet.SpreadsheetId, groupName);

                var userGoogleSheet = new UserGoogleSheet
                {
                    UserId = userId,
                    GroupId = groupId,
                    SpreadsheetId = createdSpreadsheet.SpreadsheetId,
                    SpreadsheetUrl = createdSpreadsheet.SpreadsheetUrl,
                    SheetName = spreadsheetName,
                    CreatedDate = DateTime.UtcNow
                };

                await _sheetsRepository.CreateAsync(userGoogleSheet);

                _logger.LogInformation($"CreateSpreadsheetForUserGroup: Success SpreadsheetId={createdSpreadsheet.SpreadsheetId}");
                return createdSpreadsheet.SpreadsheetUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"CreateSpreadsheetForUserGroup failed for UserId={userId}, GroupId={groupId}");
                return null;
            }
        }

        public async Task<bool> CreateSheetTabForForm(int userId, int groupId, string formName, List<string> headers)
        {
            Console.WriteLine($"===== CreateSheetTabForForm BAŞLADI: UserId={userId}, GroupId={groupId}, FormName={formName}");
            
            try
            {
                _logger.LogInformation($"CreateSheetTabForForm: UserId={userId}, GroupId={groupId}, FormName={formName}, Headers={string.Join(",", headers)}");

                var userSheet = await _sheetsRepository.GetByUserAndGroupAsync(userId, groupId);
                if (userSheet == null)
                {
                    Console.WriteLine("===== HATA: UserSheet bulunamadı!");
                    _logger.LogError($"CreateSheetTabForForm: UserSheet not found");
                    return false;
                }

                Console.WriteLine($"===== UserSheet bulundu: SpreadsheetId={userSheet.SpreadsheetId}");

                var service = await GetSheetsService(userId);
                if (service == null)
                {
                    Console.WriteLine("===== HATA: SheetsService null!");
                    _logger.LogError($"CreateSheetTabForForm: SheetsService is null");
                    return false;
                }

                Console.WriteLine("===== SheetsService başarıyla alındı");

                var spreadsheet = await service.Spreadsheets.Get(userSheet.SpreadsheetId).ExecuteAsync();
                var existingSheet = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == formName);

                if (existingSheet != null)
                {
                    Console.WriteLine($"===== Tab zaten var, header güncelleniyor");
                    _logger.LogInformation($"CreateSheetTabForForm: Tab already exists, updating headers");
                    return await UpdateSheetHeaders(service, userSheet.SpreadsheetId, formName, headers);
                }

                Console.WriteLine("===== Yeni tab oluşturuluyor...");

                var addSheetRequest = new AddSheetRequest
                {
                    Properties = new SheetProperties
                    {
                        Title = formName,
                        GridProperties = new GridProperties
                        {
                            FrozenRowCount = 1,
                            RowCount = 1000,
                            ColumnCount = headers.Count + 1
                        }
                    }
                };

                var batchUpdateRequest = new BatchUpdateSpreadsheetRequest
                {
                    Requests = new List<Request>
                    {
                        new Request { AddSheet = addSheetRequest }
                    }
                };

                await service.Spreadsheets.BatchUpdate(batchUpdateRequest, userSheet.SpreadsheetId).ExecuteAsync();
                
                Console.WriteLine("===== Tab oluşturuldu, header'lar yazılıyor...");
                
                await WriteHeadersToSheet(service, userSheet.SpreadsheetId, formName, headers);

                Console.WriteLine("===== BAŞARILI: CreateSheetTabForForm tamamlandı");
                _logger.LogInformation($"CreateSheetTabForForm: Success");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"===== EXCEPTION: {ex.Message}");
                Console.WriteLine($"===== StackTrace: {ex.StackTrace}");
                _logger.LogError(ex, $"CreateSheetTabForForm failed for UserId={userId}, GroupId={groupId}, FormName={formName}");
                return false;
            }
        }

        public async Task<int> AppendFormDataToSheet(int userId, int groupId, string formName, Dictionary<string, string> formData)
        {
            try
            {
                _logger.LogInformation($"AppendFormDataToSheet: UserId={userId}, GroupId={groupId}, FormName={formName}");

                var userSheet = await _sheetsRepository.GetByUserAndGroupAsync(userId, groupId);
                if (userSheet == null)
                {
                    _logger.LogError($"AppendFormDataToSheet: UserSheet not found for UserId={userId}, GroupId={groupId}");
                    return -1;
                }

                var service = await GetSheetsService(userId);
                if (service == null)
                {
                    _logger.LogError($"AppendFormDataToSheet: SheetsService is null for UserId={userId}");
                    return -1;
                }

                var range = $"{formName}!A1:ZZ1";
                var headerRequest = service.Spreadsheets.Values.Get(userSheet.SpreadsheetId, range);
                var headerResponse = await headerRequest.ExecuteAsync();

                if (headerResponse.Values == null || headerResponse.Values.Count == 0)
                {
                    _logger.LogError($"AppendFormDataToSheet: No headers found in sheet '{formName}' for SpreadsheetId={userSheet.SpreadsheetId}");
                    return -1;
                }

                var headers = headerResponse.Values[0].Select(h => h.ToString()).ToList();
                _logger.LogInformation($"AppendFormDataToSheet: Found headers: {string.Join(",", headers)}");

                var rowData = new List<object>();

                foreach (var header in headers)
                {
                    rowData.Add(formData.ContainsKey(header) ? formData[header] : "");
                }

                rowData.Insert(0, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                var valueRange = new ValueRange
                {
                    Values = new List<IList<object>> { rowData }
                };

                var appendRange = $"{formName}!A:ZZ";
                var appendRequest = service.Spreadsheets.Values.Append(valueRange, userSheet.SpreadsheetId, appendRange);
                appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.RAW;

                var appendResponse = await appendRequest.ExecuteAsync();
                var updatedRange = appendResponse.Updates.UpdatedRange;
                var rowNumber = int.Parse(updatedRange.Split('!')[1].Split(':')[0].Substring(1));

                userSheet.LastUpdatedDate = DateTime.UtcNow;
                await _sheetsRepository.UpdateAsync(userSheet);

                _logger.LogInformation($"AppendFormDataToSheet: Success, RowNumber={rowNumber}");
                return rowNumber;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"AppendFormDataToSheet failed for UserId={userId}, GroupId={groupId}, FormName={formName}");
                return -1;
            }
        }

        public async Task<bool> RefreshAccessToken(int userId)
        {
            try
            {
                _logger.LogInformation($"RefreshAccessToken: Starting for UserId={userId}");

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || string.IsNullOrEmpty(user.GoogleRefreshToken))
                {
                    _logger.LogError($"RefreshAccessToken: User or RefreshToken not found");
                    return false;
                }

                var refreshToken = _encryptionService.Decrypt(user.GoogleRefreshToken);

                var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = new ClientSecrets
                    {
                        ClientId = _clientId,
                        ClientSecret = _clientSecret
                    },
                    Scopes = Scopes
                });

                var tokenResponse = await flow.RefreshTokenAsync(
                    userId.ToString(),
                    refreshToken,
                    System.Threading.CancellationToken.None
                );

                user.GoogleAccessToken = _encryptionService.Encrypt(tokenResponse.AccessToken);
                user.GoogleTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresInSeconds ?? 3600);

                await _userRepository.UpdateAsync(user);

                _logger.LogInformation($"RefreshAccessToken: Success for UserId={userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"RefreshAccessToken failed for UserId={userId}");
                return false;
            }
        }

        public async Task<UserGoogleSheet> GetUserGoogleSheetAsync(int userId, int groupId)
        {
            return await _sheetsRepository.GetByUserAndGroupAsync(userId, groupId);
        }

        private async Task<SheetsService> GetSheetsService(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.GoogleAccessToken))
            {
                _logger.LogError($"GetSheetsService: User or AccessToken not found for UserId={userId}");
                return null;
            }

            if (user.GoogleTokenExpiry <= DateTime.UtcNow)
            {
                _logger.LogInformation($"GetSheetsService: Token expired, refreshing for UserId={userId}");
                var refreshed = await RefreshAccessToken(userId);
                if (!refreshed)
                {
                    _logger.LogError($"GetSheetsService: Token refresh failed for UserId={userId}");
                    return null;
                }
                user = await _userRepository.GetByIdAsync(userId);
            }

            var accessToken = _encryptionService.Decrypt(user.GoogleAccessToken);
            var credential = GoogleCredential.FromAccessToken(accessToken);

            return new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "FormSepeti"
            });
        }

        private async Task WriteInfoToSheet(SheetsService service, string spreadsheetId, string groupName)
        {
            var values = new List<IList<object>>
            {
                new List<object> { "Bilgi", "Değer" },
                new List<object> { "Grup Adı", groupName },
                new List<object> { "Oluşturulma Tarihi", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                new List<object> { "Açıklama", "Bu dosya otomatik olarak oluşturulmuştur." }
            };

            var valueRange = new ValueRange { Values = values };
            var updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, "Genel Bilgi!A1:B4");
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;

            await updateRequest.ExecuteAsync();
        }

        private async Task<bool> WriteHeadersToSheet(SheetsService service, string spreadsheetId, string sheetName, List<string> headers)
        {
            var allHeaders = new List<object> { "Gönderim Tarihi" };
            allHeaders.AddRange(headers.Cast<object>());

            _logger.LogInformation($"WriteHeadersToSheet: Writing headers to '{sheetName}': {string.Join(",", allHeaders)}");

            var values = new List<IList<object>> { allHeaders };
            var valueRange = new ValueRange { Values = values };
            var range = $"{sheetName}!A1:ZZ1";
            var updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, range);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;

            await updateRequest.ExecuteAsync();
            await FormatHeaderRow(service, spreadsheetId, sheetName);

            return true;
        }

        private async Task<bool> UpdateSheetHeaders(SheetsService service, string spreadsheetId, string sheetName, List<string> headers)
        {
            return await WriteHeadersToSheet(service, spreadsheetId, sheetName, headers);
        }

        private async Task FormatHeaderRow(SheetsService service, string spreadsheetId, string sheetName)
        {
            var spreadsheet = await service.Spreadsheets.Get(spreadsheetId).ExecuteAsync();
            var sheet = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == sheetName);
            if (sheet == null) return;

            var requests = new List<Request>
            {
                new Request
                {
                    RepeatCell = new RepeatCellRequest
                    {
                        Range = new GridRange
                        {
                            SheetId = sheet.Properties.SheetId,
                            StartRowIndex = 0,
                            EndRowIndex = 1
                        },
                        Cell = new CellData
                        {
                            UserEnteredFormat = new CellFormat
                            {
                                BackgroundColor = new Color { Red = 0.2f, Green = 0.6f, Blue = 0.9f },
                                TextFormat = new TextFormat
                                {
                                    Bold = true,
                                    ForegroundColor = new Color { Red = 1, Green = 1, Blue = 1 }
                                }
                            }
                        },
                        Fields = "userEnteredFormat(backgroundColor,textFormat)"
                    }
                }
            };

            var batchUpdateRequest = new BatchUpdateSpreadsheetRequest { Requests = requests };
            await service.Spreadsheets.BatchUpdate(batchUpdateRequest, spreadsheetId).ExecuteAsync();
        }
    }
}