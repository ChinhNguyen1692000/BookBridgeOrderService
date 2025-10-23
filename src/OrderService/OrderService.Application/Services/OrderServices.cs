using AutoMapper;
using Common.Paging;
using OrderService.Application.Interface;
using OrderService.Application.Models;
using OrderService.Domain.Entities;
using OrderService.Infracstructure.DBContext;
using System; // Cần thiết cho Exception và DateTime
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks; // Cần thiết cho async/await
using Microsoft.EntityFrameworkCore;
using OrderService.Infracstructure.Repositories;

namespace OrderService.Application.Services
{
    public class OrderServices : IOrderServices
    {
        private readonly IMapper _mapper;
        private readonly ICartClient _cartClient;
        private readonly IPaymentService _paymentService;
        private readonly OrderRepository _repo;

        public OrderServices(
            IMapper mapper,
            ICartClient cartClient,
            IPaymentService paymentService,
            OrderRepository repo)
        {
            _mapper = mapper;
            _cartClient = cartClient;
            _paymentService = paymentService;
            _repo = repo;
        }

        // --- Các phương thức CRUD/Query sử dụng int Id ---

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

        // Phương thức mới: Lấy order theo OrderStatus
        public async Task<PagedResult<Order>> GetOrderByCustomerAndStatus(OrderFilterByCustomerAndStatusRequest request, int pageNo, int pageSize)
        {
            var oL = await _repo.GetOrderByCustomerAndStatus(request.CustomerId, request.OrderStatus);
            var oLPaging = PagedResult<Order>.Create(oL, pageNo, pageSize);
            return oLPaging;
        }

        // Phương thức mới: Lấy order theo BookstoreId  
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

        //public async Task<Order> Create(OrderCreateRequest request)
        //{
        //    // Bắt buộc COD (theo yêu cầu hiện tại)
        //    if (request.PaymentMethod != PaymentMethod.COD)
        //        throw new InvalidOperationException("Service hiện tại chỉ hỗ trợ tạo đơn COD.");

        //    if (request.Stores == null || request.Stores.Count != 1)
        //        throw new ValidationException("Phương thức Create chỉ hỗ trợ tạo đơn hàng đơn lẻ, vui lòng đảm bảo Request chỉ chứa 1 Store.");

        //    var storeRequest = request.Stores.First();

        //    var order = new Order
        //    {
        //        CustomerId = request.CustomerId,
        //        BookstoreId = storeRequest.BookstoreId,
        //        CustomerPhoneNumber = request.CustomerPhoneNumber,
        //        DeliveryAddress = request.DeliveryAddress,
        //        OrderDate = DateTime.UtcNow,
        //        OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}", // Sử dụng GUID/Thời gian
        //        OrderStatus = OrderStatus.Confirmed, // COD mặc định xác nhận khi tạo (tuỳ nghiệp vụ, ở đây đánh dấu Confirmed)
        //        PaymentMethod = PaymentMethod.COD,
        //        PaymentProvider = null,
        //        PaymentStatus = PaymentStatus.Paid // COD tính là đã thanh toán ở đây theo yêu cầu "lưu order cho COD"
        //    };

        //    order.OrderItems = storeRequest.OrderItems.Select(i => new OrderItem
        //    {
        //        Id = Guid.NewGuid(),
        //        BookId = i.BookId,
        //        Quantity = i.Quantity,
        //        UnitPrice = i.UnitPrice,
        //        TotalPrice = i.Quantity * i.UnitPrice
        //    }).ToList();

        //    order.TotalQuantity = order.OrderItems.Sum(x => x.Quantity);
        //    order.TotalPrice = order.OrderItems.Sum(x => x.TotalPrice);

        //    // Tạo PaymentTransaction cho COD
        //    var paymentTx = new PaymentTransaction
        //    {
        //        Id = Guid.NewGuid(),
        //        TotalAmount = order.TotalPrice,
        //        PaymentStatus = PaymentStatus.Paid,
        //        PaymentUrl = "COD_SUCCESS",
        //        TransactionId = $"COD_TX_{Guid.NewGuid():N}",
        //        PaidDate = DateTime.UtcNow
        //    };

        //    // Liên kết
        //    order.PaymentTransactionId = paymentTx.Id;

        //    // Sử dụng transaction để đảm bảo atomicity
        //    using var transaction = await _orderDbContext.Database.BeginTransactionAsync();
        //    try
        //    {
        //        _orderDbContext.PaymentTransactions.Add(paymentTx);
        //        _orderDbContext.Orders.Add(order);

        //        // Lưu lần 1 để DB gán Order.Id (tự tăng) — cần để tạo OrderNumber có Id
        //        var rows = await _orderDbContext.SaveChangesAsync();
        //        if (rows == 0) throw new Exception("Không thể lưu order vào DB.");

        //        // Gán OrderNumber (có thể dùng millisecond để tránh trùng)
        //        order.OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{order.Id}";

        //        // Lưu lần 2 để cập nhật OrderNumber
        //        await _orderDbContext.SaveChangesAsync();

        //        await transaction.CommitAsync();

        //        return order;
        //    }
        //    catch
        //    {
        //        await transaction.RollbackAsync();
        //        throw;
        //    }
        //}

        //public async Task<PaymentTransaction> CreateFromCart(
        //    Guid customerId,
        //    OrderCreateRequest checkoutRequest,
        //    string accessToken)
        //{
        //    // Giờ chỉ xử lý COD
        //    if (checkoutRequest.PaymentMethod != PaymentMethod.COD)
        //        throw new InvalidOperationException("Service hiện tại chỉ hỗ trợ tạo đơn COD từ giỏ hàng.");

        //    if (checkoutRequest.Stores == null || !checkoutRequest.Stores.Any() || checkoutRequest.Stores.All(s => !s.OrderItems.Any()))
        //        throw new ArgumentException("Yêu cầu thanh toán không chứa mặt hàng nào hoặc cửa hàng hợp lệ.");

        //    using var transaction = await _orderDbContext.Database.BeginTransactionAsync();
        //    try
        //    {
        //        var createdOrders = new List<Order>();

        //        foreach (var store in checkoutRequest.Stores)
        //        {
        //            if (!store.OrderItems.Any()) continue;

        //            var order = new Order
        //            {
        //                CustomerId = customerId,
        //                BookstoreId = store.BookstoreId,
        //                CustomerPhoneNumber = checkoutRequest.CustomerPhoneNumber,
        //                DeliveryAddress = checkoutRequest.DeliveryAddress,
        //                PaymentMethod = PaymentMethod.COD,
        //                PaymentProvider = null,
        //                OrderDate = DateTime.UtcNow,
        //                OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}", // Giá trị duy nhất
        //                OrderStatus = OrderStatus.Confirmed,
        //                PaymentStatus = PaymentStatus.Paid
        //            };

        //            order.OrderItems = store.OrderItems.Select(i => new OrderItem
        //            {
        //                Id = Guid.NewGuid(),
        //                BookId = i.BookId,
        //                Quantity = i.Quantity,
        //                UnitPrice = i.UnitPrice,
        //                TotalPrice = i.UnitPrice * i.Quantity
        //            }).ToList();

        //            order.TotalQuantity = order.OrderItems.Sum(x => x.Quantity);
        //            order.TotalPrice = order.OrderItems.Sum(x => x.TotalPrice);

        //            createdOrders.Add(order);
        //            _orderDbContext.Orders.Add(order);
        //        }

        //        if (!createdOrders.Any())
        //            throw new ArgumentException("Không có đơn hàng nào được tạo.");

        //        // Tạo PaymentTransaction gộp cho tất cả orders (COD)
        //        var totalAmount = createdOrders.Sum(o => o.TotalPrice);
        //        var paymentTx = new PaymentTransaction
        //        {
        //            Id = Guid.NewGuid(),
        //            TotalAmount = totalAmount,
        //            PaymentStatus = PaymentStatus.Paid,
        //            PaymentUrl = "COD_SUCCESS",
        //            TransactionId = $"COD_TX_{Guid.NewGuid():N}",
        //            PaidDate = DateTime.UtcNow
        //        };

        //        // Liên kết tất cả orders tới PaymentTransaction này
        //        foreach (var order in createdOrders)
        //        {
        //            order.PaymentTransactionId = paymentTx.Id;
        //        }

        //        _orderDbContext.PaymentTransactions.Add(paymentTx);

        //        // Lưu để DB gán Id cho orders (lần 1)
        //        var rows = await _orderDbContext.SaveChangesAsync();
        //        if (rows == 0) throw new Exception("Lưu đơn hàng thất bại.");

        //        // Cập nhật OrderNumber cho từng order (cần order.Id)
        //        foreach (var order in createdOrders)
        //        {
        //            order.OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{order.Id}";
        //        }

        //        await _orderDbContext.SaveChangesAsync();

        //        // Xóa giỏ hàng (nếu có lỗi thì chỉ log, không throw để rollback giao dịch thanh toán)
        //        try
        //        {
        //            await _cartClient.ClearCartAsync(customerId.ToString(), accessToken);
        //        }
        //        catch (Exception ex)
        //        {
        //            // Không chặn luồng chính, log hoặc console
        //            Console.WriteLine($"Warning: Failed to clear cart for customer {customerId}: {ex.Message}");
        //        }

        //        await transaction.CommitAsync();

        //        return paymentTx;
        //    }
        //    catch
        //    {
        //        await transaction.RollbackAsync();
        //        throw;
        //    }
        //}


        //public async Task<PaymentTransaction> InitiatePayment(int orderId)
        //{
        //    // Không hỗ trợ khởi tạo thanh toán online trong bản hiện tại
        //    throw new NotSupportedException("Service hiện chỉ hỗ trợ COD; InitiatePayment (online) không được hỗ trợ.");
        //}

        //public async Task<bool> HandlePaymentCallback(string transactionId, IDictionary<string, string> payload)
        //{
        //    // Với bản chỉ COD, callback từ payment gateway không khả dụng.
        //    // Tuy nhiên giữ logic idempotent để nếu có transaction bị cập nhật vẫn an toàn.
        //    if (string.IsNullOrWhiteSpace(transactionId)) return false;

        //    var paymentTx = await _orderDbContext.PaymentTransactions
        //        .Include(pt => pt.Orders)
        //        .FirstOrDefaultAsync(pt => pt.TransactionId == transactionId);

        //    if (paymentTx == null) return false;

        //    // Idempotency: nếu đã Paid thì trả true ngay
        //    if (paymentTx.PaymentStatus == PaymentStatus.Paid) return true;

        //    // Nếu bản này chỉ COD, hầu như sẽ không có callback, nhưng nếu provider gửi Paid thì cập nhật:
        //    // Delegate xử lý logic xác thực payload cho _paymentService nếu cần (chỉ khi bạn tích hợp online)
        //    var result = await _paymentService.HandleCallbackAsync(transactionId, payload);

        //    if (result.Success)
        //    {
        //        paymentTx.PaymentStatus = PaymentStatus.Paid;
        //        paymentTx.PaidDate = DateTime.UtcNow;

        //        foreach (var order in paymentTx.Orders)
        //        {
        //            order.PaymentStatus = PaymentStatus.Paid;
        //            order.OrderStatus = OrderStatus.Confirmed;
        //        }
        //    }
        //    else
        //    {
        //        paymentTx.PaymentStatus = PaymentStatus.Failed;
        //        foreach (var order in paymentTx.Orders)
        //        {
        //            order.PaymentStatus = PaymentStatus.Failed;
        //            order.OrderStatus = OrderStatus.Canceled;
        //        }
        //    }

        //    await _orderDbContext.SaveChangesAsync();

        //    return true;
        //}

        //public async Task<bool> UpdatePaymentStatusAfterScan(int orderId)
        //{
        //    var order = await _orderDbContext.Orders
        //        .AsNoTracking()
        //        .FirstOrDefaultAsync(o => o.Id == orderId);

        //    if (order == null) return false;

        //    if (order.PaymentStatus == PaymentStatus.Paid) return true;

        //    if (order.PaymentTransactionId == null)
        //    {
        //        throw new InvalidOperationException($"Order {orderId} không có TransactionId. Có thể là COD (nhưng nếu là COD thì PaymentStatus nên là Paid).");
        //    }

        //    var paymentTx = await _orderDbContext.PaymentTransactions
        //        .Include(pt => pt.Orders)
        //        .FirstOrDefaultAsync(pt => pt.Id == order.PaymentTransactionId.Value);

        //    if (paymentTx == null) return false;

        //    if (paymentTx.PaymentStatus == PaymentStatus.Paid)
        //    {
        //        foreach (var relatedOrder in paymentTx.Orders)
        //        {
        //            if (relatedOrder.PaymentStatus != PaymentStatus.Paid)
        //            {
        //                relatedOrder.PaymentStatus = PaymentStatus.Paid;
        //                relatedOrder.OrderStatus = OrderStatus.Confirmed;
        //            }
        //        }

        //        await _orderDbContext.SaveChangesAsync();
        //        return true;
        //    }

        //    // Với chế độ chỉ COD, thường đây luôn là Paid. Tuy nhiên giữ check với payment service nếu cần.
        //    if (!string.IsNullOrWhiteSpace(paymentTx.TransactionId))
        //    {
        //        var isPaid = await _paymentService.CheckTransactionStatusAsync(paymentTx.TransactionId);

        //        if (isPaid)
        //        {
        //            paymentTx.PaymentStatus = PaymentStatus.Paid;

        //            foreach (var relatedOrder in paymentTx.Orders)
        //            {
        //                relatedOrder.PaymentStatus = PaymentStatus.Paid;
        //                relatedOrder.OrderStatus = OrderStatus.Confirmed;
        //            }

        //            await _orderDbContext.SaveChangesAsync();
        //            return true;
        //        }
        //    }

        //    return false;
        //}

        public async Task<IEnumerable<Order>> SearchByCustomerEmail(string email)
        {
            throw new NotImplementedException("You must call UserService to resolve email->userId");
        }
    }
}
