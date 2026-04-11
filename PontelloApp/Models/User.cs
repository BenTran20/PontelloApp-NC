using Microsoft.AspNetCore.Identity;
using Org.BouncyCastle.Bcpg;

namespace PontelloApp.Models
{
    public class User : IdentityUser
    {
        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string PhoneNumber { get; set; }
        public string? BINorEIN { get; set; }

        public AccountStatus Status { get; set; } = AccountStatus.Pending;

        public ICollection<Order>? Orders { get; set; } = new HashSet<Order>();

        public string? CompanyName { get; set; }
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }

    }
}
