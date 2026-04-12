using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PontelloApp.Custom_Controllers;
using PontelloApp.Data;
using PontelloApp.Models;
using PontelloApp.Ultilities;
using QuestPDF.Fluent;

namespace PontelloApp.Controllers
{
    public class RecurringOrderController : ElephantController
    {
        private readonly PontelloAppContext _context;
        private readonly IWebHostEnvironment _env;

        public RecurringOrderController(PontelloAppContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: RecurringOrder
        public async Task<IActionResult> Index(int orderId)
        {
            var model = await _context.RecurringOrders
                .Where(r => r.OriginalOrderId == orderId)
                .ToListAsync();

            ViewBag.OrderId = orderId;
            return View(model);
        }

        // GET: RecurringOrder/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recurringOrder = await _context.RecurringOrders
                .Include(r => r.OriginalOrder)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (recurringOrder == null)
            {
                return NotFound();
            }

            return View(recurringOrder);
        }

        // GET: RecurringOrder/Create
        public async Task<IActionResult> Create(int orderId)
        {
            var hasOrder = await _context.Orders.AnyAsync(o => o.Id == orderId);
            if (!hasOrder) return NotFound();

            var model = new RecurringOrder
            {
                OriginalOrderId = orderId,
                Frequency = "Daily",
                TimeOfDay = new TimeSpan(9, 0, 0)
            };
            return View(model);
        }

        // POST: RecurringOrder/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RecurringOrder model)
        {
            if (!ModelState.IsValid) return View(model);

            model.TimeOfDay = model.TimeOfDay.TimeOfDayToUtc();
            model.NextRun = CalculateNextRun(model);

            _context.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Recurring order created.";
            return RedirectToAction("Details", "Order", new { id = model.OriginalOrderId });
        }


        // GET: RecurringOrder/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recurringOrder = await _context.RecurringOrders.FindAsync(id);
            if (recurringOrder == null)
            {
                return NotFound();
            }
            recurringOrder.TimeOfDay = recurringOrder.TimeOfDay.TimeOfDayToEastern();

            ViewData["OriginalOrderId"] = new SelectList(_context.Orders, "Id", "Id", recurringOrder.OriginalOrderId);
            return View(recurringOrder);
        }

        // POST: RecurringOrder/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,OriginalOrderId,Frequency,TimeOfDay,WeeklyDay,MonthlyDay,NextRun,IsActive")] RecurringOrder recurringOrder)
        {
            if (id != recurringOrder.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    recurringOrder.TimeOfDay = recurringOrder.TimeOfDay.TimeOfDayToUtc();
                    recurringOrder.NextRun = CalculateNextRun(recurringOrder);

                    _context.Update(recurringOrder);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RecurringOrderExists(recurringOrder.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            //ScheduleWarningEmail(recurringOrder.OriginalOrderId, recurringOrder);
            ViewData["OriginalOrderId"] = new SelectList(_context.Orders, "Id", "Id", recurringOrder.OriginalOrderId);
            return View(recurringOrder);
        }

        // GET: RecurringOrder/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recurringOrder = await _context.RecurringOrders
                .Include(r => r.OriginalOrder)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (recurringOrder == null)
            {
                return NotFound();
            }

            return View(recurringOrder);
        }

        // POST: RecurringOrder/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var recurringOrder = await _context.RecurringOrders.FindAsync(id);
            if (recurringOrder != null)
            {
                _context.RecurringOrders.Remove(recurringOrder);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // Tracking
        public async Task<IActionResult> Tracking()
        {
            var model = await _context.RecurringOrders
                .Include(r => r.OriginalOrder)
                .ToListAsync();

            return View(model);
        }

        // Active
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id, int orderId)
        {
            var r = await _context.RecurringOrders.FindAsync(id);

            if (r == null) return NotFound();

            r.IsActive = !r.IsActive;
            await _context.SaveChangesAsync();

            return RedirectToAction("Tracking", new { orderId });
        }

        private bool RecurringOrderExists(int id)
        {
            return _context.RecurringOrders.Any(e => e.Id == id);
        }

        private DateTime CalculateNextRun(RecurringOrder r)
        {
            var now = DateTime.UtcNow;

            if (r.Frequency == "Daily")
            {
                var next = now.Date.Add(r.TimeOfDay);
                if (next <= now) next = next.AddDays(1);
                return next;
            }
            else if (r.Frequency == "Weekly" && r.WeeklyDay.HasValue)
            {
                int today = (int)now.DayOfWeek;
                int target = (int)r.WeeklyDay.Value;
                int daysUntil = (target - today + 7) % 7;
                var next = now.Date.AddDays(daysUntil).Add(r.TimeOfDay);
                if (next <= now) next = next.AddDays(7);
                return next;
            }
            else if (r.Frequency == "Monthly" && r.MonthlyDay.HasValue)
            {
                int day = Math.Min(r.MonthlyDay.Value, DateTime.DaysInMonth(now.Year, now.Month));
                var next = new DateTime(now.Year, now.Month, day).Add(r.TimeOfDay);
                if (next <= now)
                {
                    var nextMonth = now.AddMonths(1);
                    day = Math.Min(r.MonthlyDay.Value, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                    next = new DateTime(nextMonth.Year, nextMonth.Month, day).Add(r.TimeOfDay);
                }
                return next;
            }

            return now.AddDays(1);
        }

        public async Task ScheduleWarningEmail(int orderId, RecurringOrder recurring)
        {
            var order = await _context.Orders
                .Include(o => o.Shipping)
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null || string.IsNullOrWhiteSpace(order.Shipping?.Email))
                return;

            var warningTime = recurring.NextRun.AddHours(-4);

            if (DateTime.UtcNow >= recurring.NextRun)
                return;

            bool exists = _context.ScheduledEmails.Any(e =>
                e.RecurringOrderId == recurring.Id &&
                e.PaymentTime == false &&
                e.NextSendAt == warningTime
            );
            if (exists) return;


            byte[] pdfBytes = GeneratePurchaseOrderPdf(order);

            var warningEmail = new ScheduledEmail
            {
                RecurringOrderId = recurring.Id,
                OrderId = recurring.OriginalOrderId,
                Email = order.Shipping.Email,
                Subject = $"Your Pontello Order {order.PONumber}",
                HtmlBody = $@"
<p>Hi {order.Shipping.FullName},</p>
<p>Your recurring order will be processed in <strong>4 hours</strong>.</p>
<p>You can still make changes before it is processed.</p>",
                AttachmentBytes = pdfBytes,
                AttachmentName = Path.Combine(Path.GetTempPath(), $"PO_{order.PONumber}.pdf"),
                NextSendAt = warningTime,
                PaymentTime = false
            };

            _context.ScheduledEmails.Add(warningEmail);
            await _context.SaveChangesAsync();
        }

        public async Task SchedulePaymentEmail(int orderId, RecurringOrder recurring)
        {
            var order = await _context.Orders
                .Include(o => o.Shipping)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null || string.IsNullOrWhiteSpace(order.Shipping?.Email))
                return;

            var sendTime = recurring.NextRun;

            bool exists = _context.ScheduledEmails.Any(e =>
                e.RecurringOrderId == recurring.Id &&
                e.NextSendAt == sendTime
            );
            if (exists) return;

            byte[] pdfBytes = GeneratePurchaseOrderPdf(order);

            var paymentEmail = new ScheduledEmail
            {
                RecurringOrderId = recurring.Id,
                OrderId = recurring.OriginalOrderId,
                Email = order.Shipping.Email,
                Subject = $"Your Pontello Order {order.PONumber}",
                HtmlBody = $@"
                <p>Hi {order.Shipping.FullName},</p>
                <p>Your recurring order has been processed.</p>",
                AttachmentBytes = pdfBytes,
                AttachmentName = Path.Combine(Path.GetTempPath(), $"PO_{order.PONumber}.pdf"),
                NextSendAt = sendTime,
                PaymentTime = true
            };

            _context.ScheduledEmails.Add(paymentEmail);
            await _context.SaveChangesAsync();
        }

        private byte[] GeneratePurchaseOrderPdf(Order order)
        {
            var items = order.Items.Select(i => new
            {
                Product = i.Product?.ProductName ?? "",
                Quantity = i.Quantity,
                Price = i.UnitPrice,
                Total = i.Quantity * i.UnitPrice
            }).ToList();

            decimal subtotal = items.Sum(i => i.Total);
            decimal tax = order.TaxAmount;
            decimal shippingCost = order.Shipping?.ShippingCost ?? 0m;
            decimal grandTotal = order.TotalAmount;

            byte[] pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);

                    // HEADER
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Pontello").FontSize(20).Bold();
                            col.Item().Text("Purchase Order").FontSize(14);
                        });

                        row.ConstantItem(200).AlignRight().Column(col =>
                        {
                            col.Item().Text($"PO #: {order.PONumber}").Bold();
                            col.Item().Text($"Date: {order.CreatedAt:yyyy-MM-dd}");
                        });
                    });

                    // CONTENT
                    page.Content().PaddingVertical(15).Column(col =>
                    {

                        // SHIPPING INFO
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Ship To").Bold();
                                c.Item().Text(order.Shipping?.FullName ?? "");
                                c.Item().Text(order.Shipping?.FullAddress ?? "N/A");
                                c.Item().Text(order.Shipping?.Email ?? "");
                                c.Item().Text(order.Shipping?.Phone ?? "");

                                if (!string.IsNullOrWhiteSpace(order.Shipping?.BinOrEin))
                                    c.Item().Text($"BIN: {order.Shipping.BinOrEin}");

                                if (!string.IsNullOrWhiteSpace(order.Shipping?.TrackingNumber))
                                    c.Item().Text($"Tracking #: {order.Shipping.TrackingNumber}");
                            });
                        });

                        col.Item().PaddingVertical(10);

                        // TABLE
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(5);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background("#F3F4F6").Padding(6).Text("Product").Bold();
                                header.Cell().Background("#F3F4F6").Padding(6).Text("Qty").Bold();
                                header.Cell().Background("#F3F4F6").Padding(6).Text("Unit Price").Bold();
                                header.Cell().Background("#F3F4F6").Padding(6).Text("Total").Bold();
                            });

                            foreach (var i in items)
                            {
                                table.Cell().Padding(5).Text(i.Product);
                                table.Cell().Padding(5).Text(i.Quantity.ToString());
                                table.Cell().Padding(5).Text("$" + i.Price.ToString("0.00"));
                                table.Cell().Padding(5).Text("$" + i.Total.ToString("0.00"));
                            }
                        });

                        col.Item().PaddingTop(15);

                        // TOTALS
                        col.Item().AlignRight().Column(c =>
                        {
                            c.Item().Row(r =>
                            {
                                r.RelativeItem().AlignRight().Text("Subtotal:");
                                r.ConstantItem(100).AlignRight().Text("$" + subtotal.ToString("0.00"));
                            });

                            c.Item().Row(r =>
                            {
                                r.RelativeItem().AlignRight().Text("Tax:");
                                r.ConstantItem(100).AlignRight().Text("$" + tax.ToString("0.00"));
                            });

                            if (shippingCost > 0)
                            {
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().AlignRight().Text("Shipping:");
                                    r.ConstantItem(100).AlignRight().Text("$" + shippingCost.ToString("0.00"));
                                });
                            }

                            c.Item().Row(r =>
                            {
                                r.RelativeItem().AlignRight().Text("Total:").Bold();
                                r.ConstantItem(100).AlignRight().Text("$" + grandTotal.ToString("0.00")).Bold();
                            });
                        });
                    });

                    // FOOTER
                    page.Footer()
                        .AlignCenter()
                        .Text($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm}")
                        .FontSize(10)
                        .FontColor("#777777");
                });

            }).GeneratePdf();

            return pdf;
        }

    }
}
