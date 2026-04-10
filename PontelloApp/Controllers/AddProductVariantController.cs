using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Build.Tasks.Deployment.Bootstrapper;
using Microsoft.EntityFrameworkCore;
using PontelloApp.Custom_Controllers;
using PontelloApp.Data;
using PontelloApp.Models;

namespace PontelloApp.Controllers
{
    public class AddProductVariantController : ElephantController
    {
        private readonly PontelloAppContext _context;

        public AddProductVariantController(PontelloAppContext context)
        {
            _context = context;
        }

        // GET: AddProductVariant
        public async Task<IActionResult> Index(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.Options)
                .Include(p => p.Vendor)
                .FirstOrDefaultAsync(p => p.ID == id);

            if (product == null) return NotFound();

            LoadCategoryParents(product.Category);
            ViewBag.Product = product;

            var variants = product.Variants?.OrderBy(v => v.Id).ToList() ?? new List<ProductVariant>();

            PopulateDropDownLists();
            return View(variants); 
        }



        // GET: AddProductVariant/Add
        public IActionResult Add(int? ProductId)
        {
            if (!ProductId.HasValue)
            {
                return RedirectToAction(nameof(Index));
            }

            ProductVariant v = new ProductVariant
            {
                ProductId = ProductId.GetValueOrDefault(),
                Options = new List<Variant>() 
            };

            var product = _context.Products.Find(ProductId.Value);
            ViewBag.Product = product;
            return View(v);
        }

        // POST: AddProductVariant/Add
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(ProductVariant variant)
        {
            if (variant.Options == null)
                variant.Options = new List<Variant>();

            // --- VALIDATE OPTIONS ---
            for (int i = 0; i < variant.Options.Count; i++)
            {
                var opt = variant.Options[i];
                if (string.IsNullOrWhiteSpace(opt.Name) || opt.Name.Length < 3)
                {
                    ModelState.AddModelError($"Options[{i}].Name", "Variant Name must be at least 3 characters long");
                }
                if (string.IsNullOrWhiteSpace(opt.Value) || opt.Value.Length < 3)
                {
                    ModelState.AddModelError($"Options[{i}].Value", "Variant Value must be at least 3 characters long");
                }
                opt.ProductVariantId = variant.Id;
            }

            if (variant.Options.Count == 0)
            {
                ModelState.AddModelError("Options", "Must add at least one option");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.ProductVariants.Add(variant);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Add new product variant successfully";
                    if(variant.Status == true)
                    {
                        TempData["Status"] = "Status: Active";
                    }
                    else
                    {
                        TempData["Status"] = "Status: Inactive";
                    }
                    return RedirectToAction(nameof(Index), new { id = variant.ProductId });
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("", "Unable to save changes.");
                }
            }

            var product = _context.Products.Find(variant.ProductId);
            ViewBag.Product = product;
            return View(variant);
        }


        // GET: AddProductVariant/Update/5
        public async Task<IActionResult> Update(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .Include(v => v.Options)
                .FirstOrDefaultAsync(v => v.Id == id);
            
            if (variant == null)
            {
                return NotFound();
            }

            return View(variant);
        }

        // POST: AddProductVariant/Update/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, ProductVariant variant, Byte[] RowVersion)
        {
            var variantToUpdate = await _context.ProductVariants
                .Include(v => v.Product)
                .Include(v => v.Options)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (variantToUpdate == null) return NotFound();

            _context.Entry(variantToUpdate).Property("RowVersion").OriginalValue = RowVersion;

            if (await TryUpdateModelAsync<ProductVariant>(variantToUpdate, "",
                v => v.SKU_ExternalID,
                v => v.UnitPrice,
                v => v.CostPrice,
                v => v.CompareAtPrice,
                v => v.StockQuantity,
                v => v.Weight,
                v => v.Barcode,
                v => v.InventoryPolicy,
                v => v.Status))
            {
                // --- VALIDATE OPTIONS ---
                if (variant.Options == null) variant.Options = new List<Variant>();

                for (int i = 0; i < variant.Options.Count; i++)
                {
                    var opt = variant.Options[i];
                    if (string.IsNullOrWhiteSpace(opt.Name) || opt.Name.Length < 3)
                    {
                        ModelState.AddModelError($"Options[{i}].Name", "Option Name must be at least 3 characters long");
                    }
                    if (string.IsNullOrWhiteSpace(opt.Value) || opt.Value.Length < 3)
                    {
                        ModelState.AddModelError($"Options[{i}].Value", "Option Value must be at least 3 characters long");
                    }
                    opt.ProductVariantId = variantToUpdate.Id;
                }

                if (variant.Options.Count == 0)
                {
                    ModelState.AddModelError("Options", "Must add at least one option");
                }

                if (ModelState.IsValid)
                {
                    if (variantToUpdate.Options.Any())
                    {
                        _context.Variants.RemoveRange(variantToUpdate.Options);
                    }

                    foreach (var opt in variant.Options)
                    {
                        _context.Variants.Add(opt);
                    }

                    try
                    {
                        await _context.SaveChangesAsync();
                        TempData["Success"] = "Update product variant Successfully";
                        if (variant.Status == true)
                        {
                            TempData["Status"] = "Status: Active";
                        }
                        else
                        {
                            TempData["Status"] = "Status: Inactive";

                        }
                        return RedirectToAction(nameof(Index), new { id = variantToUpdate.ProductId });
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
                            if (databaseValues.CostPrice != clientValues.CostPrice)
                                ModelState.AddModelError("CostPrice", "Current value: "
                                    + databaseValues.CostPrice);
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
                            ModelState.AddModelError(string.Empty, "The record you attempted to update "
                                    + "was modified by another user after you received your values. The "
                                    + "update operation was canceled and the current values in the database "
                                    + "have been displayed. If you still want to save your version of this record, click "
                                    + "the Save button again. Otherwise click the 'Back to Product List' hyperlink.");

                            //Final steps before redisplaying: Update RowVersion from the Database
                            //and remove the RowVersion error from the ModelState
                            variantToUpdate.RowVersion = databaseValues.RowVersion ?? Array.Empty<byte>();
                            ModelState.Remove("RowVersion");
                        }
                    }
                    catch (DbUpdateException dex)
                    {
                        ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
                    }
                }
            }
            variant.Product = variantToUpdate.Product;
            return View(variant);
        }


        // GET: AddProductVariant/Remove/5
        public async Task<IActionResult> Remove(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var variant = await _context.ProductVariants
                            .Include(v => v.Product)
                            .Include(v => v.Options)
                            .AsNoTracking()
                            .FirstOrDefaultAsync(v => v.Id == id);

            if (variant == null) return NotFound();

            return View(variant);
        }

        // POST: AddProductVariant/Remove/5
        [HttpPost, ActionName("Remove")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveConfirmed(int id, Byte[] RowVersion)
        {
            var variant = await _context.ProductVariants
                            .Include(v => v.Product)
                            .Include(v => v.Options)
                            .FirstOrDefaultAsync(v => v.Id == id);

            try
            {
                _context.Entry(variant).Property("RowVersion").OriginalValue = RowVersion;
                _context.ProductVariants.Remove(variant);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Remove product variant Successfully";
                TempData["Status"] = "";
                return RedirectToAction(nameof(Index), new { id = variant.ProductId });
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty, "The record you attempted to remove"
                                + "was modified by another user. Please go back on refresh.");
                ViewData["CantSave"] = "disabled='disabled'";
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Unable to remove record. Try again, and if the problem persists see your system administrator.");
            }

            return View(variant);
        }
        private SelectList ProductSelectList(int? selectedId)
        {
            return new SelectList(_context.Products
                .OrderBy(d => d.ProductName), "ID", "ProductName", selectedId);
        }

        private void PopulateDropDownLists(ProductVariant? productVariant = null)
        {
            ViewData["ProductId"] = ProductSelectList(productVariant?.ProductId);
        }

        private bool ProductVariantExists(int id)
        {
            return _context.ProductVariants.Any(e => e.Id == id);
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
