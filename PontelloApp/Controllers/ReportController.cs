using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PontelloApp.Custom_Controllers;
using PontelloApp.Data;
using PontelloApp.Models;
using QuestPDF.Fluent;

namespace PontelloApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ReportController : ElephantController
    {
        private readonly PontelloAppContext _context;

        public ReportController(PontelloAppContext context)
        {
            _context = context;
        }

        // MAIN DASHBOARD 
        public async Task<IActionResult> DashboardReport(DateTime? fromDate, DateTime? toDate, OrderStatus? status)
        {
            var query = _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Include(o => o.Shipping)
                .Where(o => o.Status != OrderStatus.Draft && o.Status != OrderStatus.Progress)
                .AsQueryable();

            // =========================
            // FILTER
            // =========================
            if (fromDate.HasValue)
                query = query.Where(o => o.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(o => o.CreatedAt <= toDate.Value.AddDays(1));

            if (status.HasValue)
                query = query.Where(o => o.Status == status.Value);

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            // =========================
            // KPI SUMMARY
            // =========================
            var validRevenueStatuses = new[]
            {
                OrderStatus.Submitted,
                OrderStatus.Approved,
                OrderStatus.Shipped
            };

            var totalRevenue = orders
                .Where(o => validRevenueStatuses.Contains(o.Status))
                .Sum(o => o.TotalAmount);

            var totalOrders = orders.Count;

            var avgOrderValue = orders.Any()
                ? orders.Average(o => o.TotalAmount)
                : 0;

            // =========================
            // REVENUE TREND
            // =========================
            var revenueTrends = orders
                .GroupBy(o => o.CreatedAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Date = g.Key.ToString("MM-dd"),
                    Revenue = g.Sum(x => x.TotalAmount)
                })
                .ToList();

            // =========================
            // ORDER STATUS REPORT
            // =========================
            var orderStatusReport = orders
                .GroupBy(o => o.Status)
                .Select(g => new
                {
                    Status = g.Key.ToString(),
                    Count = g.Count(),
                    Revenue = g.Sum(x => x.TotalAmount)
                })
                .ToList();

            // =========================
            // PRODUCT PERFORMANCE
            // =========================
            var topProducts = await _context.OrderItems
                .Include(oi => oi.Product)
                .Include(oi => oi.Order)
                .Where(oi =>
                    oi.Order.Status != OrderStatus.Draft &&
                    oi.Order.Status != OrderStatus.Progress &&
                    oi.Order.Status != OrderStatus.Rejected)
                .GroupBy(oi => new { oi.ProductId, oi.Product.ProductName })
                .Select(g => new
                {
                    ProductName = g.Key.ProductName,
                    QuantitySold = g.Sum(x => x.Quantity),
                    Revenue = (double)g.Sum(x => x.Quantity * x.UnitPrice)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(5)
                .ToListAsync();

            // =========================
            // CUSTOMER ANALYTICS
            // =========================

            var customerOrders = orders
                .GroupBy(o => o.Shipping.Email)
                .Select(g => new
                {
                    Email = g.Key,
                    OrderCount = g.Count(),
                    TotalRevenue = g.Sum(x => x.TotalAmount),
                    Type = g.Count() == 1 ? "New" : "Returning"
                })
                .ToList();

            var customerSummary = new
            {
                NewCustomers = customerOrders.Count(x => x.Type == "New"),
                ReturningCustomers = customerOrders.Count(x => x.Type == "Returning")
            };

            var topCustomers = customerOrders
                .OrderByDescending(x => x.TotalRevenue)
                .Take(10)
                .ToList();

            // =========================
            // 📍 LOCATION ANALYTICS
            // =========================
            var locationStats = orders
                .GroupBy(o => o.Shipping.City)
                .Select(g => new
                {
                    City = g.Key ?? "Unknown",
                    TotalOrders = g.Count(),
                    TotalRevenue = g.Sum(x => x.TotalAmount)
                })
                .OrderByDescending(x => x.TotalRevenue)
                .ToList();

            // =========================
            // VIEWDATA / VIEWBAG
            // =========================
            ViewData["FromDate"] = fromDate?.ToString("yyyy-MM-dd");
            ViewData["ToDate"] = toDate?.ToString("yyyy-MM-dd");
            ViewData["Status"] = status;
            ViewData["StatusList"] =
                Enum.GetValues(typeof(OrderStatus))
                    .Cast<OrderStatus>()
                    .Where(s => s != OrderStatus.Draft && s != OrderStatus.Progress)
                    .ToList();

            ViewData["TotalRevenue"] = totalRevenue;
            ViewData["TotalOrders"] = totalOrders;
            ViewData["AvgOrderValue"] = avgOrderValue;

            ViewBag.RevenueTrends = revenueTrends;
            ViewBag.OrderStatusReport = orderStatusReport;
            ViewBag.TopProducts = topProducts;

            ViewBag.CustomerOrders = customerOrders;
            ViewBag.CustomerSummary = customerSummary;
            ViewBag.TopCustomers = topCustomers;

            ViewBag.LocationStats = locationStats;

            return View(orders);
        }


        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> ExportSalesCsv(DateTime? fromDate, DateTime? toDate, OrderStatus? status)
        {
            var query = _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Include(o => o.Shipping)
                .Where(o => o.Status != OrderStatus.Draft && o.Status != OrderStatus.Progress)
                .AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(o => o.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(o => o.CreatedAt <= toDate.Value.AddDays(1));

            if (status.HasValue)
                query = query.Where(o => o.Status == status.Value);

            var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("PO Number,Date,Status,Customer,Total Amount,Tax,Items");

            foreach (var o in orders)
            {
                var items = string.Join(" | ",
                    o.Items?.Select(i => $"{i.Product?.ProductName} x{i.Quantity}") ?? []);

                csv.AppendLine(
                    $"\"{o.PONumber}\"," +
                    $"\"{o.CreatedAt:yyyy-MM-dd}\"," +
                    $"\"{o.Status}\"," +
                    $"\"{o.Shipping?.FullName ?? "N/A"}\"," +
                    $"\"{o.TotalAmount:0.00}\"," +
                    $"\"{o.TaxAmount:0.00}\"," +
                    $"\"{items}\""
                );
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());

            return File(bytes, "text/csv", $"SalesReport_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        public async Task<IActionResult> ExportSalesPdf(DateTime? fromDate, DateTime? toDate, OrderStatus? status)
        {
            var query = _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Include(o => o.Shipping)
                .Where(o => o.Status != OrderStatus.Draft && o.Status != OrderStatus.Progress)
                .AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(o => o.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(o => o.CreatedAt <= toDate.Value.AddDays(1));

            if (status.HasValue)
                query = query.Where(o => o.Status == status.Value);

            var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

            decimal totalRevenue = orders
                .Where(o => o.Status == OrderStatus.Shipped)
                .Sum(o => o.TotalAmount);

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(25);

                    page.Header()
                        .Text("Pontello Sales Report")
                        .FontSize(18)
                        .Bold();

                    page.Content().Column(col =>
                    {
                        col.Item().Text($"Total Orders: {orders.Count}");
                        col.Item().Text($"Total Revenue: ${totalRevenue:0.00}");
                        col.Item().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd}");

                        col.Item().PaddingVertical(10);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(90);
                                columns.ConstantColumn(80);
                                columns.ConstantColumn(80);
                                columns.RelativeColumn();
                                columns.ConstantColumn(80);
                            });

                            // header
                            table.Header(header =>
                            {
                                header.Cell().Text("PO").Bold();
                                header.Cell().Text("Date").Bold();
                                header.Cell().Text("Status").Bold();
                                header.Cell().Text("Customer").Bold();
                                header.Cell().Text("Total").Bold();
                            });

                            // rows
                            foreach (var o in orders)
                            {
                                table.Cell().Text(o.PONumber);
                                table.Cell().Text(o.CreatedAt.ToString("yyyy-MM-dd"));
                                table.Cell().Text(o.Status.ToString());
                                table.Cell().Text(o.Shipping?.FullName ?? "N/A");
                                table.Cell().Text($"${o.TotalAmount:0.00}");
                            }
                        });
                    });
                });
            }).GeneratePdf();

            return File(pdf, "application/pdf", $"SalesReport_{DateTime.UtcNow:yyyyMMdd}.pdf");
        }

        public async Task<IActionResult> PrintOrder(string poNumber)
        {
            if (string.IsNullOrWhiteSpace(poNumber))
                return RedirectToAction("DashboardReport");

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.PONumber == poNumber);

            if (order == null)
            {
                TempData["Error"] = $"Order '{poNumber}' not found.";
                return RedirectToAction("DashboardReport");
            }

            return RedirectToAction("ExportOrderPO", "Order", new { id = order.Id });
        }
    }
}
