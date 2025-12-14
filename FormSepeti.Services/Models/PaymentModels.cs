namespace FormSepeti.Services.Models
{
    public class IyzicoPaymentRequest
    {
        public int PackageId { get; set; }
        public int UserId { get; set; }
        public decimal Amount { get; set; }
        public string CardHolderName { get; set; }
        public string CardNumber { get; set; }
        public string ExpireMonth { get; set; }
        public string ExpireYear { get; set; }
        public string Cvc { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
    }

    public class IyzicoPaymentResult
    {
        public bool Success { get; set; }
        public string TransactionId { get; set; }
        public string ConversationId { get; set; }
        public string ErrorMessage { get; set; }
    }
}