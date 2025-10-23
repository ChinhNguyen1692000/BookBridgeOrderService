using OrderService.Application.Interface;
using Microsoft.AspNetCore.Mvc;

namespace OrderService.API.Controllers
{
    [ApiController]
    [Route("api/payment")]
    public class PaymentController : ControllerBase
    {
        private readonly IOrderServices _orderServices;

        public PaymentController(IOrderServices orderServices)
        {
            _orderServices = orderServices;
        }

        //[HttpGet("vnpay-callback-v2")]
        //public async Task<IActionResult> VnpayCallback()
        //{
        //    // Lấy tất cả tham số từ query string (đây là payload)
        //    var payload = HttpContext.Request.Query
        //        .ToDictionary(q => q.Key, q => q.Value.ToString());

        //    // Lấy ID giao dịch nội bộ của bạn (đã gửi lên VNPay là vnp_TxnRef)
        //    var transactionId = payload.ContainsKey("vnp_TxnRef") ? payload["vnp_TxnRef"] : "";

        //    if (string.IsNullOrEmpty(transactionId))
        //    {
        //        // Xử lý lỗi: Không có mã giao dịch
        //        return BadRequest(new { RspCode = "97", Message = "Missing required parameters" });
        //    }

        //    try
        //    {
        //        // Gọi OrderService để xử lý callback
        //        var isSuccess = await _orderServices.HandlePaymentCallback(transactionId, payload);

        //        // Trả về response theo quy định của VNPay
        //        return Ok(new { RspCode = "00", Message = "Success" });
        //    }
        //    catch (Exception ex)
        //    {
        //        // Xử lý lỗi hệ thống
        //        return Ok(new { RspCode = "99", Message = "Unknown error" });
        //    }
        //}
    }
}