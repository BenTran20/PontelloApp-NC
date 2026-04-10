using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PontelloApp.Models
{
    public class Order : Auditable
    {
        public int Id { get; set; }

        public string PONumber { get; set; } = "";

        public int RevisionNumber { get; set; } = 0;

        public int? DealerId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; } = 0;

        [Required]
        public OrderStatus Status { get; set; } = OrderStatus.Draft;
        public bool IsRecurringGenerated { get; set; } = false;

        public ICollection<OrderItem>? Items { get; set; } = new HashSet<OrderItem>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // make ShippingId nullable so Order can exist without shipping
        public Shipping? Shipping { get; set; }

        public string? RejectReason { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        public string UserId { get; set; }
    }
}
