using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Iyzipay;
using Iyzipay.Model;
using Iyzipay.Request;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FormSepeti.Services.Interfaces;
using FormSepeti.Services.Models;

namespace FormSepeti.Services.Implementations
{
    public class IyzicoPaymentService : IIyzicoPaymentService
    {
        private readonly Options _options;
        private readonly ILogger<IyzicoPaymentService> _logger;

        public IyzicoPaymentService(IConfiguration configuration, ILogger<IyzicoPaymentService> logger)
        {
            _options = new Options
            {
                ApiKey = configuration["Iyzico:ApiKey"],
                SecretKey = configuration["Iyzico:SecretKey"],
                BaseUrl = configuration["Iyzico:BaseUrl"]
            };
            _logger = logger;
        }

        public async Task<IyzicoPaymentResult> ProcessPaymentAsync(IyzicoPaymentRequest request)
        {
            var result = new IyzicoPaymentResult();

            try
            {
                var paymentRequest = new CreatePaymentRequest
                {
                    Locale = Locale.TR.ToString(),
                    ConversationId = Guid.NewGuid().ToString(),
                    Price = request.Amount.ToString("F2").Replace(",", "."),
                    PaidPrice = request.Amount.ToString("F2").Replace(",", "."),
                    Currency = Currency.TRY.ToString(),
                    Installment = 1,
                    BasketId = $"P{request.PackageId}",
                    PaymentChannel = PaymentChannel.WEB.ToString(),
                    PaymentGroup = PaymentGroup.PRODUCT.ToString()
                };

                var paymentCard = new PaymentCard
                {
                    CardHolderName = request.CardHolderName,
                    CardNumber = request.CardNumber.Replace(" ", ""),
                    ExpireMonth = request.ExpireMonth,
                    ExpireYear = request.ExpireYear,
                    Cvc = request.Cvc,
                    RegisterCard = 0
                };
                paymentRequest.PaymentCard = paymentCard;

                var buyer = new Buyer
                {
                    Id = request.UserId.ToString(),
                    Name = request.CardHolderName.Split(' ')[0],
                    Surname = request.CardHolderName.Split(' ').Length > 1
                        ? request.CardHolderName.Split(' ')[1]
                        : "User",
                    Email = request.Email,
                    GsmNumber = request.PhoneNumber,
                    IdentityNumber = "11111111111",
                    RegistrationAddress = "Adres",
                    City = "Istanbul",
                    Country = "Turkey",
                    Ip = "127.0.0.1"
                };
                paymentRequest.Buyer = buyer;

                var shippingAddress = new Address
                {
                    ContactName = request.CardHolderName,
                    City = "Istanbul",
                    Country = "Turkey",
                    Description = "Adres"
                };
                paymentRequest.ShippingAddress = shippingAddress;
                paymentRequest.BillingAddress = shippingAddress;

                var basketItems = new List<BasketItem>
                {
                    new BasketItem
                    {
                        Id = request.PackageId.ToString(),
                        Name = "Paket Üyeliği",
                        Category1 = "Subscription",
                        ItemType = BasketItemType.VIRTUAL.ToString(),
                        Price = request.Amount.ToString("F2").Replace(",", ".")
                    }
                };
                paymentRequest.BasketItems = basketItems;

                var payment = await Task.Run(() => Payment.Create(paymentRequest, _options));

                if (payment.Status == "success")
                {
                    result.Success = true;
                    result.TransactionId = payment.PaymentId;
                    result.ConversationId = payment.ConversationId;
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = payment.ErrorMessage ?? "Ödeme başarısız oldu.";
                    _logger.LogWarning($"Payment failed: {payment.ErrorMessage}");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment processing error");
                result.Success = false;
                result.ErrorMessage = "Ödeme işlemi sırasında bir hata oluştu.";
                return result;
            }
        }

        public async Task<bool> VerifyPaymentAsync(string transactionId)
        {
            return await Task.FromResult(true);
        }
    }
}