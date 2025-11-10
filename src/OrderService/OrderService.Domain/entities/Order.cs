using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderService.Domain.Entities
{
    [Table("Orders")]
    public class Order
    {
        [Key]
        public int Id { get; set; }

        public Guid CustomerId { get; set; }

        [Required]
        public int BookstoreId { get; set; }

        [MaxLength(50)]
        [Required]
        public string OrderNumber { get; set; }


        // Customer infor & delivery infor
        [Required]
        [Phone]
        public string? CustomerPhoneNumber { get; set; }

        [Required]
        [MaxLength(255)]
        public string DeliveryAddress { get; set; }

        public DateTime? DeliveriedDate { get; set; }


        // Order
        [Required]
        public OrderStatus OrderStatus { get; set; }

        [Required]
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;



        // Payment
        public PaymentMethod? PaymentMethod { get; set; }

        [Required]
        public PaymentStatus PaymentStatus { get; set; }

        public PaymentProvider? PaymentProvider { get; set; }



        // Price & amount
        [Required]
        public int TotalQuantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }



        // Manage
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;


        public Guid? PaymentTransactionId { get; set; }

        // Navigation property
        // [ForeignKey(nameof(PaymentTransactionId))]
        public PaymentTransaction? PaymentTransaction { get; set; }

        // Navigation property
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }

    public enum OrderStatus
    {
        Created = 1,
        Confirmed = 2,
        Canceled = 3,
        Delivering = 4,
        Delivered = 5,
        Received = 6,
        Returning = 7,
        Returned = 8
    }

    public enum PaymentStatus
    {
        Pending = 1,
        Unpaid = 2,
        Paid = 3,
        Failed = 4,
        Refunded = 5
    }

    public enum PaymentMethod
    {
        COD = 1,
        Online = 2
}

    public enum PaymentProvider
    {
        None = 0,
        VNPay = 1
    }
}
