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

            // FILTER
            if (fromDate.HasValue)
                query = query.Where(o => o.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(o => o.CreatedAt <= toDate.Value.AddDays(1));

            if (status.HasValue)
                query = query.Where(o => o.Status == status.Value);

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            // ✅ SUMMARY
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

            // REVENUE TREND (chart)
            var revenueTrends = orders
                .GroupBy(o => o.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key.ToString("MM-dd"),
                    Revenue = g.Sum(x => x.TotalAmount)
                })
                .OrderBy(x => x.Date)
                .ToList();

            // TOP PRODUCTS
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
                    Revenue = g.Sum(x => x.Quantity * x.UnitPrice)
                })
                .OrderByDescending(x => x.QuantitySold)
                .Take(5)
                .ToListAsync();

            // VIEW DATA
            ViewData["FromDate"] = fromDate?.ToString("yyyy-MM-dd");
            ViewData["ToDate"] = toDate?.ToString("yyyy-MM-dd");
            ViewData["Status"] = status;
            ViewData["StatusList"] = Enum.GetValues(typeof(OrderStatus)).Cast<OrderStatus>().ToList();

            ViewData["TotalRevenue"] = totalRevenue;
            ViewData["TotalOrders"] = totalOrders;

            ViewBag.TopProducts = topProducts;
            ViewBag.RevenueTrends = revenueTrends;

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
                var items = string.Join("; ", o.Items?.Select(i => $"{i.Product?.ProductName} x{i.Quantity}") ?? []);
                csv.AppendLine($"{o.PONumber},{o.CreatedAt:yyyy-MM-dd},{o.Status},{o.Shipping?.FullName ?? "N/A"},${o.TotalAmount:0.00},${o.TaxAmount:0.00},\"{items}\"");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"SalesReport_{DateTime.Now:yyyyMMdd}.csv");
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

            byte[] pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header().Text("Pontello — Sales Report").FontSize(20).Bold();

                    page.Content().Column(col =>
                    {
                        col.Item().Text($"Total Orders: {orders.Count}");
                        col.Item().Text($"Total Revenue: ${totalRevenue:0.00}");
                    });
                });
            }).GeneratePdf();

            return File(pdf, "application/pdf", $"SalesReport_{DateTime.Now:yyyyMMdd}.pdf");
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
