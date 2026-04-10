using Microsoft.EntityFrameworkCore;
using PontelloApp.Data;
using PontelloApp.Models;
using QuestPDF.Fluent;

namespace PontelloApp.Ultilities
{
    public class RecurringEmailService
    {
        private readonly PontelloAppContext _db;

        public RecurringEmailService(PontelloAppContext db)
        {
            _db = db;
        }

        // Schedule warning email 
        public async Task ScheduleWarningEmail(Order order, RecurringOrder recurring)
        {
            if (order == null || string.IsNullOrWhiteSpace(order.Shipping?.Email)) return;

            var warningTime = recurring.NextRun.AddHours(-4);
            if (warningTime <= DateTime.Now)
                warningTime = DateTime.Now.AddMinutes(1);

            bool exists = await _db.ScheduledEmails
                .AnyAsync(e => e.RecurringOrderId == recurring.Id && e.NextSendAt == warningTime && e.PaymentTime == false);

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
                AttachmentName = $"PO_{order.PONumber}.pdf",
                NextSendAt = warningTime,
                PaymentTime = false,
                Remninder = "FirstReminder"
            };

            _db.ScheduledEmails.Add(warningEmail);
            await _db.SaveChangesAsync();
        }

        // Schedule payment email 
        public async Task SchedulePaymentEmail(Order order, RecurringOrder recurring)
        {
            if (order == null || string.IsNullOrWhiteSpace(order.Shipping?.Email)) return;

            var sendTime = recurring.NextRun;

            bool exists = await _db.ScheduledEmails
                .AnyAsync(e => e.RecurringOrderId == recurring.Id && e.NextSendAt == sendTime && e.PaymentTime == true);

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
                AttachmentName = $"PO_{order.PONumber}.pdf",
                NextSendAt = sendTime,
                PaymentTime = true,
                Remninder = "PaymentEmail"
            };

            _db.ScheduledEmails.Add(paymentEmail);
            await _db.SaveChangesAsync();
        }

        // PDF generator 
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

                    page.Content().PaddingVertical(15).Column(col =>
                    {
                        // Shipping info
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

                        // Table
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

                        // Totals
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

                    page.Footer()
                        .AlignCenter()
                        .Text($"Generated {DateTime.Now:yyyy-MM-dd HH:mm}")
                        .FontSize(10)
                        .FontColor("#777777");
                });

            }).GeneratePdf();

            return pdf;
        }
    }
}
