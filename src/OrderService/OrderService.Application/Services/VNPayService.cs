using OrderService.Application.Interface;
using OrderService.Domain.Entities;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Net; // Dùng cho WebUtility.UrlEncode
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;
using OrderService.Application.Models; // Đảm bảo đã có PaymentResult

namespace OrderService.Application.Services.Payment
{
    // Lớp so sánh tùy chỉnh cho SortedList (Giữ nguyên)
    public class VnPayCompare : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (x == y) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            // So sánh theo thứ tự từ điển, không phân biệt chữ hoa/thường
            return string.Compare(x, y, StringComparison.Ordinal);
        }
    }

    public class VNPayService : IPaymentService
    {
        private readonly VNPayConfig _config;

        public VNPayService(IOptions<VNPayConfig> configOptions)
        {
            _config = configOptions.Value;
        }

        // =======================================================
        // Phương thức 1: KHỞI TẠO THANH TOÁN (Lấy URL/QR)
        // =======================================================
        public Task<PaymentResult> InitiatePaymentAsync(PaymentTransaction transaction, string customerIpAddress)
        {
            var vnp_Params = new SortedList<string, string>(new VnPayCompare());
            var timeNow = DateTime.Now;

            // 1. Chuẩn bị dữ liệu yêu cầu
            vnp_Params.Add("vnp_Version", _config.Version);
            vnp_Params.Add("vnp_Command", "pay");
            vnp_Params.Add("vnp_TmnCode", _config.TmnCode);
            // vnp_Amount phải nhân 100
            vnp_Params.Add("vnp_Amount", ((long)transaction.TotalAmount * 100).ToString());
            vnp_Params.Add("vnp_CreateDate", timeNow.ToString("yyyyMMddHHmmss"));
            vnp_Params.Add("vnp_CurrCode", "VND");
            vnp_Params.Add("vnp_IpAddr", customerIpAddress);
            vnp_Params.Add("vnp_Locale", "vn");
            vnp_Params.Add("vnp_OrderInfo", $"Thanh toan GD: {transaction.Id}");
            vnp_Params.Add("vnp_OrderType", "150000");

            // QUAN TRỌNG: 
            vnp_Params.Add("vnp_ReturnUrl", _config.ReturnUrl); // Trình duyệt Khách hàng (GET)
            // vnp_Params.Add("vnp_IpnUrl", _config.IpnUrl);       // Server-to-Server (GET/POST)

            vnp_Params.Add("vnp_TxnRef", transaction.Id.ToString("N")); // ID giao dịch nội bộ (PaymentTransaction.Id)
            vnp_Params.Add("vnp_ExpireDate", timeNow.AddMinutes(15).ToString("yyyyMMddHHmmss"));

            // 2. Tạo URL và Mã hóa (Hashing)
            var data = new StringBuilder();
            foreach (var key in vnp_Params.Keys)
            {
                if (!string.IsNullOrEmpty(vnp_Params[key]))
                {
                    // SỬA LỖI: Dùng WebUtility.UrlEncode để encode các giá trị tham số
                    data.Append(WebUtility.UrlEncode(key) + "=" + WebUtility.UrlEncode(vnp_Params[key]) + "&");
                }
            }

            // 2. Build query string (encode trước khi hash)
            var query = new StringBuilder();
            var hashData = new StringBuilder();

            foreach (var kv in vnp_Params)
            {
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    query.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
                    hashData.Append(kv.Key + "=" + kv.Value + "&");
                }
            }

            // Bỏ dấu & cuối nếu có
            if (query.Length > 0) query.Length -= 1;
            if (hashData.Length > 0) hashData.Length -= 1;

            // Hash từ dữ liệu CHƯA encode
            var vnp_SecureHash = HmacSHA512(_config.HashSecret, hashData.ToString());

            var paymentUrl = $"{_config.BaseUrl}?{query}&vnp_SecureHash={vnp_SecureHash}";

            // 3. Trả về kết quả
            return Task.FromResult(new PaymentResult
            {
                Success = true,
                PaymentUrl = paymentUrl,
                TransactionId = transaction.Id.ToString(),
                Message = "VNPay payment URL created."
            });
        }

        // =======================================================
        // Phương thức 2: XỬ LÝ CALLBACK/IPN
        // =======================================================
        public Task<PaymentResult> HandleCallbackAsync(string transactionId, IDictionary<string, string> payload)
        {
            // 1. Kiểm tra chữ ký (Secure Hash)
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

        // =======================================================
        //  Phương thức 3: VẤN TIN TRẠNG THÁI (Giữ nguyên Mock)
        // =======================================================
        public async Task<bool> CheckTransactionStatusAsync(string transactionId)
        {
            // TODO: Triển khai gọi API vấn tin VNPay
            return await Task.FromResult(!string.IsNullOrEmpty(transactionId));
        }

        // --- Hỗ trợ VNPay Hash ---
        private string HmacSHA512(string key, string data)
        {
            var hash = new StringBuilder();
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);

            using (var hmac = new HMACSHA512(keyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(dataBytes);
                foreach (var b in hashBytes)
                {
                    hash.Append(b.ToString("x2"));
                }
            }
            return hash.ToString();
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
                vnp_Params.Add(key, value);
            }

            var rawData = string.Join("&", vnp_Params.Select(kv => kv.Key + "=" + kv.Value));
            string calculatedHash = HmacSHA512(_config.HashSecret, rawData);

            return calculatedHash.Equals(receivedHash, StringComparison.OrdinalIgnoreCase);
        }
    }
}