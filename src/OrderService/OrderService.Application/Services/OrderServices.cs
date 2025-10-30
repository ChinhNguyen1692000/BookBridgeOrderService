using AutoMapper;
using Common.Paging;
using OrderService.Application.Interface;
using OrderService.Application.Models;
using OrderService.Domain.Entities;
using OrderService.Infracstructure.DBContext;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrderService.Infracstructure.Repositories;

namespace OrderService.Application.Services
{
    public class OrderServices : IOrderServices
    {
        private readonly IMapper _mapper;
        private readonly IPaymentService _paymentService;
        private readonly OrderRepository _repo;
        private readonly PaymentTransactionRepository _paymentTxRepo;
        private readonly OrderDbContext _orderDbContext;

        public OrderServices(
            IMapper mapper,
            IPaymentService paymentService,
            OrderRepository repo,
            PaymentTransactionRepository paymentTxRepo,
            OrderDbContext orderDbContext)
        {
            _mapper = mapper;
            _paymentService = paymentService;
            _repo = repo;
            _paymentTxRepo = paymentTxRepo;
            _orderDbContext = orderDbContext;
        }

        // --- Các phương thức CRUD/Query sử dụng int Id (Giữ nguyên) ---

        public async Task<PagedResult<Order>> GetAll(int pageNo, int pageSize)
        {
            var oL = await _repo.GetAllAsync();
            var oLPaging = PagedResult<Order>.Create(oL, pageNo, pageSize);

            return oLPaging;
        }

        public async Task<Order> GetById(int id)
        {
            return await _repo.GetByIdAsync(id);
        }

        public async Task<PagedResult<Order>> GetOrderByCustomer(Guid customerId, int pageNo, int pageSize)
        {
            var oL = await _repo.GetOrdersByCustomerAsync(customerId);
            var oLPaging = PagedResult<Order>.Create(oL, pageNo, pageSize);
            return oLPaging;
        }

        public async Task<PagedResult<Order>> GetOrderByCustomerAndStatus(OrderFilterByCustomerAndStatusRequest request, int pageNo, int pageSize)
        {
            if (!Guid.TryParse(request.CustomerId.ToString(), out Guid customerGuid))
            {
                throw new ArgumentException("CustomerId không hợp lệ.");
            }
            var oL = await _repo.GetOrderByCustomerAndStatus(customerGuid, request.OrderStatus);
            var oLPaging = PagedResult<Order>.Create(oL, pageNo, pageSize);
            return oLPaging;
        }

        public async Task<PagedResult<Order>> GetOrderByBookstore(int bookstoreId, int pageNo, int pageSize)
        {
            var oL = await _repo.GetOrderByBookstore(bookstoreId);
            var oLPaging = PagedResult<Order>.Create(oL, pageNo, pageSize);
            return oLPaging;
        }

        public async Task<Order> Update(int id, OrderUpdateRequest request)
        {
            var exist = await _repo.GetByIdAsync(id);

            if (exist == null) throw new Exception("Order not found");

            exist.CustomerPhoneNumber = request.CustomerPhoneNumber ?? exist.CustomerPhoneNumber;
            exist.DeliveryAddress = request.DeliveryAddress ?? exist.DeliveryAddress;
            exist.PaymentMethod = request.PaymentMethod ?? exist.PaymentMethod;
            exist.PaymentProvider = request.PaymentProvider ?? exist.PaymentProvider;

            var result = await _repo.UpdateAsync(exist);

            if (result == null) throw new Exception("Update failed");
            return exist;
        }

        // --- PHƯƠNG THỨC XỬ LÝ THANH TOÁN ONLINE (VNPAY) ---
        public async Task<PaymentTransaction> CreateAndInitiatePayment(
            Guid customerId,
            OrderCreateRequest checkoutRequest,
            string customerIpAddress)
        {
            if (checkoutRequest.Stores == null || !checkoutRequest.Stores.Any() || checkoutRequest.Stores.All(s => !s.OrderItems.Any()))
                throw new ArgumentException("Yêu cầu thanh toán không chứa mặt hàng nào hoặc cửa hàng hợp lệ.");
            
            // CHỈ XỬ LÝ LUỒNG ONLINE (VNPAY)
            if (checkoutRequest.PaymentMethod == PaymentMethod.COD)
                throw new InvalidOperationException("Phương thức COD không được gọi qua API này.");

            // 1. Gán PaymentProvider (tất cả các hình thức online đều dùng VNPAY)
            var paymentProvider = PaymentProvider.VNPay;
            // Nếu bạn muốn mở rộng sau này, có thể dùng logic:
            // if (checkoutRequest.PaymentMethod == PaymentMethod.EWallet) paymentProvider = PaymentProvider.MoMo;

            // 2. Tạo các Order (từ mỗi cửa hàng)
            var createdOrders = new List<Order>();

            foreach (var store in checkoutRequest.Stores)
            {
                if (!store.OrderItems.Any()) continue;

                var order = new Order
                {
                    CustomerId = customerId,
                    BookstoreId = store.BookstoreId,
                    CustomerPhoneNumber = checkoutRequest.CustomerPhoneNumber,
                    DeliveryAddress = checkoutRequest.DeliveryAddress,
                    PaymentMethod = checkoutRequest.PaymentMethod,
                    // SỬA LỖI GÁN PaymentProvider (Sử dụng enum đã định nghĩa)
                    PaymentProvider = paymentProvider, 
                    OrderDate = DateTime.UtcNow,
                    OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
                    OrderStatus = OrderStatus.Created, // Đã tạo, chờ thanh toán
                    PaymentStatus = PaymentStatus.Pending
                };

                order.OrderItems = store.OrderItems.Select(i => new OrderItem
                {
                    Id = Guid.NewGuid(),
                    BookId = i.BookId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.UnitPrice * i.Quantity
                }).ToList();

                order.TotalQuantity = order.OrderItems.Sum(x => x.Quantity);
                order.TotalPrice = order.OrderItems.Sum(x => x.TotalPrice);

                createdOrders.Add(order);
            }

            if (!createdOrders.Any())
                throw new ArgumentException("Không có đơn hàng nào được tạo.");

            // 3. Tạo PaymentTransaction gộp cho tất cả orders
            var totalAmount = createdOrders.Sum(o => o.TotalPrice);
            var paymentTx = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                TotalAmount = totalAmount,
                PaymentStatus = PaymentStatus.Pending,
                PaymentUrl = null,
                TransactionId = Guid.NewGuid().ToString("N"),
                PaidDate = null
            };

            foreach (var order in createdOrders)
            {
                order.PaymentTransactionId = paymentTx.Id;
            }

            // 4. Khởi tạo thanh toán với Provider (VNPAY)
            var paymentResult = await _paymentService.InitiatePaymentAsync(paymentTx, customerIpAddress);

            if (!paymentResult.Success)
            {
                throw new InvalidOperationException($"Không thể khởi tạo thanh toán với nhà cung cấp: {paymentResult.Message}");
            }

            // 5. Cập nhật PaymentTransaction với thông tin từ Provider
            paymentTx.PaymentUrl = paymentResult.PaymentUrl;
            paymentTx.TransactionId = paymentResult.TransactionId;

            // 6. Lưu tất cả Orders và PaymentTransaction trong 1 Transaction DB
            var (savedTx, savedOrders) = await _paymentTxRepo.SavePaymentTransactionAndOrdersInTransactionAsync(
                paymentTx,
                createdOrders,
                (tx, orders) =>
                {
                    // Cập nhật OrderNumber bằng ID sau khi lưu DB
                    foreach (var order in orders)
                    {
                        order.OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{order.Id}";
                    }
                    return Task.CompletedTask;
                }
            );

            if (savedTx == null) throw new Exception("Tạo giao dịch thanh toán thất bại.");

            return savedTx;
        }

        // --- PHƯƠNG THỨC XỬ LÝ THANH TOÁN COD ---
        public async Task<PaymentTransaction> CreateFromCart(
            Guid customerId,
            OrderCreateRequest checkoutRequest,
            string accessToken)
        {
            // CHỈ XỬ LÝ LUỒNG COD
            if (checkoutRequest.PaymentMethod != PaymentMethod.COD)
                throw new InvalidOperationException("Service này chỉ hỗ trợ tạo đơn COD từ giỏ hàng.");

            if (checkoutRequest.Stores == null || !checkoutRequest.Stores.Any() || checkoutRequest.Stores.All(s => !s.OrderItems.Any()))
                throw new ArgumentException("Yêu cầu thanh toán không chứa mặt hàng nào hoặc cửa hàng hợp lệ.");

            var createdOrders = new List<Order>();

            foreach (var store in checkoutRequest.Stores)
            {
                if (!store.OrderItems.Any()) continue;

                var order = new Order
                {
                    CustomerId = customerId,
                    BookstoreId = store.BookstoreId,
                    CustomerPhoneNumber = checkoutRequest.CustomerPhoneNumber,
                    DeliveryAddress = checkoutRequest.DeliveryAddress,
                    PaymentMethod = PaymentMethod.COD,
                    PaymentProvider = PaymentProvider.None, // COD không cần Payment Provider
                    OrderDate = DateTime.UtcNow,
                    OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
                    OrderStatus = OrderStatus.Confirmed, // COD: Đơn hàng được xác nhận ngay
                    PaymentStatus = PaymentStatus.Paid // COD: Được xem là đã thanh toán (khi nhận hàng)
                };

                order.OrderItems = store.OrderItems.Select(i => new OrderItem
                {
                    Id = Guid.NewGuid(),
                    BookId = i.BookId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.UnitPrice * i.Quantity
                }).ToList();

                order.TotalQuantity = order.OrderItems.Sum(x => x.Quantity);
                order.TotalPrice = order.OrderItems.Sum(x => x.TotalPrice);

                createdOrders.Add(order);
            }

            if (!createdOrders.Any())
                throw new ArgumentException("Không có đơn hàng nào được tạo.");

            // Tạo PaymentTransaction gộp cho tất cả orders (COD)
            var totalAmount = createdOrders.Sum(o => o.TotalPrice);
            var paymentTx = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                TotalAmount = totalAmount,
                PaymentStatus = PaymentStatus.Paid,
                PaymentUrl = "COD_SUCCESS",
                TransactionId = $"COD_TX_{Guid.NewGuid():N}",
                PaidDate = DateTime.UtcNow
            };

            foreach (var order in createdOrders)
            {
                order.PaymentTransactionId = paymentTx.Id;
            }

            var (savedTx, savedOrders) = await _paymentTxRepo.SavePaymentTransactionAndOrdersInTransactionAsync(
                paymentTx,
                createdOrders,
                (tx, orders) =>
                {
                    foreach (var order in orders)
                    {
                        order.OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{order.Id}";
                    }
                    return Task.CompletedTask;
                }
            );

            if (savedTx == null) throw new Exception("Tạo giao dịch thanh toán thất bại.");
            
            return savedTx;
        }

        // --- PHƯƠNG THỨC XỬ LÝ CALLBACK (IPN) (Giữ nguyên) ---
        public async Task<bool> HandlePaymentCallback(string transactionId, IDictionary<string, string> payload)
        {
            if (string.IsNullOrWhiteSpace(transactionId)) return false;

            var paymentTx = await _paymentTxRepo.GetByTransactionIdWithOrdersAsync(transactionId);

            if (paymentTx == null) return false;

            if (paymentTx.PaymentStatus != PaymentStatus.Pending) return true;

            var result = await _paymentService.HandleCallbackAsync(transactionId, payload);

            if (result.Success)
            {
                paymentTx.PaymentStatus = PaymentStatus.Paid;
                paymentTx.PaidDate = DateTime.UtcNow;

                foreach (var order in paymentTx.Orders)
                {
                    order.PaymentStatus = PaymentStatus.Paid;
                    order.OrderStatus = OrderStatus.Confirmed;
                    _orderDbContext.Entry(order).State = EntityState.Modified;
                }
            }
            else
            {
                paymentTx.PaymentStatus = PaymentStatus.Failed;
                
                foreach (var order in paymentTx.Orders)
                {
                    order.PaymentStatus = PaymentStatus.Failed;
                    order.OrderStatus = OrderStatus.Canceled;
                    _orderDbContext.Entry(order).State = EntityState.Modified;
                }
            }

            _orderDbContext.Entry(paymentTx).State = EntityState.Modified;

            await _orderDbContext.SaveChangesAsync();

            return result.Success;
        }

        public async Task<IEnumerable<Order>> SearchByCustomerEmail(string email)
        {
            throw new NotImplementedException("You must call UserService to resolve email->userId");
        }
    }
}