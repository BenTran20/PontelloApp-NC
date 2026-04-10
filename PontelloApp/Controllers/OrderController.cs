using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PontelloApp.Custom_Controllers;
using PontelloApp.Data;
using PontelloApp.Models;
using PontelloApp.Ultilities;
using PontelloApp.Utilities;
using QuestPDF.Fluent;
using System.IO;

namespace PontelloApp.Controllers
{
    public class OrderController : ElephantController
    {
        private readonly PontelloAppContext _context;
        private readonly EmailSender _emailSender;
        private readonly UserManager<User> _userManager;
        private readonly IHubContext<NotificationHub> _hubContext;

        public OrderController(PontelloAppContext context, EmailSender emailSender, UserManager<User> userManager, IHubContext<NotificationHub> hubContext  )
        {
            _context = context;
            _emailSender = emailSender;
            _userManager = userManager;
            _hubContext = hubContext;
        }

        // GET: /Order
        [Authorize(Roles = "Dealer")]
        public async Task<IActionResult> Index(string? SearchString, int? OrderStatusID, OrderStatus? Status, DateTime? FromDate, DateTime? ToDate, int? page, int? pageSizeID, string? actionButton)
        {
            var user = await _userManager.GetUserAsync(User);

            var orders = _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Include(o => o.Shipping)
                .Where(o => o.UserId == user.Id && o.Status!=OrderStatus.Draft)
                .OrderByDescending(o => o.CreatedAt)
                .AsNoTracking();

            ViewData["Filtering"] = "btn-outline-secondary";
            int numberFilters = 0;

            ViewData["OrderStatusID"] = OrderStatusSelectList(Status);

            if (!String.IsNullOrEmpty(SearchString))
            {
                orders = orders.Where(o => o.PONumber.ToUpper().Contains(SearchString.ToUpper()));

                numberFilters++;
            }

            if (FromDate.HasValue)
            {
                orders = orders.Where(o => o.CreatedAt >= FromDate);
                numberFilters++;
            }

            if (ToDate.HasValue)
            {
                orders = orders.Where(o => o.CreatedAt <= ToDate);
                numberFilters++;
            }

            if (Status.HasValue)
            {
                orders = orders.Where(o => o.Status == Status.Value);
            }

            if (numberFilters != 0)
            {
                ViewData["numberFilters"] = "(" + numberFilters.ToString() + ")";
                ViewData["ShowFilter"] = "show";
            }

            int pageSize = PageSizeHelper.SetPageSize(HttpContext, pageSizeID, "Order");
            ViewData["pageSizeID"] = PageSizeHelper.PageSizeList(pageSize);

            int totalOrders = orders.Count();
            ViewData["TotalOrders"] = totalOrders;

            var pagedData = await PaginatedList<Order>.CreateAsync(orders, page ?? 1, pageSize);

            return View(await orders.ToListAsync());


        }

        [Authorize(Roles = "Admin")]
        // GET: Admin management view for orders
        public async Task<IActionResult> Admin()
        {
            var orders = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .Include(o => o.Shipping)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var pendingSchedules = await _context.RecurringOrders
                .Include(r => r.OriginalOrder)
                .Where(r => r.IsActive && r.NextRun > DateTime.Now)
                .ToListAsync();

            ViewBag.PendingSchedules = pendingSchedules;

            // Revenue calculation 
            var deliveredOrders = orders.Where(o => o.Status == OrderStatus.Shipped).ToList();
            ViewBag.TotalRevenue = deliveredOrders.Sum(o => o.TotalAmount);
            ViewBag.TodayRevenue = deliveredOrders
                .Where(o => o.CreatedAt.Date == DateTime.Today)
                .Sum(o => o.TotalAmount);
            ViewBag.MonthRevenue = deliveredOrders
                .Where(o => o.CreatedAt.Month == DateTime.Now.Month && o.CreatedAt.Year == DateTime.Now.Year)
                .Sum(o => o.TotalAmount);

            return View(orders);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Analytics(DateTime? fromDate, DateTime? toDate)
        {
            var ordersQuery = _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .Where(o => o.Status != OrderStatus.Draft && o.Status!=OrderStatus.Progress && o.Status!=OrderStatus.Rejected)
                .AsQueryable();

            // FILTER by date
            if (fromDate.HasValue)
                ordersQuery = ordersQuery.Where(o => o.CreatedAt >= fromDate);

            if (toDate.HasValue)
                ordersQuery = ordersQuery.Where(o => o.CreatedAt <= toDate);

            var orders = await ordersQuery.ToListAsync();

            // Revenue trends
            var revenueTrends = orders
                .GroupBy(o => o.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key.ToString("MM-dd"),
                    Revenue = g.Sum(x => x.TotalAmount)
                })
                .OrderBy(x => x.Date)
                .ToList();

            // Top products
            var topProducts = await _context.OrderItems
                .Include(oi => oi.Product)
                .Include(oi => oi.Order)
                .Where(oi => oi.Order.Status != OrderStatus.Draft && oi.Order.Status != OrderStatus.Progress && oi.Order.Status != OrderStatus.Rejected)
                .GroupBy(oi => new { oi.ProductId, oi.Product.ProductName })
                .Select(g => new
                {
                    ProductName = g.Key.ProductName,
                    QuantitySold = g.Sum(x => x.Quantity),
                    Revenue = g.Sum(x => x.Quantity * x.UnitPrice)
                })
                .OrderByDescending(x => x.QuantitySold)
                .Take(5)
                .ToListAsync();

            // Revenue 
            var revenueByMonth = orders
                .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
                .Select(g => new
                {
                    Month = $"{g.Key.Month}/{g.Key.Year}",
                    Revenue = g.Sum(x => x.TotalAmount)
                })
                .OrderBy(x => x.Month)
                .ToList();

            ViewBag.TopProducts = topProducts;
            ViewBag.RevenueTrends = revenueTrends;

            ViewData["FromDate"] = fromDate?.ToString("yyyy-MM-dd");
            ViewData["ToDate"] = toDate?.ToString("yyyy-MM-dd");

            return View();
        }

        // GET: /Order/Details/5
        [Authorize(Roles = "Dealer, Admin")]
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            var order = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Include(o => o.Items)
                    .ThenInclude(i => i.ProductVariant)
                .Include(o => o.Shipping)
                .FirstOrDefaultAsync(o => o.Id == id &&
                    (User.IsInRole("Admin") || o.UserId == user.Id));

            if (order == null) return NotFound();

            return View(order);
        }

        // GET: /Order/Review/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Review(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Include(o => o.Items)
                    .ThenInclude(i => i.ProductVariant)
                    .ThenInclude(i => i.Options)
                .Include(o => o.Shipping)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            return View(order);
        }

        [Authorize(Roles = "Admin")]
        private async Task<Order?> GetOrder(int id)
        {
            return await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Include(o => o.Items)
                    .ThenInclude(i => i.ProductVariant)
                    .ThenInclude(v => v.Options)
                .Include(o => o.Shipping)
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Progress(int id)
        {
            var order = await GetOrder(id);
            if (order == null) return NotFound();
            return View(order);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approved(int id)
        {
            var order = await GetOrder(id);
            if (order == null) return NotFound();
            return View(order);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Rejected(int id)
        {
            var order = await GetOrder(id);
            if (order == null) return NotFound();
            return View(order);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Shipped(int id)
        {
            var order = await GetOrder(id);
            if (order == null) return NotFound();
            return View(order);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Recurring(int id)
        {
            var order = await GetOrder(id);
            if (order == null) return NotFound();
            return View(order);
        }

        [Authorize(Roles = "Dealer, Admin")]
        public IActionResult ExportOrderPO(int id)
        {
            var order = _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.Shipping)
                .FirstOrDefault(o => o.Id == id);

            if (order == null)
                return NotFound();

            var items = order.Items.Select(i => new
            {
                Product = i.Product.ProductName,
                Quantity = i.Quantity,
                Price = i.UnitPrice,
                Total = i.Quantity * i.UnitPrice
            }).ToList();

            decimal subtotal = items.Sum(i => i.Total);
            decimal tax = order.TaxAmount;
            decimal shippingCost = order.Shipping?.ShippingCost ?? 0m;
            decimal grandTotal = order.TotalAmount; // order.TotalAmount should include shipping if saved

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
                                r.ConstantItem(120).AlignRight().Text("$" + subtotal.ToString("0.00"));
                            });

                            c.Item().Row(r =>
                            {
                                r.RelativeItem().AlignRight().Text("Tax (13%):");
                                r.ConstantItem(120).AlignRight().Text("$" + tax.ToString("0.00"));
                            });

                            if (shippingCost > 0)
                            {
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().AlignRight().Text("Shipping (incl. tax):");
                                    r.ConstantItem(120).AlignRight().Text("$" + shippingCost.ToString("0.00"));
                                });
                            }

                            c.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor("#CCCCCC");

                            c.Item().Row(r =>
                            {
                                r.RelativeItem().AlignRight().Text("Total:").Bold();
                                r.ConstantItem(120).AlignRight().Text("$" + grandTotal.ToString("0.00")).Bold();
                            });
                        });
                    });

                    // FOOTER
                    page.Footer()
                        .AlignCenter()
                        .Text($"Generated {DateTime.Now:yyyy-MM-dd HH:mm}")
                        .FontSize(10)
                        .FontColor("#777777");
                });

            }).GeneratePdf();

            return File(pdf, "application/pdf", $"Purchase Order .pdf");
        }

        private SelectList OrderStatusSelectList(OrderStatus? selectedStatus)
        {
            var statusList = Enum.GetValues(typeof(OrderStatus))
                                 .Cast<OrderStatus>()
                                 .Select(s => new
                                 {
                                     Value = s,
                                     Text = s.ToString()
                                 });

            return new SelectList(statusList, "Value", "Text", selectedStatus);
        }

        public async Task<IActionResult> Decision(int id, string status)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Include(o => o.Items)
                    .ThenInclude(i => i.ProductVariant)
                .Include(o => o.Shipping)
                .FirstOrDefaultAsync(o => o.Id == id &&
                    (o.Status == OrderStatus.Submitted || o.Status == OrderStatus.Approved));

            if (order == null || order.Items == null || !order.Items.Any())
                return RedirectToAction("Action", "Order");

            // generate PO, keep status as Draft until shipping is provided
            order.PONumber = $"PO-{DateTime.Now:yyyyMMddHHmmss}";

            order.CreatedAt = DateTime.Now;


            if (status == "Approved")
            {
                order.Status = OrderStatus.Approved;

                // Save notification
                var notification = new Notification
                {
                    UserId = order.UserId, // dealer
                    Title = "Order Approved",
                    Message = $"Your order {order.PONumber} has been approved by Admin.",
                    Type = "Order",
                    Link = $"/Order/Details/{order.Id}"
                };

                _context.Add(notification);
                await _context.SaveChangesAsync();

                // Send realtime
                await _hubContext.Clients.User(order.UserId).SendAsync("ReceiveNotification", new
                {
                    Title = notification.Title,
                    Message = notification.Message,
                    CreatedAt = notification.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                });
            }

            // persist the created order (with its items and shipping placeholder)
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Order {order.PONumber} has been approved!";
            if (order.Status == OrderStatus.Approved)
                TempData["Status"] = "Status: Approved";

            return RedirectToAction("Admin", "Order");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> ShipOrder(int id, string TrackingNumber, decimal ShippingCost)
        {
            var order = await _context.Orders
                .Include(o => o.Shipping)
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound();

            if (order.Shipping == null)
                order.Shipping = new Shipping();

            order.Shipping.TrackingNumber = TrackingNumber;
            order.Shipping.ShippingCost = ShippingCost;
            order.TotalAmount += ShippingCost; // Add shipping cost to total

            order.Status = OrderStatus.Shipped;

            // Recalculate totals including shipping
            var subtotal = order.Items?.Sum(i => i.TotalPrice) ?? 0m;

            var tax = order.TaxAmount;

            bool isTaxExempt = !string.IsNullOrWhiteSpace(order.Shipping?.BinOrEin);
            decimal shippingWithTax = isTaxExempt
                ? Math.Round(ShippingCost, 2)
                : Math.Round(ShippingCost * 1.13m, 2); //shipping logic(13% tax)

            order.Shipping.ShippingCost = shippingWithTax;
            order.TotalAmount = Math.Round(subtotal + tax + shippingWithTax, 2);

            await _context.SaveChangesAsync();

            // Generate PO PDF bytes
            byte[] pdfBytes = GeneratePurchaseOrderPdf(order);

            if (!string.IsNullOrWhiteSpace(order.Shipping?.Email))
            {
                string subject = $"Your Pontello Order {order.PONumber}";
                string body = $@"
                            <div style=""font-family: Arial, sans-serif; font-size: 14px; color: #333; text-align: left;"">

                            <p>Hi <strong>{order.Shipping.FullName}</strong>,</p>

                            <p>Thank you for your order! We're excited to let you know that your purchase has been received and is being processed.</p>

                            <p>You can find your Purchase Order attached for your reference.</p>

                            <hr style=""border:none; border-top:1px solid #eee; margin:20px 0;"" />

                            <p style=""font-size:12px; color:#777;"">
                                Pontello Team<br/>
                                Questions? Reply to this email 
                            </p>
                        </div>";

                // Save pdf temporarily
                string tempPath = Path.Combine(Path.GetTempPath(), $"PO_{order.PONumber}.pdf");
                try
                {
                    await System.IO.File.WriteAllBytesAsync(tempPath, pdfBytes);

                    await _emailSender.SendEmailWithAttachmentAsync(order.Shipping.Email, subject, body, tempPath);
                }
                finally
                {
                    // optional: delete temp file after sending
                    try { System.IO.File.Delete(tempPath); } catch { /* swallow */ }
                }

                TempData["Success"] = $"Order {order.PONumber} has been shipped!";
                if (order.Status == OrderStatus.Shipped)
                {
                    TempData["Status"] = "Status: Shipped ";
                }
            }

            var notification = new Notification
            {
                UserId = order.UserId, // dealer
                Title = "Order Shipped",
                Message = $"Your order {order.PONumber} has been shipped. Tracking #: {TrackingNumber}.",
                Type = "Order",
                Link = $"/Order/Details/{order.Id}"
            };
            _context.Add(notification);
            await _context.SaveChangesAsync();

            await _hubContext.Clients.User(order.UserId).SendAsync("ReceiveNotification", new
            {
                Title = notification.Title,
                Message = notification.Message,
                CreatedAt = notification.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            });

            return RedirectToAction("Admin");
        }


        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RejectOrder(int id, string reason)
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound();

            order.Status = OrderStatus.Rejected;
            order.RejectReason = reason;

            var notification = new Notification
            {
                UserId = order.UserId,
                Title = "Order Rejected",
                Message = $"Your order {order.PONumber} was rejected. Reason: {reason}.",
                Type = "Order",
                Link = $"/Order/Details/{order.Id}"
            };
            _context.Add(notification);
            await _context.SaveChangesAsync();

            await _hubContext.Clients.User(order.UserId).SendAsync("ReceiveNotification", new
            {
                Title = notification.Title,
                Message = notification.Message,
                CreatedAt = notification.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Order {order.PONumber} has been rejected.";
            if (order.Status == OrderStatus.Rejected)
            {
                TempData["Status"] = "Status: Rejected";
            }

            return RedirectToAction("Admin");
        }

        // Helper to generate PO PDF bytes (extracted from ExportOrderPO)
        [Authorize(Roles = "Admin")]
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
                        .Text($"Generated {DateTime.Now:yyyy-MM-dd HH:mm}")
                        .FontSize(10)
                        .FontColor("#777777");
                });

            }).GeneratePdf();

            return pdf;
        }
        //Reordering button
                [HttpPost]
                [Authorize(Roles = "Dealer")]
                [ValidateAntiForgeryToken]
                public async Task<IActionResult> Reorder(int id)
                {
                    var user = await _userManager.GetUserAsync(User);
                    if (user == null)
                        return Challenge();
                
                    var previousOrder = await _context.Orders
                        .Include(o => o.Items)
                            .ThenInclude(i => i.Product)
                        .Include(o => o.Items)
                            .ThenInclude(i => i.ProductVariant)
                        .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);
                
                    if (previousOrder == null)
                        return NotFound();
                
                    var draftOrder = await _context.Orders
                        .Include(o => o.Items)
                        .FirstOrDefaultAsync(o => o.UserId == user.Id && o.Status == OrderStatus.Draft);
                
                    if (draftOrder == null)
                    {
                        draftOrder = new Order
                        {
                            UserId = user.Id,
                            Status = OrderStatus.Draft,
                            CreatedAt = DateTime.Now,
                            Items = new List<OrderItem>()
                        };
                
                        _context.Orders.Add(draftOrder);
                    }
                
                    int addedCount = 0;
                    int skippedCount = 0;
                
                    foreach (var oldItem in previousOrder.Items)
                    {
                        if (oldItem.ProductVariantId == null)
                        {
                            skippedCount++;
                            continue;
                        }
                
                        var variant = await _context.ProductVariants
                            .Include(v => v.Product)
                            .FirstOrDefaultAsync(v => v.Id == oldItem.ProductVariantId.Value);
                
                        if (variant == null || variant.Product == null || !variant.Product.IsActive)
                        {
                            skippedCount++;
                            continue;
                        }
                
                        var policy = variant.InventoryPolicy ?? InventoryPolicy.Deny;
                        int quantityToAdd = oldItem.Quantity;
                
                        if (policy == InventoryPolicy.Deny)
                        {
                            if (variant.StockQuantity <= 0)
                            {
                                skippedCount++;
                                continue;
                            }
                
                                    quantityToAdd = Math.Min(quantityToAdd, variant.StockQuantity ?? 0);
                                }
                
                        var existingItem = draftOrder.Items
                            .FirstOrDefault(i => i.ProductVariantId == variant.Id);
                
                        if (existingItem != null)
                        {
                            if (policy == InventoryPolicy.Deny)
                            {
                                        existingItem.Quantity = existingItem.Quantity + quantityToAdd;
                                    }
                            else
                            {
                                        existingItem.Quantity = existingItem.Quantity + quantityToAdd;
                                    }
                
                            existingItem.UnitPrice = variant.UnitPrice;
                        }
                        else
                        {
                            draftOrder.Items.Add(new OrderItem
                            {
                                ProductId = variant.ProductId,
                                ProductVariantId = variant.Id,
                                Quantity = quantityToAdd,
                                UnitPrice = variant.UnitPrice
                            });
                        }
                
                        addedCount++;
                    }


                
                            await _context.SaveChangesAsync();
                
                    if (addedCount == 0)
                    {
                        TempData["ErrorMessage"] = "No items could be re-ordered from this order.";
                        return RedirectToAction(nameof(Index));
                    }
                
                    TempData["SuccessMessage"] = skippedCount > 0
                        ? $"Re-order added to cart. {skippedCount} item(s) were skipped because they are unavailable."
                        : "Re-order added to cart successfully.";
                
                    return RedirectToAction("Cart", "Cart");
                }
    }
}






