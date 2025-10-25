using Common.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using OrderService.Infracstructure.DBContext;
using System;
using System.Threading.Tasks;

namespace OrderService.Infracstructure.Repositories
{
    public class PaymentTransactionRepository : BaseRepository<PaymentTransaction, Guid>
    {
        // Cần DbContext để quản lý Transaction
        private readonly OrderDbContext _context;

        public PaymentTransactionRepository(OrderDbContext context) : base(context)
        {
            _context = context;
        }

        // Phương thức lấy PaymentTransaction theo TransactionId và bao gồm Orders
        public async Task<PaymentTransaction?> GetByTransactionIdWithOrdersAsync(string transactionId)
        {
            return await _dbSet
                .Include(pt => pt.Orders)
                .FirstOrDefaultAsync(pt => pt.TransactionId == transactionId);
        }

        // Phương thức lấy PaymentTransaction theo Id và bao gồm Orders
        public async Task<PaymentTransaction?> GetByIdWithOrdersAsync(Guid transactionId)
        {
            return await _dbSet
                .Include(pt => pt.Orders)
                .FirstOrDefaultAsync(pt => pt.Id == transactionId);
        }

        // Phương thức lưu gộp PaymentTransaction và danh sách Orders liên quan, sử dụng Transaction
        public async Task<(PaymentTransaction?, List<Order>?)> SavePaymentTransactionAndOrdersInTransactionAsync(
            PaymentTransaction paymentTx,
            List<Order> orders,
            Func<PaymentTransaction, List<Order>, Task> updateBeforeCommitAction = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Thêm PaymentTransaction và Orders vào DbContext
                await _dbSet.AddAsync(paymentTx);
                await _context.Orders.AddRangeAsync(orders);

                // Lưu lần 1 để DB gán Order.Id (tự tăng)
                var rows = await _context.SaveChangesAsync();
                if (rows == 0) throw new Exception("Không thể lưu PaymentTransaction và Orders lần 1.");

                // Thực hiện hành động cập nhật bổ sung (ví dụ: cập nhật OrderNumber dựa trên Order.Id)
                if (updateBeforeCommitAction != null)
                {
                    await updateBeforeCommitAction(paymentTx, orders);
                }

                // Lưu lần 2 để cập nhật OrderNumber hoặc các thay đổi khác
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return (paymentTx, orders);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}