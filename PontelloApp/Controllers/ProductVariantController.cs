using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Build.Tasks.Deployment.Bootstrapper;
using Microsoft.EntityFrameworkCore;
using PontelloApp.Custom_Controllers;
using PontelloApp.Data;
using PontelloApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PontelloApp.Controllers
{
    public class ProductVariantController : ElephantController
    {
        private readonly PontelloAppContext _context;

        public ProductVariantController(PontelloAppContext context)
        {
            _context = context;
        }

        // GET: ProductVariant
        public async Task<IActionResult> Index()
        {
            var pontelloAppContext = _context.ProductVariants.Include(p => p.Product);
            return View(await pontelloAppContext.ToListAsync());
        }

        // GET: ProductVariant/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var productVariant = await _context.ProductVariants
                .Include(p => p.Product)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (productVariant == null)
            {
                return NotFound();
            }

            return View(productVariant);
        }

        // GET: ProductVariant/Create
        public IActionResult Create()
        {
            ViewData["ProductId"] = new SelectList(_context.Products, "ID", "ProductName");
            return View();
        }

        // POST: ProductVariant/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,UnitPrice,StockQuantity,SKU_ExternalID,ProductId")] ProductVariant productVariant)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    _context.Add(productVariant);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Create new product variant Successfully";
                    return RedirectToAction(nameof(Index));
                }
            }

            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
            }
            ViewData["ProductId"] = new SelectList(_context.Products, "ID", "ProductName", productVariant.ProductId);
            return View(productVariant);
        }

        // GET: ProductVariant/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var productVariant = await _context.ProductVariants.FindAsync(id);
            if (productVariant == null)
            {
                return NotFound();
            }
            ViewData["ProductId"] = new SelectList(_context.Products, "ID", "ProductName", productVariant.ProductId);
            return View(productVariant);
        }

        // POST: ProductVariant/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,UnitPrice,StockQuantity,SKU_ExternalID,ProductId")] ProductVariant productVariant, Byte[] RowVersion)
        {
            if (id != productVariant.Id)
            {
                return NotFound();
            }

            _context.Entry(productVariant).Property("RowVersion").OriginalValue = RowVersion;

            if (ModelState.IsValid)
            {
                try
                {
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Edit product variant Successfully";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    var exceptionEntry = ex.Entries.Single();
                    var clientValues = (ProductVariant)exceptionEntry.Entity;
                    var databaseEntry = exceptionEntry.GetDatabaseValues();
                    if (databaseEntry == null)
                    {
                        ModelState.AddModelError("",
                            "Unable to save changes. The Product was archived by another user.");
                    }
                    else
                    {
                        var databaseValues = (ProductVariant)databaseEntry.ToObject();
                        if (databaseValues.UnitPrice != clientValues.UnitPrice)
                            ModelState.AddModelError("UnitPrice", "Current value: "
                                + databaseValues.UnitPrice);
                        if (databaseValues.StockQuantity != clientValues.StockQuantity)
                            ModelState.AddModelError("StockQuantity", "Current value: "
                                + databaseValues.StockQuantity);
                        if (databaseValues.SKU_ExternalID != clientValues.SKU_ExternalID)
                            ModelState.AddModelError("SKU_ExternalID", "Current value: "
                                + databaseValues.SKU_ExternalID);
                        if (databaseValues.CompareAtPrice != clientValues.CompareAtPrice)
                            ModelState.AddModelError("CompareAtPrice", "Current value: "
                                + databaseValues.CompareAtPrice);
                        if (databaseValues.Weight != clientValues.Weight)
                            ModelState.AddModelError("Weight", "Current value: "
                                + databaseValues.Weight);
                        if (databaseValues.Barcode != clientValues.Barcode)
                            ModelState.AddModelError("Barcode", "Current value: "
                                + databaseValues.Barcode);
                        if (databaseValues.InventoryPolicy != clientValues.InventoryPolicy)
                            ModelState.AddModelError("InventoryPolicy", "Current value: "
                                + databaseValues.InventoryPolicy);
                        //For the foreign key, we need to go to the database to get the information to show
                        if (databaseValues.ProductId != clientValues.ProductId)
                        {
                            Models.Product? databaseProduct = await _context.Products.FirstOrDefaultAsync(i => i.ID == databaseValues.ProductId);
                            ModelState.AddModelError("ProductId", $"Current value: {databaseProduct?.ProductName}");
                        }
                        ModelState.AddModelError(string.Empty, "The record you attempted to edit "
                                + "was modified by another user after you received your values. The "
                                + "edit operation was canceled and the current values in the database "
                                + "have been displayed. If you still want to save your version of this record, click "
                                + "the Save button again. Otherwise click the 'Back to Product List' hyperlink.");

                        //Final steps before redisplaying: Update RowVersion from the Database
                        //and remove the RowVersion error from the ModelState
                        productVariant.RowVersion = databaseValues.RowVersion ?? Array.Empty<byte>();
                        ModelState.Remove("RowVersion");
                    }
                }
                catch (DbUpdateException dex)
                {
                    ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
                }
            }
            ViewData["ProductId"] = new SelectList(_context.Products, "ID", "ProductName", productVariant.ProductId);
            return View(productVariant);
        }

        // GET: ProductVariant/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var productVariant = await _context.ProductVariants
                .Include(p => p.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);
            if (productVariant == null)
            {
                return NotFound();
            }

            return View(productVariant);
        }

        // POST: ProductVariant/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, Byte[] RowVersion)
        {
            var productVariant = await _context.ProductVariants.FindAsync(id);
            try
            {
                if (productVariant != null)
                {
                    _context.Entry(productVariant).Property("RowVersion").OriginalValue = RowVersion;
                    _context.ProductVariants.Remove(productVariant);
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Delete product variant Successfully";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty, "The Product Variant you attempted to delete "
                                + "was modified by another user. Please go back on refresh.");
                ViewData["CantSave"] = "disabled='disabled'";
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Unable to delete Product Variant. Try again, and if the problem persists see your system administrator.");
            }
            return View(productVariant);

        }

        private bool ProductVariantExists(int id)
        {
            return _context.ProductVariants.Any(e => e.Id == id);
        }
    }
}
