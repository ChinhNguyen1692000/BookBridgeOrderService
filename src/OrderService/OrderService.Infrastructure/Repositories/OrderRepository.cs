using Common.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using OrderService.Infracstructure.DBContext;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OrderService.Infracstructure.Repositories
{
    public class OrderRepository : BaseRepository<Order, Guid>
    {
        // Cần DbContext để quản lý Transaction cho OrderServices
        private readonly OrderDbContext _context;

        // Cập nhật Constructor để lưu _context
        public OrderRepository(OrderDbContext context) : base(context) 
        {
            _context = context;
        }

        // ======================= PRIVATE HELPERS ======================= //

        private async Task<bool> UpdateOrderFieldAsync(int orderId, Action<Order> updateAction)
        {
            // Sử dụng FindAsync để tránh AsNoTracking nếu muốn update
            var order = await _dbSet.FirstOrDefaultAsync(o => o.Id == orderId); 
            if (order == null) return false;

            updateAction(order);
            await _context.SaveChangesAsync();
            return true;
        }

        // ======================= GET METHODS ======================= //

        public async Task<List<Order>> GetOrderByBookstore(int storeId)
        {
            // Thêm Include(OrderItems) cho đủ thông tin
            return await _dbSet
                .Include(o => o.OrderItems)
                .Where(o => o.BookstoreId == storeId)
                .AsNoTracking()
                .ToListAsync();
        }

        // Đổi kiểu dữ liệu của userId sang Guid
        public async Task<List<Order>> GetOrderByCustomerAndStatus(Guid userId, int orderStatus)
        {
            var status = (OrderStatus)orderStatus;
            return await _dbSet
                .Include(o => o.OrderItems)
                .Where(o => o.CustomerId.Equals(userId) && o.OrderStatus == status)
                .AsNoTracking() // Thêm AsNoTracking
                .ToListAsync();
        }

        public async Task<List<Order>> GetOrdersByCustomerAsync(Guid customerId)
        {
            return await _dbSet
                .Include(o => o.OrderItems)
                .Where(o => o.CustomerId == customerId)
                .AsNoTracking()
                .ToListAsync();
        }

        // Chỉnh sửa để trả về Order? và thêm Include/AsNoTracking
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

        // Sử dụng phương thức Add của BaseRepository (hoặc có thể tạo lại với logic đặc biệt nếu cần)
        public async Task<Order> CreateAsync(Order order)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));
            await _dbSet.AddAsync(order);
            await _context.SaveChangesAsync(); // Lưu thay đổi để có Order.Id
            return order;
        }

        // Thêm UpdateAsync để OrderServices có thể sử dụng (đã có trong BaseRepository, nhưng cần triển khai cụ thể nếu không dùng BaseRepository.Update)
        public async Task<Order> UpdateAsync(Order order)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));
            _dbSet.Update(order);
            await _context.SaveChangesAsync();
            return order;
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