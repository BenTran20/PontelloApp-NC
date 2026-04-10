using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PontelloApp.Custom_Controllers;
using PontelloApp.Data;
using PontelloApp.Models;
using PontelloApp.Ultilities;
using QuestPDF.Fluent;
using System.Linq;
using System.Threading.Tasks;

namespace PontelloApp.Controllers
{
    public class ShippingController : ElephantController
    {
        private readonly PontelloAppContext _context;
        private readonly UserManager<User> _userManager;

        public ShippingController(PontelloAppContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Shipping/Create?orderId=123
        public async Task<IActionResult> Create(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Include(o => o.Shipping)
                .FirstOrDefaultAsync(o => o.Id == orderId && (o.Status == OrderStatus.Draft
                || o.Status == OrderStatus.Progress || o.Status == OrderStatus.Submitted));

            if (order == null)
                return RedirectToAction("Cart", "Cart");

            var user = await _userManager.GetUserAsync(User);

            order.Shipping = new Shipping
                {
                    FullName = $"{user?.FirstName} {user?.LastName}",
                    CompanyName = user?.CompanyName,
                    Email = user?.Email,
                    Phone = user?.PhoneNumber,
                    StreetAddress = user?.AddressLine1,
                    StreetAddress2 = user?.AddressLine2,
                    City = user?.City,
                    Province = user?.Province,
                    PostalCode = user?.PostalCode,
                    Country = user?.Country,
                    BinOrEin = user?.BINorEIN
                };

            return View(order);
        }

        // POST: Shipping/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int orderId, [Bind("FullName,CompanyName,Email,Phone,StreetAddress,City,Province,PostalCode,Country,DeliveryNotes")] Shipping shipping)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Include(o => o.Shipping)
                .FirstOrDefaultAsync(o => o.Id == orderId && (o.Status == OrderStatus.Draft ||
                   o.Status == OrderStatus.Progress || o.Status == OrderStatus.Submitted));

            if (order == null)
                return RedirectToAction("Cart", "Cart");

            if (!ModelState.IsValid)
            {
                return View(order);
            }

            var user = await _userManager.GetUserAsync(User);
            var binOrEin = user?.BINorEIN;

            if (order.Shipping == null)
            {
                order.Shipping = shipping;
            }
            else
            {
                order.Shipping.FullName = shipping.FullName;
                order.Shipping.CompanyName = shipping.CompanyName;
                order.Shipping.Email = shipping.Email;
                order.Shipping.Phone = shipping.Phone;
                order.Shipping.StreetAddress = shipping.StreetAddress;
                order.Shipping.StreetAddress2 = shipping.StreetAddress2;
                order.Shipping.City = shipping.City;
                order.Shipping.Province = shipping.Province;
                order.Shipping.PostalCode = shipping.PostalCode;
                order.Shipping.Country = shipping.Country;
                order.Shipping.BinOrEin = shipping.BinOrEin;
                order.Shipping.DeliveryNotes = shipping.DeliveryNotes;
            }

            // Recalculate tax and total on the order immediately so the saved order reflects exemption
            var subtotal = order.Items?.Sum(i => i.TotalPrice) ?? 0m;
            
            var taxableSubtotal = order.Items?
                .Where(i => i.ProductVariant != null &&
                            i.ProductVariant.Product != null &&
                            i.ProductVariant.Product.IsTaxable)
                .Sum(i => i.TotalPrice) ?? 0m;
            
            if (!string.IsNullOrWhiteSpace(order.Shipping?.BinOrEin))
            {
                order.TaxAmount = 0m;
            }
            else
            {
                order.TaxAmount = Math.Round(taxableSubtotal * 0.13m, 2);
            }
            
            order.TotalAmount = Math.Round(subtotal + order.TaxAmount, 2);

            // We will mark submitted now that shipping is provided.
            // Before persisting, decrement stock for each variant in an atomic transaction.
            // Aggregate quantities by variant id to avoid double-check races within the order
            var variantQuantities = order.Items?
                .Where(i => i.ProductVariantId.HasValue)
                .GroupBy(i => i.ProductVariantId!.Value)
                .Select(g => new { VariantId = g.Key, Quantity = g.Sum(i => i.Quantity) })
                .ToList() ?? new();

            // Start transaction so stock updates + order status change are atomic
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // For each variant referenced in the order, load tracked entity and decrement.
                // Important: Do NOT decrement stock for special-order variants (InventoryPolicy == Continue).
                foreach (var vq in variantQuantities)
                {
                    var variant = await _context.ProductVariants
                        .Include(v => v.Product)
                        .FirstOrDefaultAsync(v => v.Id == vq.VariantId);

                    if (variant == null)
                    {
                        ModelState.AddModelError(string.Empty, $"Product variant (ID {vq.VariantId}) not found. Please review your cart.");
                        await transaction.RollbackAsync();
                        return View(order);
                    }

                    // Treat null policy as Deny (conservative)
                    var policy = variant.InventoryPolicy ?? InventoryPolicy.Deny;

                    // If variant is special-order (Continue), skip local stock checks and decrement.
                    if (policy == InventoryPolicy.Continue)
                    {
                        // Special-order items are fulfilled externally; do not touch local stock.
                        continue;
                    }

                    // For Deny (normal inventory) enforce stock availability
                    if (variant.StockQuantity < vq.Quantity)
                    {
                        ModelState.AddModelError(string.Empty, $"Insufficient stock for variant '{variant.SKU_ExternalID ?? variant.Id.ToString()}'. Available: {variant.StockQuantity}, requested: {vq.Quantity}.");
                        await transaction.RollbackAsync();
                        return View(order);
                    }

                    variant.StockQuantity -= vq.Quantity;
                    _context.ProductVariants.Update(variant);
                }

                // mark submitted now shipping provided
                order.Status = OrderStatus.Submitted;

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "Unable to update stock because the item was modified by someone else. Please review your cart and try again.");
                return View(order);
            }
            catch
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "An unexpected error occurred while submitting the order. Please try again.");
                return View(order);
            }



            TempData["SuccessMessage"] = "Shipping info saved successfully.";

            return RedirectToAction("Details", "Order", new { id = order.Id });
        }
    }
}
