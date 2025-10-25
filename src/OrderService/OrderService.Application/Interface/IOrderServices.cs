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

        Task<PaymentTransaction> CreateAndInitiatePayment(
            Guid customerId,
            OrderCreateRequest checkoutRequest,
            string customerIpAddress);

        Task<PaymentTransaction> CreateFromCart(
        Guid customerId,
        OrderCreateRequest checkoutRequest,
        string accessToken);

        Task<bool> HandlePaymentCallback(string transactionId, IDictionary<string, string> payload);
    }
}