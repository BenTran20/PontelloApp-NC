using System.ComponentModel.DataAnnotations;

namespace PontelloApp.Models
{
    public class Notification
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } 

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        public string Message { get; set; }

        [MaxLength(50)]
        public string Type { get; set; } // Info, Order, Message...

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? Link { get; set; } // optional, click redirect
    }
}
