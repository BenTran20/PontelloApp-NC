using System.ComponentModel.DataAnnotations;
using Microsoft.IdentityModel.Tokens;

namespace PontelloApp.Models
{
    public class Product : Auditable, IValidatableObject
    {
        public int ID { get; set; }

        [Display(Name = "Name")]
        [Required(ErrorMessage = "Product Name is required.")]
        [StringLength(200, ErrorMessage = "Product Name cannot be more than 200 characters long.")]
        public string ProductName { get; set; } = "";

        [Required(ErrorMessage = "Handle is required.")]
        [StringLength(255, ErrorMessage = "Handle cannot be more than 255 characters long.")]
        public string Handle { get; set; }


        [Display(Name = "Vendor")]
        [Required(ErrorMessage = "Please select a Vendor.")]
        public int VendorID { get; set; }

        public Vendor? Vendor { get; set; }


        [StringLength(100, ErrorMessage = "Type cannot be more than 100 characters long.")]
        public string? Type { get; set; }

        [StringLength(100, ErrorMessage = "Tag cannot be more than 100 characters long.")]
        public string? Tag { get; set; }

        [Required(ErrorMessage = "Product Description is required.")]
        public string Description { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }

        [Display(Name = "Is Unlisted")]
        public bool IsUnlisted { get; set; } = false;

        [Display(Name = "Is Taxable")]
        public bool IsTaxable { get; set; } = true;

        [ScaffoldColumn(false)]
        [Timestamp]
        public Byte[]? RowVersion { get; set; }//Added for concurrency

        [Required(ErrorMessage = "Please select the Category.")]
        public int CategoryID { get; set; }

        public Category? Category { get; set; }

        public ICollection<ProductVariant>? Variants { get; set; } = new HashSet<ProductVariant>();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ProductName.Length <= 2 || ProductName.IsNullOrEmpty())
            {
                yield return new ValidationResult("Product Name must be at least 3 Characters long", new[] { "ProductName" });
            }
        }

    }
}
