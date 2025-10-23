using OrderService.Domain.Entities;

namespace OrderService.Application.Models
{

    public class OrderItemRequest
    {
        public int BookId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class OrderUpdateRequest
    {
        public string CustomerPhoneNumber { get; set; }
        public string DeliveryAddress { get; set; }
        public PaymentMethod? PaymentMethod { get; set; }
        public PaymentProvider? PaymentProvider { get; set; }
        // trạng thái update nếu cần
    }

    public class OrderSearchRequest
    {
        public string? CustomerEmail { get; set; } // nếu search bằng email
        public Guid? CustomerId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
    public class OrderFilterByCustomerAndStatusRequest
    {
        public string CustomerId { get; set; }
        public int OrderStatus { get; set; }
    }

}
