using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PontelloApp.Custom_Controllers;
using PontelloApp.Data;
using PontelloApp.Models;
using PontelloApp.Ultilities;

namespace PontelloApp.Controllers
{
    [Authorize(Roles = "Dealer")]
    public class CartController : ElephantController
    {
        private readonly PontelloAppContext _context;
        private readonly UserManager<User> _userManager;

        public CartController(PontelloAppContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Cart()
        {
            var user = await _userManager.GetUserAsync(User);

            var order = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                .Include(o => o.Items)
                    .ThenInclude(i => i.ProductVariant)
                        .ThenInclude(pv => pv.Options)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o =>
                    o.UserId == user.Id &&
                    o.Status == OrderStatus.Draft);

            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCart(int id, int quantity, string action)
        {
            // Handle + and -
            if (action == "minus")
                quantity -= 1;

            if (action == "plus")
                quantity += 1;

            var item = await _context.OrderItems
                .Include(i => i.Order)
                    .ThenInclude(o => o.Items)
                        .ThenInclude(oi => oi.ProductVariant)
                            .ThenInclude(pv => pv.Product)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null)
            {
                return RedirectToAction("Cart");
            }

            if (quantity > item.ProductVariant.StockQuantity)
            {
                quantity = (int)item.ProductVariant.StockQuantity;
            }

            var order = item.Order;

            IEnumerable<OrderItem> remainingItems;

            if (quantity <= 0)
            {
                _context.OrderItems.Remove(item);
                remainingItems = order.Items.Where(x => x.Id != item.Id);
            }
            else
            {
                item.Quantity = quantity;
                remainingItems = order.Items;
            }

            var subtotal = remainingItems.Sum(x => x.TotalPrice);

            var taxableSubtotal = remainingItems
                .Where(x => x.ProductVariant != null &&
                            x.ProductVariant.Product != null &&
                            x.ProductVariant.Product.IsTaxable)
                .Sum(x => x.TotalPrice);

            order.TaxAmount = Math.Round(taxableSubtotal * 0.13m, 2);
            order.TotalAmount = subtotal + order.TaxAmount;

            await _context.SaveChangesAsync();

            return RedirectToAction("Cart");
        }


        [HttpPost]
        public async Task<IActionResult> RemoveItem(int itemId)
        {
            var user = await _userManager.GetUserAsync(User);

            var cart = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Include(o => o.Items)
                    .ThenInclude(i => i.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                .FirstOrDefaultAsync(o =>
                    o.UserId == user.Id &&
                    o.Status == OrderStatus.Draft);

            if (cart == null)
            {
                TempData["ErrorMessage"] = "Cart not found.";
                return RedirectToAction("Cart");
            }

            var item = cart.Items.FirstOrDefault(x => x.Id == itemId);
            if (item != null)
            {
                cart.Items.Remove(item);
                _context.OrderItems.Remove(item);

                var subtotal = cart.Items.Sum(x => x.TotalPrice);

                var taxableSubtotal = cart.Items
                    .Where(x => x.ProductVariant != null &&
                                x.ProductVariant.Product != null &&
                                x.ProductVariant.Product.IsTaxable)
                    .Sum(x => x.TotalPrice);

                cart.TaxAmount = Math.Round(taxableSubtotal * 0.13m, 2);
                cart.TotalAmount = subtotal + cart.TaxAmount;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Item removed from cart.";
            }
            else
            {
                TempData["ErrorMessage"] = "Item not found.";
            }

            return RedirectToAction("Cart");
        }

        // Ensure a Shipping placeholder exists for the order (prevents null ref and gives a place to fill BIN/EIN)
        private void EnsureShippingPlaceholder(Order order)
        {
            if (order == null) return;
            if (order.Shipping != null) return;

            var shipping = new Shipping
            {
                FullName = string.Empty,
                StreetAddress = string.Empty,
                City = string.Empty,
                Province = string.Empty,
                PostalCode = string.Empty,
                Country = string.Empty,
                Phone = string.Empty,
                Email = string.Empty,
                BinOrEin = string.Empty,
                ShippingCost = null,
                OrderId = order.Id
            };

            // Track the new shipping and attach to the order
            _context.Shippings.Add(shipping);
            order.Shipping = shipping;
        }

        // POST: create the order (keeps it in Draft) then redirect to Shipping controller to collect shipping info
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            var cart = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Include(o => o.Items)
                    .ThenInclude(i => i.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                .FirstOrDefaultAsync(o =>
                    o.Id == id &&
                    o.UserId == user.Id &&
                    o.Status == OrderStatus.Draft);

            if (cart == null || cart.Items == null || !cart.Items.Any())
                return RedirectToAction("Cart");

            // generate PO, keep status as Draft until shipping is provided
            cart.PONumber = $"PO-{DateTime.UtcNow:yyyyMMddHHmmss}";

            cart.UserId = user.Id;
            cart.Status = OrderStatus.Progress;
            cart.CreatedAt = DateTime.UtcNow;

            // create a shipping placeholder so Shipping view/controller always has an object to update (including BIN/EIN)
            if (cart.Shipping == null)
            {
                EnsureShippingPlaceholder(cart);
            }

            // calculate current tax/total for informational purposes (will be recalculated when shipping saved)
            var taxableSubtotal = cart.Items
                .Where(i => i.ProductVariant != null &&
                            i.ProductVariant.Product != null &&
                            i.ProductVariant.Product.IsTaxable)
                .Sum(i => i.TotalPrice);

            var subtotal = cart.Items.Sum(i => i.TotalPrice);

            cart.TaxAmount = Math.Round(taxableSubtotal * 0.13m, 2);
            cart.TotalAmount = subtotal + cart.TaxAmount;

            // persist the created order (with its items and shipping placeholder)
            await _context.SaveChangesAsync();

            // show success message at the top of the shipping form
            TempData["SuccessMessage"] = "Order created. Please provide shipping to complete submission.";

            return RedirectToAction("Create", "Shipping", new { orderId = cart.Id });
        }

    }
}
