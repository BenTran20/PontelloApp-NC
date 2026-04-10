using System.ComponentModel.DataAnnotations;

namespace PontelloApp.Models
{

    public class Vendor : Auditable
    {
        [Key]
        public int VendorID { get; set; }

        [Display(Name = "Vendor Name")]
        [Required(ErrorMessage = "Vendor Name is required.")]
        [StringLength(200, ErrorMessage = "Vendor Name cannot be more than 200 characters long.")]
        public string Name { get; set; } = "";

        [Display(Name = "Contact Name")]
        [StringLength(200, ErrorMessage = "Contact Name cannot be more than 200 characters long.")]
        public string? ContactName { get; set; }

        [Display(Name = "Phone Number")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Enter a valid 10-digit phone number (digits only).")]
        [StringLength(10)]
        public string? Phone { get; set; }

        [Display(Name = "Email")]
        [StringLength(255)]
        [DataType(DataType.EmailAddress)]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string? Email { get; set; }

        public bool IsArchived { get; set; } = false;

        [Timestamp]
        public byte[]? RowVersion { get; set; }
        public ICollection<Product>? Products { get; set; } = new HashSet<Product>();
    }

}

