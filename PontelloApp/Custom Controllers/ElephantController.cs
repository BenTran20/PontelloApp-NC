using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;
using PontelloApp.Data;
using PontelloApp.Models;
using PontelloApp.Ultilities;

namespace PontelloApp.Custom_Controllers
{
    /// <summary>
    /// The Elephant Controller has a good memory to help
    /// persist the Index Sort, Filter and Paging parameters
    /// into a URL stored in ViewData
    /// WARNING: Depends on the following Utilities
    ///  - CookieHelper
    ///  - MaintainURL
    /// </summary>
    public class ElephantController : CognizantController
    {
        //This is the list of Actions that will add the ReturnURL to ViewData
        internal string[] ActionWithURL = [ "Details", "Create", "Edit", "Delete", "Archive",
            "Add", "Update", "Remove" ];

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (ActionWithURL.Contains(ActionName()))
            {
                ViewData["returnURL"] = MaintainURL.ReturnURL(HttpContext, ControllerName());
            }
            else if (ActionName() == "Index")
            {
                //Clear the sort/filter/paging URL Cookie for Controller
                CookieHelper.CookieSet(HttpContext, ControllerName() + "URL", "", -1);
            }

            if (User.Identity.IsAuthenticated)
            {
                var _context = (PontelloAppContext)HttpContext.RequestServices.GetService(typeof(PontelloAppContext));
                var _userManager = (UserManager<User>)HttpContext.RequestServices.GetService(typeof(UserManager<User>));

                var userId = _userManager.GetUserId(User);
                var notifications = _context.Notifications
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToList();

                ViewData["Notifications"] = notifications;
            }
            base.OnActionExecuting(context);
        }

        public override Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            if (ActionWithURL.Contains(ActionName()))
            {
                ViewData["returnURL"] = MaintainURL.ReturnURL(HttpContext, ControllerName());
            }
            else if (ActionName() == "Index")
            {
                //Clear the sort/filter/paging URL Cookie for Controller
                CookieHelper.CookieSet(HttpContext, ControllerName() + "URL", "", -1);
            }
            return base.OnActionExecutionAsync(context, next);
        }
    }
}
