using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Threading.Tasks;
using FormSepeti.Data.Entities;
using FormSepeti.Data.Repositories.Interfaces;
using FormSepeti.Services.Interfaces;
using FormSepeti.Services.Models;

namespace FormSepeti.Web.Controllers
{
    [Authorize]
    public class PackageController : Controller
    {
        private readonly IPackageService _packageService;
        private readonly IFormGroupRepository _formGroupRepository;
        private readonly IIyzicoPaymentService _paymentService;
        private readonly IUserService _userService;

        public PackageController(
            IPackageService packageService,
            IFormGroupRepository formGroupRepository,
            IIyzicoPaymentService paymentService,
            IUserService userService)
        {
            _packageService = packageService;
            _formGroupRepository = formGroupRepository;
            _paymentService = paymentService;
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var groups = await _formGroupRepository.GetAllActiveAsync();
            var userId = GetCurrentUserId();
            var activePackages = await _packageService.GetUserActivePackagesAsync(userId);

            var model = new PackageListViewModel
            {
                Groups = groups,
                ActivePackages = activePackages
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GroupPackages(int groupId)
        {
            var group = await _formGroupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                return NotFound();
            }

            var packages = await _packageService.GetPackagesByGroupIdAsync(groupId);
            var userId = GetCurrentUserId();
            var hasActivePackage = await _packageService.HasActivePackageForGroupAsync(userId, groupId);

            var model = new GroupPackagesViewModel
            {
                Group = group,
                Packages = packages,
                HasActivePackage = hasActivePackage
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Purchase(int packageId)
        {
            var userId = GetCurrentUserId();
            var user = await _userService.GetUserByIdAsync(userId);
            var package = await _packageService.GetPackageByIdAsync(packageId);

            if (package == null)
            {
                return NotFound();
            }

            var model = new PurchaseViewModel
            {
                Package = package,
                User = user
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(PurchaseFormModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Purchase", model);
            }

            var userId = GetCurrentUserId();
            var user = await _userService.GetUserByIdAsync(userId);
            var package = await _packageService.GetPackageByIdAsync(model.PackageId);

            if (package == null)
            {
                return NotFound();
            }

            if (package.Price == 0)
            {
                var userPackage = await _packageService.PurchasePackageAsync(
                    userId,
                    package.PackageId,
                    "FREE-" + Guid.NewGuid().ToString(),
                    0
                );

                TempData["Success"] = "Ücretsiz paket başarıyla aktive edildi!";
                return RedirectToAction("Index", "Dashboard");
            }

            var paymentRequest = new IyzicoPaymentRequest
            {
                PackageId = package.PackageId,
                UserId = userId,
                Amount = package.Price,
                CardHolderName = model.CardHolderName,
                CardNumber = model.CardNumber,
                ExpireMonth = model.ExpireMonth,
                ExpireYear = model.ExpireYear,
                Cvc = model.Cvc,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber ?? "5555555555"
            };

            var paymentResult = await _paymentService.ProcessPaymentAsync(paymentRequest);

            if (paymentResult.Success)
            {
                await _packageService.PurchasePackageAsync(
                    userId,
                    package.PackageId,
                    paymentResult.TransactionId,
                    package.Price
                );

                TempData["Success"] = "Ödeme başarılı! Paketiniz aktive edildi.";
                return RedirectToAction("Success", new { transactionId = paymentResult.TransactionId });
            }
            else
            {
                TempData["Error"] = $"Ödeme başarısız: {paymentResult.ErrorMessage}";
                return RedirectToAction("Purchase", new { packageId = package.PackageId });
            }
        }

        [HttpGet]
        public IActionResult Success(string transactionId)
        {
            ViewBag.TransactionId = transactionId;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> MyPackages()
        {
            var userId = GetCurrentUserId();
            var packages = await _packageService.GetUserActivePackagesAsync(userId);
            return View(packages);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            return int.Parse(userIdClaim ?? "0");
        }
    }

    public class PackageListViewModel
    {
        public List<FormGroup> Groups { get; set; }
        public List<UserPackage> ActivePackages { get; set; }
    }

    public class GroupPackagesViewModel
    {
        public FormGroup Group { get; set; }
        public List<Package> Packages { get; set; }
        public bool HasActivePackage { get; set; }
    }

    public class PurchaseViewModel
    {
        public Package Package { get; set; }
        public User User { get; set; }
    }

    public class PurchaseFormModel
    {
        public int PackageId { get; set; }

        [Required(ErrorMessage = "Kart üzerindeki isim gereklidir.")]
        public string CardHolderName { get; set; }

        [Required(ErrorMessage = "Kart numarası gereklidir.")]
        [CreditCard(ErrorMessage = "Geçerli bir kart numarası giriniz.")]
        public string CardNumber { get; set; }

        [Required(ErrorMessage = "Son kullanma ayı gereklidir.")]
        [Range(1, 12, ErrorMessage = "Geçerli bir ay giriniz (1-12).")]
        public string ExpireMonth { get; set; }

        [Required(ErrorMessage = "Son kullanma yılı gereklidir.")]
        public string ExpireYear { get; set; }

        [Required(ErrorMessage = "CVC kodu gereklidir.")]
        [StringLength(4, MinimumLength = 3, ErrorMessage = "CVC 3-4 haneli olmalıdır.")]
        public string Cvc { get; set; }

        public bool AcceptTerms { get; set; }
    }
}