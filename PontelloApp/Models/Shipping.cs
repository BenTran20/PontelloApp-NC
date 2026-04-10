using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PontelloApp.Models
{
    public class Shipping
    {
        public int ID { get; set; }

        public string? FullAddress
        {
            get
            {
                if (string.IsNullOrWhiteSpace(StreetAddress) &&
                    string.IsNullOrWhiteSpace(City) &&
                    string.IsNullOrWhiteSpace(Province) &&
                    string.IsNullOrWhiteSpace(PostalCode))
                    return "";

                return $"{StreetAddress}, {City}, {Province} {PostalCode}";
            }
        }

        [Required(ErrorMessage = "Full Name is required.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Company Name")]
        public string? CompanyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^[2-9]\d{2}[2-9]\d{6}$", ErrorMessage = "Enter a valid 10-digit phone number.")]
        [DataType(DataType.PhoneNumber)]
        [StringLength(10)]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; } = string.Empty;

        // Optional BIN or EIN. If provided, customer will be tax-exempt for the order.
        [Display(Name = "BIN / EIN")]
        public string? BinOrEin { get; set; }

        public string? TrackingNumber { get; set; }

        [Display(Name = "Shipping Cost")]
        public decimal? ShippingCost { get; set; }

        // Optional delivery notes
        [Display(Name = "Delivery Instructions")]
        public string? DeliveryNotes { get; set; }

        //Address
        [Required(ErrorMessage = "Line Address is required.")]
        [Display(Name = "Line Address 1")]
        public string StreetAddress { get; set; }

        //Address
        [Display(Name = "Line Address 2")]
        public string? StreetAddress2 { get; set; } = string.Empty;

        [Required(ErrorMessage = "City is required.")]
        public string City { get; set; }

        [Required(ErrorMessage = "Province is required.")]
        public string Province { get; set; }

        [Display(Name = "Postal Code")]
        [RegularExpression(@"^[A-Za-z]\d[A-Za-z][ -]?\d[A-Za-z]\d$",
            ErrorMessage = "Invalid Canadian postal code (e.g. L2S 3A1)")]
        [Required(ErrorMessage = "Postal Code is required.")]
        public string PostalCode { get; set; }

        [Required(ErrorMessage = "Country is required.")]
        public string Country { get; set; } = "Canada";


        // navigation
        public int OrderId { get; set; }
        public Order? Order { get; set; }
    }
}
