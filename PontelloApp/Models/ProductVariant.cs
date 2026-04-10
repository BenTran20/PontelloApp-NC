using PontelloApp.Ultilities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static PontelloApp.Models.InventoryPolicy;

namespace PontelloApp.Models
{
    public class ProductVariant : Auditable, IValidatableObject
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Unit price is required.")]
        [Display(Name = "Unit Price")]
        [Column(TypeName = "decimal(18,2)")]
        [DataType(DataType.Currency)]
        [DisplayFormat(DataFormatString = "{0:F2}", ApplyFormatInEditMode = true)]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Cost Price")]
        [Column(TypeName = "decimal(18,2)")]
        [DataType(DataType.Currency)]
        [DisplayFormat(DataFormatString = "{0:F2}", ApplyFormatInEditMode = true)]
        public decimal? CostPrice { get; set; }

        [Display(Name = "Stock Quantity")]
        [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative.")]
        public int? StockQuantity { get; set; }

        [StringLength(50, ErrorMessage = "SKU_ExternalID cannot be more than 50 characters long.")]
        [Display(Name = "SKU")]
        public string? SKU_ExternalID { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CompareAtPrice { get; set; }

        [Column(TypeName = "decimal(17,7)")]
        public decimal? Weight { get; set; }

        public ImperialUnits Unit { get; set; }

        [StringLength(50, ErrorMessage = "Barcode cannot be more than 50 characters long.")]
        public string? Barcode { get; set; }

        [Display(Name = "Inventory Policy")]
        public InventoryPolicy? InventoryPolicy { get; set; }

        public bool Status { get; set; }

        [Required(ErrorMessage = "Must add the Options")]
        public List<Variant> Options { get; set; } = new List<Variant>();

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        [ScaffoldColumn(false)]
        [Timestamp]
        public Byte[]? RowVersion { get; set; }//Added for concurrency

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (UnitPrice <= 0)
            {
                yield return new ValidationResult("Unit Price cannot be less than $0", new[] { "UnitPrice" });
            }

            if (CostPrice.HasValue && CostPrice.Value < 0)
            {
                yield return new ValidationResult("Cost Price cannot be negative", new[] { "CostPrice" });
            }
        }
    }
}


