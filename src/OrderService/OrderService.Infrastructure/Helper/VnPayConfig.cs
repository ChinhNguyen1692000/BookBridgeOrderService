namespace OrderService.Application.Services.Payment
{
    public class VNPayConfig
    {
        // Lấy từ tài liệu (hoặc đăng ký theo link VNPAY cung cấp)
        public string TmnCode { get; set; } = "2AN3Q8IC"; 
        public string HashSecret { get; set; } = "OY64662GQST9OKZJE484SL7PFWO0T1FS";
        public string BaseUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        
        // URL Front-end sau khi thanh toán xong
        public string ReturnUrl { get; set; } = "https://e00d56ad.bookbridge-5ju.pages.dev"; 
        
        // URL API của Controller (Nơi nhận IPN)
        public string IpnUrl { get; set; } = "https://www.bookbridge.io.vn/api/orders/vnpay-ipn"; 
        
        public string Version { get; set; } = "2.1.0";
    }
}