using EVCharging.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;

namespace EVCharging.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;

        private readonly SignInManager<IdentityUser> _signInManager;

        private readonly IHtmlContentGenerator _generator;

        public DashboardController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            IHtmlContentGenerator generator)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _generator = generator;
        }

        public ActionResult Privacy()
        {
            return View("Privacy");
        }

        public ActionResult Index()
        {
            _generator.SignalRTimer.Change(2000, 2000);

            return View("Index");
        }

        public ActionResult Notify()
        {
            IdentityUser user = _userManager.GetUserAsync(User).GetAwaiter().GetResult();
            bool signedIn = _signInManager.IsSignedIn(User);

            if ((user != null) && user.EmailConfirmed && signedIn)
            {
                _generator.NotificationList.Add(user.Email);
                Console.WriteLine("Added user " + user.Email + " to notification list.");

                return View("Notification");
            }
            else
            {
                return View("Index");
            }
        }

        public ActionResult ClaimBadge(string badgeId)
        {
            IdentityUser user = _userManager.GetUserAsync(User).GetAwaiter().GetResult();
            bool signedIn = _signInManager.IsSignedIn(User);

            if ((user != null) && user.EmailConfirmed && signedIn)
            {
                if (!_generator.BadgeClaims.ContainsKey(badgeId))
                {
                    _generator.BadgeClaims.Add(badgeId, user.Email);
                    Console.WriteLine("Added user " + user.Email + " to badge claim list.");
                }

                return View("Claim", new ClaimModel { BadgeId = badgeId });
            }
            else
            {
                return View("Index");
            }
        }
    }
}
