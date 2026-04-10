using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PontelloApp.Custom_Controllers;
using PontelloApp.Data;
using PontelloApp.Models;
using PontelloApp.Ultilities;
using PontelloApp.Utilities;

namespace PontelloApp.Controllers
{
    [Authorize(Roles = "Dealer")]
    public class ProductDealerController : ElephantController
    {
        private readonly PontelloAppContext _context;
        private readonly OrderService _orderService;
        private readonly UserManager<User> _userManager;

        public ProductDealerController(PontelloAppContext context, OrderService orderService, UserManager<User> userManager)
        {
            _context = context;
            _orderService = orderService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? SearchString, int? CategoryID,
                             int? page, int? pageSizeID, string? actionButton, string sortDirection = "asc", string sortField = "A-Z")
        {
            string[] sortOptions = new[] { "A-Z", "Z-A" };

            int numberFilters = 0;
            PopulateDropDownLists();
            var products = _context.Products
                .Include(p => p.Variants)
                    .ThenInclude(v => v.Options)
                .Include(p => p.Category)
                .Include(p => p.Vendor)
                .Where(p => p.IsActive && !p.IsUnlisted)
                .AsNoTracking();

            // Filter by search
            if (!string.IsNullOrEmpty(SearchString))
                products = products.Where(p => p.ProductName.ToUpper().Contains(SearchString.ToUpper())
                 || p.Handle.ToUpper().Contains(SearchString.ToUpper()));

            // Filter by category
            if (CategoryID.HasValue)
            {
                products = products.Where(p => p.CategoryID == CategoryID);
                numberFilters++;
            }

            if (numberFilters != 0)
                ViewData["numberFilters"] = $"({numberFilters})";

            if (!string.IsNullOrEmpty(actionButton) && sortOptions.Contains(actionButton))
            {
                sortField = actionButton;
                page = 1;
            }

            products = sortField switch
            {
                "A-Z" => products.OrderBy(p => p.ProductName),
                "Z-A" => products.OrderByDescending(p => p.ProductName),
                _ => products.OrderBy(p => p.ProductName),
            };

            ViewData["sortField"] = sortField;
            ViewData["sortDirection"] = sortDirection;

            int pageSize = PageSizeHelper.SetPageSize(HttpContext, pageSizeID, ControllerName());
            ViewData["pageSizeID"] = PageSizeHelper.PageSizeList(pageSize);

            ViewData["TotalItems"] = await products.CountAsync();

            var pagedData = await PaginatedList<Product>.CreateAsync(products.AsNoTracking(), page ?? 1, pageSize);

            return View(pagedData);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Variants)
                    .ThenInclude(v => v.Options)
                .Include(p => p.Category)
                .Include(p => p.Vendor)
                .FirstOrDefaultAsync(p => p.ID == id && p.IsActive && !p.IsUnlisted);

            if (product == null) return NotFound();

            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int variantId, int quantity)
        {
            // Basic server-side validation
            if (quantity <= 0)
            {
                TempData["ErrorMessage"] = "Quantity must be at least 1.";
                return RedirectToAction("Details", new { id = productId });
            }

            var variant = await _context.ProductVariants
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == variantId);

            if (variant == null)
            {
                TempData["ErrorMessage"] = "Selected product variant not found.";
                return RedirectToAction("Details", new { id = productId });
            }

            // Treat null InventoryPolicy as Deny for safety
            var policy = variant.InventoryPolicy ?? InventoryPolicy.Deny;

            // If policy denies and stock is zero -> cannot order
            if (policy == InventoryPolicy.Deny && variant.StockQuantity <= 0)
            {
                TempData["ErrorMessage"] = "Selected variant is out of stock.";
                return RedirectToAction("Details", new { id = productId });
            }

            // If policy denies, ensure requested quantity does not exceed stock
            if (policy == InventoryPolicy.Deny && variant.StockQuantity < quantity)
            {
                TempData["ErrorMessage"] = $"Requested quantity ({quantity}) exceeds available stock ({variant.StockQuantity}).";
                return RedirectToAction("Details", new { id = productId });
            }

            // If policy is Continue (special order), allow ordering regardless of stock
            var userId = _userManager.GetUserId(User);

            await _orderService.AddToCartAsync(userId, productId, variantId, quantity);

            TempData["SuccessMessage"] = "Product added to cart successfully!";

            return RedirectToAction("Cart", "Cart", new { id = productId });
        }

        private void PopulateDropDownLists(Product? product = null)
        {
            var rootCategories = _context.Categories
                .Where(c => c.ParentCategoryID == null)
                .Include(c => c.SubCategories)
                    .ThenInclude(sc1 => sc1.SubCategories)
                        .ThenInclude(sc2 => sc2.SubCategories)
                            .ThenInclude(sc3 => sc3.SubCategories)
                                .ThenInclude(sc4 => sc4.SubCategories)
                                    .ThenInclude(sc5 => sc5.SubCategories)
                                        .ThenInclude(sc6 => sc6.SubCategories)
                .ToList();

            ViewData["CategoryID"] =
                BuildCategorySelectList(rootCategories, product?.CategoryID);
        }

        private List<SelectListItem> BuildCategorySelectList(IEnumerable<Category> categories,
            int? selectedId, int level = 0)
        {
            var items = new List<SelectListItem>();

            foreach (var category in categories)
            {
                items.Add(new SelectListItem
                {
                    Value = category.ID.ToString(),
                    Text = $"{new string('-', level * 2)} {category.Name}",
                    Selected = category.ID == selectedId
                });

                if (category.SubCategories.Any())
                {
                    items.AddRange(
                        BuildCategorySelectList(category.SubCategories, selectedId, level + 1)
                    );
                }
            }

            return items;
        }

        private void LoadCategoryParents(Category? category)
        {
            while (category != null && category.ParentCategoryID != null)
            {
                category.ParentCategory = _context.Categories
                    .FirstOrDefault(c => c.ID == category.ParentCategoryID);
                category = category.ParentCategory;
            }
        }
    }
}
