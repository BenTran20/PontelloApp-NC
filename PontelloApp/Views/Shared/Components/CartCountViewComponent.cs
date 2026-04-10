using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PontelloApp.Data;
using PontelloApp.Models;

public class CartCountViewComponent : ViewComponent
{
    private readonly PontelloAppContext _context;
    private readonly UserManager<User> _userManager;


    public CartCountViewComponent(PontelloAppContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = await _userManager.GetUserAsync(HttpContext.User);

        if (user == null)
        {
            return View(0);
        }

        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.UserId == user.Id && o.Status == OrderStatus.Draft);

        int count = order?.Items?.Sum(i => i.Quantity) ?? 0;

        return View(count);
    }
}
