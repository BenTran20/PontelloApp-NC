namespace PontelloApp.Models
{
    public class RecurringOrderExecutionLog
    {
        public int Id { get; set; }
        public int RecurringOrderId { get; set; }
        public RecurringOrder? RecurringOrder { get; set; }

        public DateTime RunAt { get; set; } = DateTime.UtcNow;
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int? NewOrderId { get; set; }
    }
}