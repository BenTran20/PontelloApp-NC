using Microsoft.EntityFrameworkCore;
using PontelloApp.Controllers;
using PontelloApp.Data;
using PontelloApp.Models;
using PontelloApp.Services;

namespace PontelloApp.Ultilities
{
    public class RecurringOrderProcessorJob
    {
        private readonly PontelloAppContext _db;
        private readonly RecurringOrderService _recurringSvc;
        private readonly RecurringEmailService _emailSvc;

        public RecurringOrderProcessorJob(
            PontelloAppContext db,
            RecurringOrderService recurringSvc, RecurringEmailService emailSvc)
        {
            _db = db;
            _recurringSvc = recurringSvc;
            _emailSvc = emailSvc;
        }

        public async Task RunAsync()
        {
            var warningLeadTime = TimeSpan.FromHours(4);
            var now = DateTime.Now;

            var due = await _db.RecurringOrders
                .Where(r => r.IsActive && r.NextRun <= now)
                .ToListAsync();

            var toWarn = await _db.RecurringOrders
                .Where(r => r.IsActive &&
                            r.NextRun > now &&
                            r.NextRun <= now.Add(warningLeadTime))
                .ToListAsync();

            // Warning email
            foreach (var r in toWarn)
            {
                var order = await _db.Orders
                    .Include(o => o.Shipping)
                    .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                    .FirstOrDefaultAsync(o => o.Id == r.OriginalOrderId);

                if (order != null)
                    await _emailSvc.ScheduleWarningEmail(order, r);
            }

            // Process order + payment email
            foreach (var r in due)
            {
                int newOrderId = 0;
                bool success = true;
                string msg = "";

                try
                {
                    newOrderId = await _recurringSvc.CreateOrderFromRecurring(r);

                    var order = await _db.Orders
                        .Include(o => o.Shipping)
                        .Include(o => o.Items)
                        .ThenInclude(i => i.Product)
                        .FirstOrDefaultAsync(o => o.Id == newOrderId);

                    if (order != null)
                        await _emailSvc.SchedulePaymentEmail(order, r);

                    msg = "OK";
                }
                catch (Exception ex)
                {
                    success = false;
                    msg = ex.Message;
                }

                _db.RecurringOrderExecutionLogs.Add(new RecurringOrderExecutionLog
                {
                    RecurringOrderId = r.Id,
                    Success = success,
                    Message = msg,
                    NewOrderId = newOrderId > 0 ? newOrderId : null
                });

                r.NextRun = CalculateNextRun(r);
                await _db.SaveChangesAsync();
            }
        }

        private DateTime CalculateNextRun(RecurringOrder r)
        {
            var nowLocal = DateTime.Now;
            DateTime nextLocal;

            if (r.Frequency == "Daily")
            {
                nextLocal = nowLocal.Date.Add(r.TimeOfDay);
                if (nextLocal <= nowLocal) nextLocal = nextLocal.AddDays(1);
            }
            else if (r.Frequency == "Weekly" && r.WeeklyDay.HasValue)
            {
                int today = (int)nowLocal.DayOfWeek;
                int target = (int)r.WeeklyDay.Value;
                int daysUntil = (target - today + 7) % 7;
                nextLocal = nowLocal.Date.AddDays(daysUntil).Add(r.TimeOfDay);
                if (nextLocal <= nowLocal) nextLocal = nextLocal.AddDays(7);
            }
            else if (r.Frequency == "Monthly" && r.MonthlyDay.HasValue)
            {
                int day = Math.Min(r.MonthlyDay.Value, DateTime.DaysInMonth(nowLocal.Year, nowLocal.Month));
                nextLocal = new DateTime(nowLocal.Year, nowLocal.Month, day).Add(r.TimeOfDay);
                if (nextLocal <= nowLocal)
                {
                    var nextMonth = nowLocal.AddMonths(1);
                    day = Math.Min(r.MonthlyDay.Value, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                    nextLocal = new DateTime(nextMonth.Year, nextMonth.Month, day).Add(r.TimeOfDay);
                }
            }
            else
            {
                nextLocal = nowLocal.AddDays(1).Date.Add(r.TimeOfDay);
            }

            return nextLocal;
        }
    }
}