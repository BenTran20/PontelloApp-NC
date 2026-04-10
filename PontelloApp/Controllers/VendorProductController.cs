using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PontelloApp.Custom_Controllers;
using PontelloApp.Data;
using PontelloApp.Models;

namespace PontelloApp.Controllers
{
    public class VendorProductController : ElephantController
    {
        private readonly PontelloAppContext _context;

        public VendorProductController(PontelloAppContext context)
        {
            _context = context;
        }

        // GET: /VendorProduct?vendorId=5
        public async Task<IActionResult> Index(int? vendorId)
        {
            if (vendorId == null)
                return NotFound();

            var vendor = await _context.Vendors
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.VendorID == vendorId);

            if (vendor == null)
                return NotFound();

            ViewBag.Vendor = vendor;

            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Variants)
                .Where(p => p.VendorID == vendorId)
                .OrderBy(p => p.ProductName)
                .AsNoTracking()
                .ToListAsync();

            return View(products);
        }

        // GET: /VendorProduct/Add?vendorId=5
        public async Task<IActionResult> Add(int? vendorId, string? vendorName)
        {
            if (!vendorId.HasValue)
            {
                return Redirect(ViewData["returnURL"]?.ToString() ?? "/");
            }

            var vendor = await _context.Vendors
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.VendorID == vendorId.Value);
            if (vendor == null)
                return NotFound();

            ViewData["VendorName"] = vendorName ?? vendor.Name;

            var model = new Product
            {
                VendorID = vendorId.Value,
                IsActive = true
            };

            PopulateDropDownLists();
            return View(model);
        }

        // POST: /VendorProduct/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(
            [Bind("ProductName,Handle,VendorID,Type,Tag,Description,IsActive,CategoryID")] Product product,
            string? vendorName)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    _context.Add(product);
                    await _context.SaveChangesAsync();
                    return Redirect(ViewData["returnURL"]?.ToString() ?? "/");
                }
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("",
                    "Unable to save changes. Try again, and if the problem persists see your system administrator.");
            }

            PopulateDropDownLists(product);
            ViewData["VendorName"] = vendorName;
            return View(product);
        }

        // GET: /VendorProduct/Edit/10
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Products == null)
                return NotFound();

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Vendor)
                .Include(p => p.Variants)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ID == id);

            if (product == null)
                return NotFound();

            // Đổ dropdowns
            PopulateDropDownLists(product);
            return View(product);
        }

        // POST: /VendorProduct/Edit/10
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id)
        {
            var productToUpdate = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Vendor)
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(p => p.ID == id);

            if (productToUpdate == null)
                return NotFound();

            if (await TryUpdateModelAsync<Product>(
                    productToUpdate, "",
                    p => p.ProductName, p => p.Handle, p => p.VendorID,
                    p => p.Type, p => p.Tag, p => p.Description,
                    p => p.IsActive, p => p.CategoryID))
            {
                try
                {
                    _context.Update(productToUpdate);
                    await _context.SaveChangesAsync();
                    return Redirect(ViewData["returnURL"]?.ToString() ?? "/");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(productToUpdate.ID))
                        return NotFound();
                    else
                        throw;
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("",
                        "Unable to save changes. Try again, and if the problem persists see your system administrator.");
                }
            }

            PopulateDropDownLists(productToUpdate);
            return View(productToUpdate);
        }
        // GET: /VendorProduct/Remove/10
        public async Task<IActionResult> Archive(int? id)
        {
            if (id == null || _context.Products == null)
                return NotFound();

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Vendor)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ID == id);

            if (product == null)
                return NotFound();

            return View(product);
        }

        // POST: /VendorProduct/Archive/10
        [HttpPost, ActionName("Archive")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Vendor)
                .FirstOrDefaultAsync(p => p.ID == id);

            if (product == null)
                return NotFound();

            try
            {
                product.IsActive = false; await _context.SaveChangesAsync();
                await _context.SaveChangesAsync();
                return Redirect(ViewData["returnURL"]?.ToString() ?? "/");
            }
            catch (Exception)
            {
                ModelState.AddModelError("",
                    "Unable to save changes. Try again, and if the problem persists see your system administrator.");
            }

            return View(product);
        }
        private SelectList CategorySelectList(int? id)
        {
            var query = from c in _context.Categories
                        orderby c.Name
                        select c;
            return new SelectList(query, "CategoryID", "CategoryName", id);
        }

        private void PopulateDropDownLists(Product? product = null)
        {
            ViewData["CategoryID"] = CategorySelectList(product?.CategoryID);
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(p => p.ID == id);
        }
    }
}
