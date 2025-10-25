using OrderService.Application.Interface;
using OrderService.Application.Models;
using OrderService.Domain.Entities;

namespace OrderService.Application.Services.Payment
{
    public class MockPaymentService : IPaymentService
    {
        // Mock provider: generate a fake URL representing QR or payment page
        public Task<PaymentResult> InitiatePaymentAsync(PaymentTransaction transaction, string clientIpAddress)
        {
            // Giả lập logic khởi tạo thanh toán thành công
            var mockResult = new PaymentResult
            {
                Success = true,
                // Sử dụng TotalAmount từ PaymentTransaction để tạo ID/URL giả
                TransactionId = $"MOCK_TX_{transaction.TotalAmount}_{DateTime.UtcNow.Ticks}",
                PaymentUrl = "https://mock-payment-gateway.com/pay/" + transaction.Id.ToString(),
                Message = "Mock payment initiated successfully."
            };

            return Task.FromResult(mockResult);
        }

        public Task<PaymentResult> HandleCallbackAsync(string transactionId, IDictionary<string, string> payload)
        {
            // In real: verify signature, check provider status -> here accept if payload contains status=success
            payload.TryGetValue("status", out var status);
            var success = string.Equals(status, "success", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase);

            return Task.FromResult(new PaymentResult
            {
                Success = success,
                TransactionId = transactionId,
                PaymentUrl = null,
                Message = success ? "Payment succeeded (mock)" : "Payment failed (mock)"
            });
        }

        public Task<bool> CheckTransactionStatusAsync(string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId)) return Task.FromResult(false);

            // Giả lập trạng thái đã thanh toán nếu TransactionId kết thúc bằng một chữ số chẵn
            var isPaid = (transactionId.GetHashCode() % 10) < 9;

            return Task.FromResult(isPaid);
        }
    }
}
