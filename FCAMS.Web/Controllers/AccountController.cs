using System;
using System.Web.Mvc;
using System.Web.Security;
using FCAMS.Web.DAL.Repositories;
using FCAMS.Web.Infrastructure;
using FCAMS.Web.Models.ViewModels;
using FCAMS.Web.Services;
using Newtonsoft.Json;

namespace FCAMS.Web.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly IAuthenticationService _authService;
        private readonly IAuditLogService _auditLog;

        public AccountController() : this(new AuthenticationService(new UserRepository()), new AuditLogService()) { }

        public AccountController(IAuthenticationService authService, IAuditLogService auditLog)
        {
            _authService = authService;
            _auditLog = auditLog;
        }

        [HttpGet]
        public ActionResult Login(string returnUrl)
        {
            if (Request.IsAuthenticated) return RedirectToAction("Index", "Home");
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var outcome = _authService.Authenticate(model.Username, model.Password);

            switch (outcome.Result)
            {
                case LoginResult.Success:
                    SignIn(outcome.User);
                    _auditLog.LogAction("Login", "User", outcome.User.UserId.ToString());

                    if (Url.IsLocalUrl(model.ReturnUrl))
                        return Redirect(model.ReturnUrl);
                    return RedirectToAction("Index", "Home");

                case LoginResult.AccountLockedOut:
                    _auditLog.LogAction("LoginFailed", "User", null, null,
                        new { model.Username, reason = "Locked out" });
                    ModelState.AddModelError("", "This account is temporarily locked due to repeated failed login attempts. Try again in 15 minutes.");
                    return View(model);

                case LoginResult.AccountInactive:
                    ModelState.AddModelError("", "This account has been deactivated. Contact your system administrator.");
                    return View(model);

                default:
                    _auditLog.LogAction("LoginFailed", "User", null, null,
                        new { model.Username, reason = "Invalid credentials" });
                    ModelState.AddModelError("", "Invalid username or password.");
                    return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            var userId = SessionUser.Current?.UserId.ToString();
            _auditLog.LogAction("Logout", "User", userId);
            FormsAuthentication.SignOut();
            Session.Clear();
            return RedirectToAction("Login");
        }

        [HttpGet]
        public ActionResult AccessDenied()
        {
            return View();
        }

        private void SignIn(Models.User user)
        {
            var userData = JsonConvert.SerializeObject(new CurrentUserInfo
            {
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.FullName,
                RoleName = user.RoleName,
                DepartmentId = user.DepartmentId
            });

            var ticket = new FormsAuthenticationTicket(
                version: 1,
                name: user.Username,
                issueDate: DateTime.Now,
                expiration: DateTime.Now.AddMinutes(30),
                isPersistent: false,
                userData: userData,
                cookiePath: FormsAuthentication.FormsCookiePath);

            string encryptedTicket = FormsAuthentication.Encrypt(ticket);
            var cookie = new System.Web.HttpCookie(FormsAuthentication.FormsCookieName, encryptedTicket)
            {
                HttpOnly = true,
                Secure = Request.IsSecureConnection
            };
            Response.Cookies.Add(cookie);

            SessionUser.Current = JsonConvert.DeserializeObject<CurrentUserInfo>(userData);
        }
    }
}
