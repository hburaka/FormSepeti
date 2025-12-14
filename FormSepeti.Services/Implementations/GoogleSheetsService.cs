using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Configuration;
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

        private static readonly string[] Scopes = {
            SheetsService.Scope.Spreadsheets,
            DriveService.Scope.DriveFile
        };

        public GoogleSheetsService(
            IConfiguration configuration,
            IUserRepository userRepository,
            IUserGoogleSheetsRepository sheetsRepository,
            IEncryptionService encryptionService)
        {
            _clientId = configuration["Google:ClientId"];
            _clientSecret = configuration["Google:ClientSecret"];
            _redirectUri = configuration["Google:RedirectUri"];
            _userRepository = userRepository;
            _sheetsRepository = sheetsRepository;
            _encryptionService = encryptionService;
        }

        public Task<string> GetAuthorizationUrl(int userId)  // async'i kaldır
        {
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = _clientId,
                    ClientSecret = _clientSecret
                },
                Scopes = Scopes
            });

            var authRequest = flow.CreateAuthorizationCodeRequest(_redirectUri);

            var authUrl = authRequest.Build();

            return Task.FromResult(authUrl.ToString());
        }

        public async Task<bool> HandleOAuthCallback(int userId, string code)
        {
            try
            {
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
                if (user == null) return false;

                user.GoogleAccessToken = _encryptionService.Encrypt(tokenResponse.AccessToken);
                user.GoogleRefreshToken = _encryptionService.Encrypt(tokenResponse.RefreshToken);
                user.GoogleTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresInSeconds ?? 3600);

                await _userRepository.UpdateAsync(user);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<string> CreateSpreadsheetForUserGroup(int userId, int groupId, string groupName)
        {
            try
            {
                var existingSheet = await _sheetsRepository.GetByUserAndGroupAsync(userId, groupId);
                if (existingSheet != null)
                {
                    return existingSheet.SpreadsheetUrl;
                }

                var service = await GetSheetsService(userId);
                if (service == null) return null;

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

                return createdSpreadsheet.SpreadsheetUrl;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<bool> CreateSheetTabForForm(int userId, int groupId, string formName, List<string> headers)
        {
            try
            {
                var userSheet = await _sheetsRepository.GetByUserAndGroupAsync(userId, groupId);
                if (userSheet == null) return false;

                var service = await GetSheetsService(userId);
                if (service == null) return false;

                var spreadsheet = await service.Spreadsheets.Get(userSheet.SpreadsheetId).ExecuteAsync();
                var existingSheet = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == formName);

                if (existingSheet != null)
                {
                    return await UpdateSheetHeaders(service, userSheet.SpreadsheetId, formName, headers);
                }

                var addSheetRequest = new AddSheetRequest
                {
                    Properties = new SheetProperties
                    {
                        Title = formName,
                        GridProperties = new GridProperties
                        {
                            FrozenRowCount = 1,
                            RowCount = 1000,
                            ColumnCount = headers.Count
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
                await WriteHeadersToSheet(service, userSheet.SpreadsheetId, formName, headers);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<int> AppendFormDataToSheet(int userId, int groupId, string formName, Dictionary<string, string> formData)
        {
            try
            {
                var userSheet = await _sheetsRepository.GetByUserAndGroupAsync(userId, groupId);
                if (userSheet == null) return -1;

                var service = await GetSheetsService(userId);
                if (service == null) return -1;

                var range = $"{formName}!A1:ZZ1";
                var headerRequest = service.Spreadsheets.Values.Get(userSheet.SpreadsheetId, range);
                var headerResponse = await headerRequest.ExecuteAsync();

                if (headerResponse.Values == null || headerResponse.Values.Count == 0)
                {
                    return -1;
                }

                var headers = headerResponse.Values[0].Select(h => h.ToString()).ToList();
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

                return rowNumber;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public async Task<bool> RefreshAccessToken(int userId)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || string.IsNullOrEmpty(user.GoogleRefreshToken)) return false;

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
                return true;
            }
            catch (Exception)
            {
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
            if (user == null || string.IsNullOrEmpty(user.GoogleAccessToken)) return null;

            if (user.GoogleTokenExpiry <= DateTime.UtcNow)
            {
                var refreshed = await RefreshAccessToken(userId);
                if (!refreshed) return null;
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