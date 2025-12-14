using System.Threading.Tasks;
using FormSepeti.Services.Models;

namespace FormSepeti.Services.Interfaces
{
    public interface IIyzicoPaymentService
    {
        Task<IyzicoPaymentResult> ProcessPaymentAsync(IyzicoPaymentRequest request);
        Task<bool> VerifyPaymentAsync(string transactionId);
    }
}