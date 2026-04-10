using System.ComponentModel.DataAnnotations;

namespace PontelloApp.Models
{
    public class RecurringOrder
    {
        public int Id { get; set; }

        public int OriginalOrderId { get; set; }
        public Order? OriginalOrder { get; set; }

        [Required]
        public string Frequency { get; set; } = "Daily";
        [Required]
        public TimeSpan TimeOfDay { get; set; }

        public DayOfWeek? WeeklyDay { get; set; }
        public int? MonthlyDay { get; set; }

        public DateTime NextRun { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
