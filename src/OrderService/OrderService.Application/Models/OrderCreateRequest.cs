// Trong OrderService.Application.Models
// File: OrderCreateRequest.cs

public class OrderCreateRequest
{
    public Guid CustomerId { get; set; }
    // public string CustomerEmail { get; set; } // Sẽ được Controller ghi đè
    public string CustomerPhoneNumber { get; set; }
    public string DeliveryAddress { get; set; }
    public OrderService.Domain.Entities.PaymentMethod PaymentMethod { get; set; }
    public OrderService.Domain.Entities.PaymentProvider? PaymentProvider { get; set; }

    // Dữ liệu Multi-Store/Checkout
    public List<StoreCheckoutDto> Stores { get; set; } = new List<StoreCheckoutDto>();
}

// File: StoreCheckoutDto.cs
public class StoreCheckoutDto
{
    public int BookstoreId { get; set; }
    public List<OrderItemCreateRequest> OrderItems { get; set; } = new List<OrderItemCreateRequest>();
}

// File: OrderItemCreateRequest.cs
public class OrderItemCreateRequest
{
    public int BookId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}