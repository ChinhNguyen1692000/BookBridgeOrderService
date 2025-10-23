using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Application.Interface;
using OrderService.Application.Models;
using OrderService.Domain.Entities;
using System.Security.Claims;

namespace OrderService.Api.Controllers
{
    //[Authorize]
    [ApiController]
    [Route("api/orders")]
    public class OrderController : BaseApiController
    {
        private readonly IOrderServices _service;

        public OrderController(IOrderServices service)
        {
            _service = service;
        }

        // ==========================
        // 🔹 Helper methods (Giữ nguyên)
        // ==========================
        private Guid GetCustomerId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? User.FindFirstValue("nameid");
            return Guid.TryParse(id, out var guid) ? guid : Guid.Empty;
        }

        private string GetCustomerEmail() =>
            User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue("email")
            ?? string.Empty;

        /// <summary>
        /// Lấy tất cả Order
        /// </summary>
        [HttpGet("list")]
        public async Task<IActionResult> GetAll(int page = 1, int pageSize = 10)
        {
            var result = await _service.GetAll(page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Lấy Order theo id
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var order = await _service.GetById(id);
            return order is null ? NotFound() : Ok(order);
        }

        /// <summary>
        /// Lấy Order theo Customer
        /// </summary>
        [HttpGet("by-customer/{customerId:guid}")]
        public async Task<IActionResult> GetByCustomer(Guid customerId, int page = 1, int pageSize = 10)
        {
            var result = await _service.GetOrderByCustomer(customerId, page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Lấy Order theo customer và status
        /// </summary>
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

        /// <summary>
        /// Lấy Order theo store
        /// </summary>
        [HttpGet("by-bookstore/{bookstoreId:guid}")]
        [Authorize(Roles = "Admin,Seller")] // Admin/Seller mới có quyền truy vấn theo BookstoreId
        public async Task<IActionResult> GetByBookstore(int bookstoreId, int page = 1, int pageSize = 10)
        {
            var result = await _service.GetOrderByBookstore(bookstoreId, page, pageSize);
            return Ok(result);
        }

        //[HttpPost("create")]
        //public async Task<IActionResult> Create([FromBody] OrderCreateRequest request)
        //{
        //    var order = await _service.Create(request);
        //    return Ok(order);
        //}

        //// Tạo đơn hàng qua giỏ hàng (checkout flow)
        //[HttpPost("checkout/create")]
        //public async Task<IActionResult> CreateFromCart([FromBody] OrderCreateRequest request)
        //{
        //    var customerId = GetCustomerId();
        //    var customerEmail = GetCustomerEmail();
        //    var accessToken = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

        //    if (customerId == Guid.Empty || string.IsNullOrEmpty(customerEmail))
        //        return Unauthorized("Không tìm thấy thông tin định danh (ID/Email) của khách hàng trong token.");

        //    if (string.IsNullOrEmpty(accessToken))
        //        return Unauthorized("Không tìm thấy Access Token.");

        //    try
        //    {
        //        request.CustomerId = customerId;
        //        request.CustomerEmail = customerEmail;

        //        var paymentTx = await _service.CreateFromCart(customerId, request, accessToken);

        //        if (request.PaymentMethod == PaymentMethod.COD)
        //        {
        //            return Ok(new
        //            {
        //                Message = "Đơn hàng COD đã được tạo thành công.",
        //                OrderIds = paymentTx.Orders.Select(o => o.Id).ToList()
        //            });
        //        }

        //        // Phản hồi thanh toán online: Lấy PaymentUrl và TransactionId từ PaymentTransaction
        //        return Ok(new
        //        {
        //            PaymentTransactionId = paymentTx.Id,
        //            TotalAmount = paymentTx.TotalAmount,
        //            PaymentUrl = paymentTx.PaymentUrl,          // ✅ Correct: Lấy từ paymentTx
        //            TransactionId = paymentTx.TransactionId,    // ✅ Correct: Lấy từ paymentTx
        //            PaymentStatus = paymentTx.PaymentStatus.ToString(),
        //            OrderIds = paymentTx.Orders.Select(o => o.Id).ToList()
        //        });
        //    }
        //    catch (ArgumentException ex)
        //    {
        //        return BadRequest(new { message = $"Lỗi dữ liệu: {ex.Message}" });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { message = $"Lỗi hệ thống khi tạo đơn hàng: {ex.Message}" });
        //    }
        //}


        // ==========================
        // 🔹 POST: Khởi tạo thanh toán (Đơn hàng đơn lẻ)
        // ==========================
        //[HttpPost("{id:guid}/payment/initiate")]
        //public async Task<IActionResult> InitiatePayment(int id)
        //{
        //    try
        //    {
        //        var paymentTx = await _service.InitiatePayment(id);

        //        return Ok(new
        //        {
        //            PaymentTransactionId = paymentTx.Id,
        //            TotalAmount = paymentTx.TotalAmount,
        //            PaymentUrl = paymentTx.PaymentUrl,          // ✅ Correct: Lấy từ paymentTx
        //            TransactionId = paymentTx.TransactionId,    // ✅ Correct: Lấy từ paymentTx
        //            PaymentStatus = paymentTx.PaymentStatus.ToString()
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(new { message = $"Lỗi khởi tạo thanh toán: {ex.Message}" });
        //    }
        //}

        // 🔹 POST: Callback từ nhà cung cấp thanh toán (Webhook) (Giữ nguyên)
        //[AllowAnonymous]
        //[HttpPost("payment/callback")]
        //public async Task<IActionResult> PaymentCallback([FromForm] string transactionId)
        //{
        //    var dict = Request.Form.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

        //    var success = await _service.HandlePaymentCallback(transactionId, dict);

        //    if (!success)
        //        return BadRequest(new { message = "Xử lý callback thanh toán thất bại." });

        //    return Ok("Success");
        //}

        // ==========================
        // 🔹 PUT (Giữ nguyên)
        // ==========================
        [HttpPut("{id:guid}/confirm")]
        [Authorize(Roles = "Admin,Seller")]
        public async Task<IActionResult> Confirm(Guid id)
        {
            return Ok(new { message = "Confirm logic needs implementation." });
        }

        [HttpPut("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            return Ok(new { message = "Cancel logic needs implementation." });
        }

        // ==========================
        // 🔹 POST: Kiểm tra trạng thái thanh toán (Polling)
        // ==========================
        //[HttpPost("{orderId:guid}/payment/check-status")]
        //public async Task<IActionResult> CheckPaymentStatus(int orderId)
        //{
        //    var order = await _service.GetById(orderId);
        //    if (order == null) return NotFound();

        //    // 1. Kiểm tra trạng thái trong DB trước
        //    if (order.PaymentStatus == PaymentStatus.Paid)
        //        // Phản hồi chỉ dùng thuộc tính của Order (PaymentStatus, PaymentTransactionId)
        //        return Ok(new { status = "Paid", order.PaymentTransactionId });

        //    // 2. Nếu chưa Paid, gọi Service để kiểm tra Payment Transaction từ cổng thanh toán
        //    var isPaid = await _service.UpdatePaymentStatusAfterScan(orderId);

        //    if (isPaid)
        //        // Lấy lại Order đã cập nhật để trả về status mới
        //        order = await _service.GetById(orderId);

        //    // Phản hồi chỉ dùng thuộc tính của Order (PaymentStatus, PaymentTransactionId)
        //    return Ok(new { status = order.PaymentStatus.ToString(), order.PaymentTransactionId });
        //}
    }
}