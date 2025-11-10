using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using OrderService.Application.Interface;
using OrderService.Application.Models;
using OrderService.Domain.Entities;

namespace OrderService.Application.Services.Payment
{
    public class MockPaymentService : IPaymentService
    {

        private readonly VNPayConfig _config;
        public MockPaymentService(IOptions<VNPayConfig> configOptions)
        {
            _config = configOptions.Value;
        }
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
            // 1. Kiểm tra chữ ký (Secure Hash) - Phải validate trước mọi thứ
            if (!ValidateHash(payload))
            {
                return Task.FromResult(new PaymentResult { Success = false, Message = "Invalid hash signature." });
            }

            // 2. Kiểm tra mã phản hồi của VNPay
            var vnp_ResponseCode = payload.ContainsKey("vnp_ResponseCode") ? payload["vnp_ResponseCode"] : "";
            var vnp_TransactionStatus = payload.ContainsKey("vnp_TransactionStatus") ? payload["vnp_TransactionStatus"] : "";
            // 00: Thành công
            var success = vnp_ResponseCode == "00" && vnp_TransactionStatus == "00";

            return Task.FromResult(new PaymentResult
            {
                Success = success,
                // Lấy ID giao dịch của VNPAY (vnp_TransactionNo) để lưu vào DB (nếu cần)
                TransactionId = payload.TryGetValue("vnp_TransactionNo", out var vnpTxnNo) ? vnpTxnNo : transactionId,
                Message = success ? "Payment callback succeeded." : $"Payment failed. Code: {vnp_ResponseCode}"
            });
        }

        private bool ValidateHash(IDictionary<string, string> payload)
        {
            var vnp_Params = new SortedList<string, string>(new VnPayCompare());
            string receivedHash = string.Empty;

            foreach (var (key, value) in payload)
            {
                if (key == "vnp_SecureHash")
                {
                    receivedHash = value;
                    continue;
                }
                if (key.StartsWith("vnp_")) // Chỉ lấy param vnp_ (bỏ thừa nếu có)
                {
                    vnp_Params.Add(key, value);
                }
            }

            var rawData = string.Join("&", vnp_Params.Select(kv => kv.Key + "=" + kv.Value));
            string calculatedHash = HmacSHA512(_config.HashSecret, rawData);
            return calculatedHash.Equals(receivedHash, StringComparison.OrdinalIgnoreCase);
        }

        private static string HmacSHA512(string key, string data)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (data == null) data = string.Empty;

            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
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
