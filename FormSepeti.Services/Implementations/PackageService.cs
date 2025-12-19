using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using FormSepeti.Services.Interfaces;
using FormSepeti.Services.Models;

namespace FormSepeti.Services.Implementations
{
    public class PackageService : IPackageService
    {
        private readonly IPackageRepository _packageRepository;
        private readonly IUserPackageRepository _userPackageRepository;
        private readonly IFormGroupRepository _formGroupRepository;
        private readonly IFormGroupMappingRepository _formGroupMappingRepository;
        private readonly IEmailService _emailService;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<PackageService> _logger;

        public PackageService(
            IPackageRepository packageRepository,
            IUserPackageRepository userPackageRepository,
            IFormGroupRepository formGroupRepository,
            IFormGroupMappingRepository formGroupMappingRepository,
            IEmailService emailService,
            IUserRepository userRepository,
            ILogger<PackageService> logger)
        {
            _packageRepository = packageRepository;
            _userPackageRepository = userPackageRepository;
            _formGroupRepository = formGroupRepository;
            _formGroupMappingRepository = formGroupMappingRepository;
            _emailService = emailService;
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task<List<FormGroup>> GetAvailableGroupsForUserAsync(int userId)
        {
            var userGroups = await _userPackageRepository.GetActiveGroupsByUserIdAsync(userId);
            var freeGroups = await _formGroupRepository.GetGroupsWithFreeFormsAsync();

            var allGroups = userGroups.Concat(freeGroups)
                .GroupBy(g => g.GroupId)
                .Select(g => g.First())
                .OrderBy(g => g.SortOrder)
                .ToList();

            return allGroups;
        }

        public async Task<List<Package>> GetPackagesByGroupIdAsync(int groupId)
        {
            return await _packageRepository.GetByGroupIdAsync(groupId);
        }

        public async Task<Package> GetPackageByIdAsync(int packageId)
        {
            return await _packageRepository.GetByIdAsync(packageId);
        }

        public async Task<List<UserPackage>> GetUserActivePackagesAsync(int userId)
        {
            return await _userPackageRepository.GetActiveByUserIdAsync(userId);
        }

        public async Task<bool> HasActivePackageForGroupAsync(int userId, int groupId)
        {
            return await _userPackageRepository.HasActivePackageAsync(userId, groupId);
        }

        public async Task<UserPackage> PurchasePackageAsync(int userId, int packageId, string transactionId, decimal amount)
        {
            try
            {
                var package = await _packageRepository.GetByIdAsync(packageId);
                if (package == null)
                {
                    throw new Exception("Paket bulunamadı.");
                }

                DateTime? expiryDate = null;
                if (package.DurationDays.HasValue)
                {
                    expiryDate = DateTime.UtcNow.AddDays(package.DurationDays.Value);
                }

                var userPackage = new UserPackage
                {
                    UserId = userId,
                    PackageId = packageId,
                    GroupId = package.GroupId,
                    PurchaseDate = DateTime.UtcNow,
                    ActivationDate = DateTime.UtcNow,
                    ExpiryDate = expiryDate,
                    IsActive = true,
                    PaymentTransactionId = transactionId,
                    PaymentAmount = amount
                };

                var createdPackage = await _userPackageRepository.CreateAsync(userPackage);

                var user = await _userRepository.GetByIdAsync(userId);
                if (user != null && !string.IsNullOrEmpty(user.Email))
                {
                    await _emailService.SendPackagePurchaseConfirmationAsync(
                        user.Email,
                        user.Email.Split('@')[0],
                        package.PackageName,
                        amount
                    );
                }

                _logger.LogInformation($"Package purchased: User {userId}, Package {packageId}, Transaction {transactionId}");

                return createdPackage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error purchasing package: User {userId}, Package {packageId}");
                throw;
            }
        }

        public async Task<bool> ActivatePackageAsync(int userPackageId)
        {
            var userPackage = await _userPackageRepository.GetByIdAsync(userPackageId);
            if (userPackage == null) return false;

            userPackage.IsActive = true;
            userPackage.ActivationDate = DateTime.UtcNow;

            return await _userPackageRepository.UpdateAsync(userPackage);
        }

        public async Task<bool> DeactivatePackageAsync(int userPackageId)
        {
            var userPackage = await _userPackageRepository.GetByIdAsync(userPackageId);
            if (userPackage == null) return false;

            userPackage.IsActive = false;

            return await _userPackageRepository.UpdateAsync(userPackage);
        }

        public async Task<PackageAccessInfo> GetUserAccessToFormAsync(int userId, int formId, int groupId)
        {
            var accessInfo = new PackageAccessInfo
            {
                HasAccess = false,
                RequiresPackage = false,
                IsFree = false
            };

            var mapping = await _formGroupMappingRepository.GetByFormAndGroupAsync(formId, groupId);
            if (mapping == null)
            {
                return accessInfo;
            }

            if (mapping.IsFreeInGroup && !mapping.RequiresPackage)
            {
                accessInfo.HasAccess = true;
                accessInfo.IsFree = true;
                return accessInfo;
            }

            accessInfo.RequiresPackage = true;

            var hasPackage = await HasActivePackageForGroupAsync(userId, groupId);
            if (hasPackage)
            {
                accessInfo.HasAccess = true;
            }

            return accessInfo;
        }
    }
}