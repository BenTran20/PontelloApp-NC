using Microsoft.EntityFrameworkCore;
using MimeKit;
using PontelloApp.Data;
using PontelloApp.Models;

namespace PontelloApp.Ultilities
{
    public class OrderService
    {
        private readonly PontelloAppContext _db;

        public OrderService(PontelloAppContext db)
        {
            _db = db;
        }

        /// <summary>
        /// </summary>
        public async Task<Order> GetOrCreateDraftOrderAsync(string userId)
        {
            var order = await _db.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.ProductVariant)
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.UserId == userId && o.Status == OrderStatus.Draft);

            if (order == null)
            {
                order = new Order
                {
                    UserId = userId,
                    Status = OrderStatus.Draft,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Orders.Add(order);
                await _db.SaveChangesAsync();
            }

            return order;
        }

        /// <summary>
        /// </summary>
        public async Task AddToCartAsync(string userId, int productId, int? variantId, int quantity)
        {
            var order = await GetOrCreateDraftOrderAsync(userId);

            // Lấy variant nếu có
            ProductVariant? variant = null;
            if (variantId.HasValue)
            {
                variant = await _db.ProductVariants
                    .Include(v => v.Product)
                    .FirstOrDefaultAsync(v => v.Id == variantId.Value);
            }

            var product = await _db.Products.FindAsync(productId)
                          ?? throw new Exception("Product not found");

            decimal unitPrice = variant.UnitPrice;

            var existingItem = order.Items.FirstOrDefault(i =>
                i.ProductId == productId && i.ProductVariantId == variantId);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                var orderItem = new OrderItem
                {
                    ProductId = productId,
                    ProductVariantId = variantId,
                    Quantity = quantity,
                    UnitPrice = unitPrice
                };
                order.Items.Add(orderItem);
            }

            order.TotalAmount = order.Items.Sum(i => i.TotalPrice);

            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// </summary>
        public async Task<List<OrderItem>> GetCartItemsAsync(string userId)
        {
            var order = await _db.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.ProductVariant)
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.UserId == userId && o.Status == OrderStatus.Draft);

            return order?.Items.ToList() ?? new List<OrderItem>();
        }

        /// <summary>
        /// </summary>
        public async Task UpdateOrderTotalAsync(string userId)
        {
            var order = await GetOrCreateDraftOrderAsync(userId);
            order.TotalAmount = order.Items.Sum(i => i.TotalPrice);
            await _db.SaveChangesAsync();
        }

    }
}
