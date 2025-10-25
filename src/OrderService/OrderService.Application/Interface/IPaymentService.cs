using OrderService.Application.Models;
using OrderService.Domain.Entities;
using System.Threading.Tasks;
using System.Collections.Generic; // Cần thiết

namespace OrderService.Application.Interface
{
    public interface IPaymentService
    {
        /// <summary>
        /// Initiate payment for a transaction: return paymentUrl + txId
        /// (Sửa để nhận PaymentTransaction thay vì Order)
        /// </summary>
        Task<PaymentResult> InitiatePaymentAsync(PaymentTransaction paymentTransaction, string customerIpAddress); // <- ĐÃ SỬA

        /// <summary>
        /// Verify or handle provider callback; returns whether payment succeeded
        /// </summary>
        Task<PaymentResult> HandleCallbackAsync(string transactionId, IDictionary<string, string> payload);

        /// <summary>
        /// Check transaction status directly with Payment Provider (e.g., polling after QR scan).
        /// </summary>
        Task<bool> CheckTransactionStatusAsync(string transactionId);
    }
}