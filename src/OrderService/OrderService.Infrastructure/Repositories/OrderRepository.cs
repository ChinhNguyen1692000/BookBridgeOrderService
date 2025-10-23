using Common.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using OrderService.Infracstructure.DBContext;

namespace OrderService.Infracstructure.Repositories
{
    public class OrderRepository : BaseRepository<Order, Guid>
    {
        public OrderRepository(OrderDbContext context) : base(context) { }

        // ======================= PRIVATE HELPERS ======================= //

       
        private async Task<bool> UpdateOrderFieldAsync(int orderId, Action<Order> updateAction)
        {
            var order = await _dbSet.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return false;

            updateAction(order);
            await _context.SaveChangesAsync();
            return true;
        }

        // ======================= GET METHODS ======================= //

        public async Task<List<Order>> GetOrderByBookstore(int storeId)
        {
            return await _dbSet.Where(o => o.BookstoreId == storeId).ToListAsync();
        }
        public async Task<List<Order>> GetOrderByCustomerAndStatus(string userId, int orderStatus)
        {
            var status = (OrderStatus)orderStatus;
            return await _dbSet.Include(o => o.OrderItems).Where(o => o.CustomerId.Equals(userId) && o.OrderStatus==status).ToListAsync();
        }
        public async Task<List<Order>> GetOrdersByCustomerAsync(Guid customerId)
        {
            return await _dbSet
                .Include(o => o.OrderItems)
                .Where(o => o.CustomerId == customerId)
                .AsNoTracking()
                .ToListAsync();
        }


        public async Task<Order?> GetByIdAsync(int id)
        {
            return await _dbSet
                .Include(o => o.OrderItems)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<List<Order>> GetAllAsync()
        {
            return await _dbSet
                .Include(o => o.OrderItems)
                .AsNoTracking()
                .ToListAsync();
        }
        

        // ======================= CREATE / UPDATE ======================= //
        public async Task<bool> CreateOrderAsync(Order order)
        {
            if (order == null) return false;
            try
            {
                await _dbSet.AddAsync(order); // <-- dùng _dbSet, không _context.Orders
                Console.WriteLine($"DB Provider: {_context.Database.ProviderName}");
                Console.WriteLine($"DB Conn: {_context.Database.GetConnectionString()}");
                await _context.SaveChangesAsync();
                Console.WriteLine($"✅ Order {order.OrderNumber} saved successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Save failed: {ex}");
                return false;
            }
        }



        public async Task<bool> UpdateOrderAsync(Order order)
        {
            if (order == null) return false;

            _dbSet.Update(order);
            await _context.SaveChangesAsync();
            return true;
        }

        // ======================= UPDATE FIELDS ======================= //
        public Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus status)
            => UpdateOrderFieldAsync(orderId, o => o.OrderStatus = status);

        public Task<bool> UpdatePaymentStatusAsync(int orderId, PaymentStatus status)
            => UpdateOrderFieldAsync(orderId, o => o.PaymentStatus = status);

        public Task<bool> UpdatePaymentProviderAsync(int orderId, PaymentProvider provider)
            => UpdateOrderFieldAsync(orderId, o => o.PaymentProvider = provider);

        public Task<bool> UpdatePaymentMethodAsync(int orderId, PaymentMethod method)
            => UpdateOrderFieldAsync(orderId, o => o.PaymentMethod = method);

        public Task<bool> UpdateDeliveredDateAsync(int orderId, DateTime deliveredDate)
            => UpdateOrderFieldAsync(orderId, o => o.DeliveriedDate = deliveredDate);
    }
}
