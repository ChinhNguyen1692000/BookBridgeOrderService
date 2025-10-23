using Common.Paging;
using OrderService.Application.Models;
using OrderService.Domain.Entities;

namespace OrderService.Application.Interface
{
    public interface IOrderServices
    {
        Task<PagedResult<Order>> GetAll(int page, int pageSize);
        Task<Order> GetById(int id);
        Task<PagedResult<Order>> GetOrderByCustomer(Guid customerId, int pageNo, int pageSize);
        Task<PagedResult<Order>> GetOrderByCustomerAndStatus(OrderFilterByCustomerAndStatusRequest request, int pageNo, int pageSize);
        Task<Order> Update(int id, OrderUpdateRequest request);
        Task<PagedResult<Order>> GetOrderByBookstore(int bookstoreId, int pageNo, int pageSize);


        //Task<Order> Create(OrderCreateRequest request);

        //// THAY ĐỔI: Thêm OrderCreateRequest để nhận thông tin nhận hàng và PaymentMethod,
        //// và accessToken để truyền cho CartClient.
        //Task<PaymentTransaction> CreateFromCart(Guid customerId, OrderCreateRequest checkoutRequest, string accessToken);

        //Task<IEnumerable<Order>> SearchByCustomerEmail(string email);
        //Task<PaymentTransaction> InitiatePayment(int orderId);

        //// THÊM: Method để xử lý kết quả callback/webhook thanh toán
        //Task<bool> HandlePaymentCallback(string transactionId, IDictionary<string, string> payload);

        //// THÊM: Method để cập nhật trạng thái sau khi user quét QR (có thể dùng để poll)
        //Task<bool> UpdatePaymentStatusAfterScan(int orderId);

        //Task<PagedResult<Order>> GetOrderByStatus(OrderStatus status, int pageNo, int pageSize);

        //// Lấy order theo BookstoreId
    }
}