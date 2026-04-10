namespace PontelloApp.Models
{
    public class ScheduledEmail
    {
        public int Id { get; set; }
        public int RecurringOrderId { get; set; }
        public RecurringOrder RecurringOrder { get; set; }
        public int? OrderId { get; set; }
        public string Email { get; set; }
        public string Subject { get; set; }
        public string HtmlBody { get; set; }
        public byte[]? AttachmentBytes { get; set; }
        public string? AttachmentName { get; set; }
        public DateTime NextSendAt { get; set; }
        public string Remninder { get; set; }
        public bool PaymentTime { get; set; }
    }
}
