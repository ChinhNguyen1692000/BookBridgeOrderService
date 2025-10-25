namespace OrderService.Application.Models
{
    public class CartItemDto
    {
        public int BookId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
