using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PontelloApp.Data;
using PontelloApp.Models;
 using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PontelloApp.Controllers
{
    public class CategoryController : Controller
    {
        private readonly PontelloAppContext _context;

        public CategoryController(PontelloAppContext context)
        {
            _context = context;
        }

        // GET: Category
        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories
                .Where(c => c.ParentCategoryID == null)
                .Include(c => c.Products)

                .Include(c => c.SubCategories)
                    .ThenInclude(sc1 => sc1.Products)
                .Include(c => c.SubCategories)
                    .ThenInclude(sc1 => sc1.SubCategories)
                        .ThenInclude(sc2 => sc2.Products)
                .Include(c => c.SubCategories)
                    .ThenInclude(sc1 => sc1.SubCategories)
                        .ThenInclude(sc2 => sc2.SubCategories)
                            .ThenInclude(sc3 => sc3.Products)
                .Include(c => c.SubCategories)
                    .ThenInclude(sc1 => sc1.SubCategories)
                        .ThenInclude(sc2 => sc2.SubCategories)
                            .ThenInclude(sc3 => sc3.SubCategories)
                                .ThenInclude(sc4 => sc4.Products)
                .Include(c => c.SubCategories)
                    .ThenInclude(sc1 => sc1.SubCategories)
                        .ThenInclude(sc2 => sc2.SubCategories)
                            .ThenInclude(sc3 => sc3.SubCategories)
                                .ThenInclude(sc4 => sc4.SubCategories)
                                    .ThenInclude(sc5 => sc5.Products)
                .AsNoTracking()
                .ToListAsync();


            return View(categories);
        }


        // GET: Category/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories
                .Include(c => c.ParentCategory)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ID == id);
            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // GET: Category/Create
        public IActionResult Create(int? parentId)
        {
            var model = new Category();

            if (parentId.HasValue)
            {
                model.ParentCategoryID = parentId.Value;

                ViewBag.FullCategory = GetFullCategoryPath(parentId.Value);
            }

            return View(model);
        }


        // POST: Category/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    _context.Add(category);
                    await _context.SaveChangesAsync();

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new SelectList(_context.Categories, "ID", "Name", category.ID));
                    }

                    return RedirectToAction(nameof(Index));
                }

                if (!ModelState.IsValid && Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    string errorMessage = "";
                    foreach (var modelState in ViewData.ModelState.Values)
                    {
                        foreach (ModelError error in modelState.Errors)
                        {
                            errorMessage += error.ErrorMessage + "|";
                        }
                    }
                    return BadRequest(errorMessage);
                }

                ViewData["ParentCategoryID"] = new SelectList(_context.Categories, "ID", "Name", category.ParentCategoryID);
                return View(category);
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
            }

            return View(category);
        }


        // GET: Category/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            ViewBag.FullCategory = GetFullCategoryPath(category.ID);

            return View(category);
        }

        // POST: Category/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,Name,ParentCategoryID")] Category category)
        {
            if (id != category.ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(category);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CategoryExists(category.ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.FullCategory = GetFullCategoryPath(category.ID);
            return View(category);
        }

        // GET: Category/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories
                .Include(c => c.ParentCategory)
                .Include(c => c.SubCategories)
                .FirstOrDefaultAsync(m => m.ID == id);
            if (category == null)
            {
                return NotFound();
            }

            ViewBag.FullCategory = GetFullCategoryPath(category.ID);
            return View(category);
        }

        // POST: Category/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories
                .Include(c => c.SubCategories) 
                .FirstOrDefaultAsync(c => c.ID == id);

            try
            {
                if (category.SubCategories.Any())
                {
                    var categoryWithParents = await _context.Categories
                        .Include(c => c.ParentCategory)
                        .FirstOrDefaultAsync(c => c.ID == id);

                    ViewBag.FullCategory = GetFullCategoryPath(categoryWithParents.ID);
                    ModelState.AddModelError("", "Cannot delete this category because it has subcategories");
                    return View("Delete", categoryWithParents);
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));

            }
            catch (DbUpdateException dex)
            {
                if (dex.GetBaseException().Message.Contains("FOREIGN KEY constraint failed"))
                {
                    ModelState.AddModelError("", "Unable to Delete Category. Remember, you cannot delete a Category that has Products assigned.");
                }
                else
                {
                    ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
                }
            }

            return View(category);
        }

        private bool CategoryExists(int id)
        {
            return _context.Categories.Any(e => e.ID == id);
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

        private string GetFullCategoryPath(int categoryId)
        {
            var category = _context.Categories
                .AsNoTracking()  
                .FirstOrDefault(c => c.ID == categoryId);

            if (category == null)
                return "";

            if (category.ParentCategoryID == null)
                return category.Name;

            return GetFullCategoryPath(category.ParentCategoryID.Value) + " > " + category.Name;
        }


    }
}

