using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PontelloApp.Custom_Controllers;
using PontelloApp.Data;
using PontelloApp.Models;

namespace PontelloApp.Controllers
{
    public class VendorController : ElephantController
    {
        private readonly PontelloAppContext _context;

        public VendorController(PontelloAppContext context)
        {
            _context = context;
        }

        // GET: Vendor
        public async Task<IActionResult> Index()
        {

            var vendors = await _context.Vendors
                    .Where(v => !v.IsArchived)
                    .OrderBy(v => v.Name)
                    .ToListAsync();

            return View(vendors);

        }

        // GET: Vendor/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vendor = await _context.Vendors
                .FirstOrDefaultAsync(m => m.VendorID == id);
            if (vendor == null)
            {
                return NotFound();
            }

            return View(vendor);
        }

        // GET: Vendor/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Vendor/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("VendorID,Name,ContactName,Phone,Email,IsArchived,RowVersion")] Vendor vendor)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    _context.Add(vendor);
                    await _context.SaveChangesAsync();

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new[] { new SelectListItem { Value = vendor.VendorID.ToString(), Text = vendor.Name, Selected = true } });
                    }

                    return RedirectToAction(nameof(Index));
                }
            }

            catch (DbUpdateException dex)
            {
                if (dex.GetBaseException().Message.Contains("UNIQUE constraint failed"))
                {
                    ModelState.AddModelError("Name", "Unable to save changes. " +
                        "Remember, you cannot have duplicate Vendor Names.");
                }
                else
                {
                    ModelState.AddModelError("", "Unable to save changes. Try again, " +
                        "and if the problem persists see your system administrator.");
                }
            }

            //Decide if we need to send the Validaiton Errors directly to the client
            if (!ModelState.IsValid && Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                //Was an AJAX request so build a message with all validation errors
                string errorMessage = "";
                foreach (var modelState in ViewData.ModelState.Values)
                {
                    foreach (ModelError error in modelState.Errors)
                    {
                        errorMessage += error.ErrorMessage + "|";
                    }
                }
                //Note: returning a BadRequest results in HTTP Status code 400
                return BadRequest(errorMessage);
            }

            return View(vendor);
        }

        // GET: Vendor/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vendor = await _context.Vendors.FindAsync(id);
            if (vendor == null)
            {
                return NotFound();
            }
            return View(vendor);
        }

        // POST: Vendor/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("VendorID,Name,ContactName,Phone,Email,IsArchived,RowVersion")] Vendor vendor)
        {
            if (id != vendor.VendorID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(vendor);
                    await _context.SaveChangesAsync();
                    return Json(new[] { new SelectListItem { Value = vendor.VendorID.ToString(), Text = vendor.Name, Selected = true } });

                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!VendorExists(vendor.VendorID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (DbUpdateException dex)
                {
                    if (dex.GetBaseException().Message.Contains("UNIQUE constraint failed"))
                    {
                        ModelState.AddModelError("Name", "Unable to save changes. " +
                            "Remember, you cannot have duplicate Vendor Names.");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Unable to save changes. Try again, " +
                            "and if the problem persists see your system administrator.");
                    }
                }

                return RedirectToAction(nameof(Index));
            }
            return View(vendor);
        }


        // GET: Vendor/Archive/5
        public async Task<IActionResult> Archive(int? id)
        {
            if (id == null)
                return NotFound();

            var vendor = await _context.Vendors
                .FirstOrDefaultAsync(v => v.VendorID == id);

            if (vendor == null)
                return NotFound();

            return View(vendor);
        }



        // POST: Vendor/Archive/5
        [HttpPost, ActionName("Archive")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveConfirmed(int id)
        {
            var vendor = await _context.Vendors.FindAsync(id);
            if (vendor == null)
                return NotFound();

            vendor.IsArchived = true;

            var products = await _context.Products
                                         .Where(p => p.VendorID == id && p.IsActive)
                                         .ToListAsync();

            foreach (var p in products)
            {
                p.IsActive = false;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }



        // GET: Vendor/Archived
        public async Task<IActionResult> Archived()
        {
            var archivedVendors = await _context.Vendors
                .Where(v => v.IsArchived)
                .OrderBy(v => v.Name)
                .AsNoTracking()
                .ToListAsync();

            return View(archivedVendors);
        }


        // POST: Vendor/Unarchive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unarchive(int id)
        {
            var vendor = await _context.Vendors.FindAsync(id);
            if (vendor == null)
                return NotFound();

            vendor.IsArchived = false;

            var products = await _context.Products
                                         .Where(p => p.VendorID == id && !p.IsActive)
                                         .ToListAsync();

            foreach (var p in products)
            {
                p.IsActive = true;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Archived));
        }


        private bool VendorExists(int id)
        {
            return _context.Vendors.Any(e => e.VendorID == id);
        }
    }
}
