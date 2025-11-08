using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Application.Interface;
using OrderService.Application.Models;
using OrderService.Domain.Entities;
using StackExchange.Redis;
using System.Security.Claims;

namespace OrderService.Api.Controllers
{
    [ApiController]
    [Route("api/orders")]
    public class OrderController : BaseApiController
    {
        private readonly IOrderServices _service;
        private readonly IPaymentService _paymentService;

        public OrderController(IOrderServices service, IPaymentService paymentService)
        {
            _service = service;
            _paymentService = paymentService;
        }

        // ==========================
        // Helper methods
        // ==========================
        private Guid GetCustomerId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("nameid");
            return Guid.TryParse(id, out var guid) ? guid : Guid.Empty;
        }

        private string GetCustomerIpAddress()
        {
            // Lấy IP từ Header hoặc Connection, tùy thuộc vào môi trường Deployment (Proxy/Load Balancer)
            if (Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                return Request.Headers["X-Forwarded-For"].ToString().Split(',').FirstOrDefault()?.Trim() ?? "127.0.0.1";
            }
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        }

        private string GetCustomerEmail() =>
            User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue("email")
            ?? string.Empty;

        // ==========================
        // QUERIES/GET
        // ==========================

        // Lấy tất cả Order
        [HttpGet("list")]
        public async Task<IActionResult> GetAll(int page = 1, int pageSize = 10)
        {
            var result = await _service.GetAll(page, pageSize);
            return Ok(result);
        }

        // Lấy Order theo id
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var order = await _service.GetById(id);
            return order is null ? NotFound() : Ok(order);
        }


        // Lấy Order theo Customer
        [HttpGet("by-customer/{customerId:guid}")]
        public async Task<IActionResult> GetByCustomer(Guid customerId, int page = 1, int pageSize = 10)
        {
            var result = await _service.GetOrderByCustomer(customerId, page, pageSize);
            return Ok(result);
        }


        // Lấy Order theo customer và status
        [HttpGet("by-status")]
        public async Task<IActionResult> GetByCustomerAndStatus([FromQuery] OrderFilterByCustomerAndStatusRequest request, int page = 1, int pageSize = 10)
        {
            if (!Enum.IsDefined(typeof(OrderStatus), request.OrderStatus))
            {
                return BadRequest("Trạng thái đơn hàng không hợp lệ.");
            }

            var result = await _service.GetOrderByCustomerAndStatus(request, page, pageSize);
            return Ok(result);
        }

        // Lấy Order theo store
        [HttpGet("by-bookstore/{bookstoreId:int}")]
        [Authorize(Roles = "Admin,Seller")] // Admin/Seller mới có quyền truy vấn theo BookstoreId
        public async Task<IActionResult> GetByBookstore(int bookstoreId, int page = 1, int pageSize = 10)
        {
            var result = await _service.GetOrderByBookstore(bookstoreId, page, pageSize);
            return Ok(result);
        }

        // ==========================
        // THANH TOÁN ONLINE (VNPAY, MoMo...)
        // ==========================
        // Tạo đơn hàng và khởi tạo giao dịch thanh toán Online (redirect URL).
        [HttpPost("checkout-online")]
        [Authorize(Roles = "Buyer")]
        public async Task<IActionResult> CheckoutOnline([FromBody] OrderCreateRequest request)
        {
            var customerId = GetCustomerId();
            var customerIpAddress = GetCustomerIpAddress();

            if (customerId == Guid.Empty)
                return Unauthorized("Không tìm thấy thông tin khách hàng (Customer ID) trong token.");

            // CHỈ CHO PHÉP thanh toán Online
            if (request.PaymentMethod == PaymentMethod.COD)
                return BadRequest(new { message = "Phương thức thanh toán COD phải được gọi qua API 'checkout-cod'." });

            try
            {
                var paymentTx = await _service.CreateAndInitiatePayment(customerId, request, customerIpAddress);

                // Phản hồi: Trả về URL thanh toán cho Frontend
                return Ok(new
                {
                    PaymentTransactionId = paymentTx.Id,
                    TotalAmount = paymentTx.TotalAmount,
                    PaymentUrl = paymentTx.PaymentUrl,
                    TransactionId = paymentTx.TransactionId,
                    PaymentStatus = paymentTx.PaymentStatus.ToString(),
                    // Không cần trả OrderIds, vì Frontend chỉ cần URL để redirect.
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = $"Lỗi dữ liệu: {ex.Message}" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = $"Lỗi logic thanh toán: {ex.Message}" });
            }
            catch (Exception ex)
            {
                // TODO: Log exception chi tiết
                return StatusCode(500, new { message = $"Lỗi hệ thống khi tạo đơn hàng: {ex.Message}" });
            }
        }

        // ==========================
        //  THANH TOÁN COD
        // ==========================
        // Tạo đơn hàng với phương thức thanh toán COD (Thanh toán khi nhận hàng).
        [HttpPost("checkout-cod")]
        [Authorize(Roles = "Buyer")]
        public async Task<IActionResult> CheckoutCOD([FromBody] OrderCreateRequest request)
        {
            var customerId = GetCustomerId();

            if (customerId == Guid.Empty)
                return Unauthorized("Không tìm thấy thông tin khách hàng (Customer ID) trong token.");

            // CHỈ CHO PHÉP thanh toán COD
            if (request.PaymentMethod != PaymentMethod.COD)
                return BadRequest(new { message = "API này chỉ hỗ trợ phương thức thanh toán COD." });

            try
            {
                // Gọi phương thức tạo đơn hàng COD
                // AccessToken không cần thiết cho luồng này nên truyền string.Empty
                var paymentTx = await _service.CreateFromCart(customerId, request, string.Empty);

                // Phản hồi: Trả về ID giao dịch đã được tạo
                return Ok(new
                {
                    PaymentTransactionId = paymentTx.Id,
                    TotalAmount = paymentTx.TotalAmount,
                    PaymentStatus = paymentTx.PaymentStatus.ToString(),
                    Message = "Đơn hàng COD được tạo thành công."
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = $"Lỗi dữ liệu: {ex.Message}" });
            }
            catch (Exception ex)
            {
                // TODO: Log exception chi tiết
                return StatusCode(500, new { message = $"Lỗi hệ thống khi tạo đơn hàng COD: {ex.Message}" });
            }
        }


        // ==========================
        // CALLBACKS (VNPAY)
        // ==========================

        /// GET: IPN URL (Server VNPAY gọi đến) - Nơi chính thức cập nhật DB.
        [HttpGet("vnpay-ipn")]
        [Produces("application/json")]
        public async Task<IActionResult> VnpayIpnCallback([FromQuery] Dictionary<string, string> vnpayData)
        {
            // Lấy vnp_TxnRef (ID nội bộ của PaymentTransaction) từ query string
            if (!vnpayData.TryGetValue("vnp_TxnRef", out var txnRef) || string.IsNullOrWhiteSpace(txnRef))
            {
                return Ok(new { RspCode = "99", Message = "Input data required (missing vnp_TxnRef)" });
            }

            try
            {
                // Gọi Service để xác thực Hash và cập nhật DB
                var success = await _service.HandlePaymentCallback(txnRef, vnpayData);

                if (success)
                {
                    // VNPAY yêu cầu phản hồi JSON: 00 = Thành công (DB đã cập nhật)
                    return Content("{\"RspCode\":\"00\",\"Message\":\"Confirm Success\"}", "application/json");
                }
                else
                {
                    // Lỗi: Chữ ký không hợp lệ, hoặc kiểm tra khác thất bại
                    return Ok(new { RspCode = "97", Message = "Invalid signature or payment failed" });
                }
            }
            catch (Exception ex)
            {
                // Lỗi hệ thống, yêu cầu VNPAY thử lại (Retry)
                // TODO: Log exception chi tiết
                return StatusCode(200, new { RspCode = "99", Message = $"System Error: {ex.Message}" });
            }
        }

        /// GET: Return URL (Trình duyệt Khách hàng được Redirect về) - Chỉ hiển thị thông báo.
        [HttpGet("vnpay-return")]
        public async Task<IActionResult> VnpayReturnCallback([FromQuery] Dictionary<string, string> vnpayData)
        {
            // KHÔNG cập nhật DB tại đây. Chỉ xác thực Hash và chuyển hướng khách hàng về trang kết quả.

            vnpayData.TryGetValue("vnp_TxnRef", out var txnRef);

            // Gọi service để xác thực kết quả nhận được từ VNPAY
            var validationResult = await _paymentService.HandleCallbackAsync(txnRef, vnpayData);

            // Dựa vào kết quả validation (kiểm tra hash và trạng thái) để hiển thị cho KH
            if (validationResult.Success)
            {
                // Giao dịch thành công (DB đã được IPN cập nhật)
                return Ok(new { Message = "Giao dịch thành công. Đơn hàng đang được xử lý.", TxnRef = txnRef, VNPAY_Status = vnpayData.GetValueOrDefault("vnp_ResponseCode") });
            }

            // Giao dịch thất bại
            return Ok(new { Message = $"Giao dịch thất bại. Mã lỗi VNPAY: {vnpayData.GetValueOrDefault("vnp_ResponseCode")}", TxnRef = txnRef });
        }


        // 
        [HttpPut("{id:guid}/confirm")]
        [Authorize(Roles = "Admin,Seller")]
        public async Task<IActionResult> Confirm(Guid id)
        {
            // Logic Confirm Order
            return Ok(new { message = "Confirm logic needs implementation." });
        }

        [HttpPut("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            // Logic Cancel Order
            return Ok(new { message = "Cancel logic needs implementation." });
        }
    }
}