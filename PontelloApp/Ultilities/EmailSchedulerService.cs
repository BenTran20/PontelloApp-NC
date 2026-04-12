using Microsoft.EntityFrameworkCore;
using PontelloApp.Data;
using PontelloApp.Models;
using PontelloApp.Services;
using PontelloApp.Ultilities;
using QuestPDF.Fluent;

namespace PontelloApp.Utilities
{
    public class EmailSchedulerService : BackgroundService
    {
        private readonly IServiceProvider _provider;

        public EmailSchedulerService(IServiceProvider provider)
        {
            _provider = provider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PontelloAppContext>();
                var emailSender = scope.ServiceProvider.GetRequiredService<EmailSender>();
                var payment = scope.ServiceProvider.GetRequiredService<RecurringOrderService>();
                var notify = scope.ServiceProvider.GetRequiredService<RecurringOrderProcessorJob>();

                var dueEmails = db.ScheduledEmails
                    .Include(e => e.RecurringOrder)
                    .Where(e => e.NextSendAt <= DateTime.UtcNow)
                    .ToList();

                foreach (var schedule in dueEmails)
                {
                    var recurring = await db.RecurringOrders
                                .FirstOrDefaultAsync(x => x.Id == schedule.RecurringOrderId);

                    if (recurring == null || !recurring.IsActive)
                    {
                        db.ScheduledEmails.Remove(schedule);
                        continue;
                    }

                    try
                    {
                        if (schedule.PaymentTime == true)
                        {
                            int newOrderId = await payment.CreateOrderFromRecurring(recurring);

                            var order = await db.Orders
                                .Include(o => o.Shipping)
                                .Include(o => o.Items)
                                .ThenInclude(i => i.Product)
                                .FirstOrDefaultAsync(o => o.Id == newOrderId);

                            byte[] pdfBytes = GeneratePurchaseOrderPdf(order);
                            var tempPath = Path.Combine(Path.GetTempPath(), $"PO_{order.PONumber}.pdf");
                            System.IO.File.WriteAllBytes(tempPath, pdfBytes);

                            await emailSender.SendEmailWithAttachmentAsync(
                                schedule.Email,
                                $"Your Pontello Order {order.PONumber}",
                                schedule.HtmlBody,
                                tempPath
                            );

                            if (File.Exists(tempPath))
                                File.Delete(tempPath);

                            recurring.IsActive = false;
                            db.ScheduledEmails.Remove(schedule);
                        }

                        if (schedule.PaymentTime == false)
                        {
                            await emailSender.SendEmailAsync(
                                schedule.Email,
                                schedule.Subject,
                                schedule.HtmlBody
                            );

                            db.ScheduledEmails.Remove(schedule);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to send email {schedule.Id}: {ex.Message}");
                    }
                }

                await db.SaveChangesAsync();
                await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
            }
        }

        private byte[] GeneratePurchaseOrderPdf(Order order)
        {

            var items = (order.Items ?? new List<OrderItem>()).Select(i => new
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
