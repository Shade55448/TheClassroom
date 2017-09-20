﻿using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using TheClassroom.Models;
using Microsoft.Owin.Security.DataProtection;

namespace TheClassroom.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;

        public AccountController()
        {
        }

        public AccountController(ApplicationUserManager userManager, ApplicationSignInManager signInManager )
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            }
            private set 
            { 
                _signInManager = value; 
            }
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        //
        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // This doesn't count login failures towards account lockout
            // To enable password failures to trigger account lockout, change to shouldLockout: true
            var result = await SignInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, shouldLockout: false);
            switch (result)
            {
                case SignInStatus.Success:
                    return RedirectToLocal(returnUrl);
                case SignInStatus.LockedOut:
                    return View("Lockout");
                case SignInStatus.RequiresVerification:
                    return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });
                case SignInStatus.Failure:
                default:
                    ModelState.AddModelError("", "Přihlášení se nezdařilo");
                    return View(model);
            }
        }


        //
        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            return View();
        }

        //
        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
                var result = await UserManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await SignInManager.SignInAsync(user, isPersistent:false, rememberBrowser:false);


                    //Email send code
                     string code = await UserManager.GenerateEmailConfirmationTokenAsync(user.Id);
                     var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);
                     await UserManager.SendEmailAsync(user.Id, "Potvrzení účtu", "Prosím potvrďte váš účet zde: <a href=\"" + callbackUrl + "\">Potvrzení účtu</a>");

                    return RedirectToAction("EmailSent", "Account", new { Email = user.Email });
                }
                AddErrors(result);
            }

            // Something failed, redisplay
            return View(model);
        }

        //
        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        public ActionResult EmailSent(string email)
        {
            var user = UserManager.FindByEmail(email);
            if (user == null) //Secure
            {
                return View("Error");
            }
            ViewBag.Email = email;
            return View();
        }
        [AllowAnonymous]
        public ActionResult ResendVerifyEmail(string email)
        {
            var user = UserManager.FindByEmail(email);

            if (user == null)
                return View("Error");
            if (user.EmailConfirmed)
                return View();
            // Email send code
            string code = UserManager.GenerateEmailConfirmationToken(user.Id);
            var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);
            UserManager.SendEmail(user.Id, "Potvrzení účtu", "Prosím potvrďte váš účet zde: <a href=\"" + callbackUrl + "\">Potvrzení účtu</a>");

            return RedirectToAction("EmailSent", "Account", new { Email = user.Email });
        }

        //
        // GET: /Account/ConfirmEmail
        [AllowAnonymous]
        public async Task<ActionResult> ConfirmEmail(int userId = -1, string code = null)
        {
            if (userId == -1|| code == null) //Secure
            {
                return View("Error");
            }
            var result = await UserManager.ConfirmEmailAsync(userId, code);
            return View(result.Succeeded ? "ConfirmEmail" : "Error"); //Potvrď
        }

        /* //
         // GET: /Account/VerifyCode
         [AllowAnonymous]
         public async Task<ActionResult> VerifyCode(string provider, string returnUrl, bool rememberMe)
         {
             // Require that the user has already logged in via username/password or external login
             if (!await SignInManager.HasBeenVerifiedAsync())
             {
                 return View("Error");
             }
             return View(new VerifyCodeViewModel { Provider = provider, ReturnUrl = returnUrl, RememberMe = rememberMe });
         }

         //
         // POST: /Account/VerifyCode
         [HttpPost]
         [AllowAnonymous]
         [ValidateAntiForgeryToken]
         public async Task<ActionResult> VerifyCode(VerifyCodeViewModel model)
         {
             if (!ModelState.IsValid)
             {
                 return View(model);
             }

             // The following code protects for brute force attacks against the two factor codes. 
             // If a user enters incorrect codes for a specified amount of time then the user account 
             // will be locked out for a specified amount of time. 
             // You can configure the account lockout settings in IdentityConfig
             var result = await SignInManager.TwoFactorSignInAsync(model.Provider, model.Code, isPersistent: model.RememberMe, rememberBrowser: model.RememberBrowser);
             switch (result)
             {
                 case SignInStatus.Success:
                     return RedirectToLocal(model.ReturnUrl);
                 case SignInStatus.LockedOut:
                     return View("Lockout");
                 case SignInStatus.Failure:
                 default:
                     ModelState.AddModelError("", "Invalid code.");
                     return View(model);
             }
         }



         //
         // GET: /Account/ForgotPassword
         [AllowAnonymous]
         public ActionResult ForgotPassword()
         {
             return View();
         }

         //
         // POST: /Account/ForgotPassword
         [HttpPost]
         [AllowAnonymous]
         [ValidateAntiForgeryToken]
         public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
         {
             if (ModelState.IsValid)
             {
                 var user = await UserManager.FindByNameAsync(model.Email);
                 if (user == null || !(await UserManager.IsEmailConfirmedAsync(user.Id)))
                 {
                     // Don't reveal that the user does not exist or is not confirmed
                     return View("ForgotPasswordConfirmation");
                 }

                 // For more information on how to enable account confirmation and password reset please visit https://go.microsoft.com/fwlink/?LinkID=320771
                 // Send an email with this link
                 // string code = await UserManager.GeneratePasswordResetTokenAsync(user.Id);
                 // var callbackUrl = Url.Action("ResetPassword", "Account", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);		
                 // await UserManager.SendEmailAsync(user.Id, "Reset Password", "Please reset your password by clicking <a href=\"" + callbackUrl + "\">here</a>");
                 // return RedirectToAction("ForgotPasswordConfirmation", "Account");
             }

             // If we got this far, something failed, redisplay form
             return View(model);
         }

         //
         // GET: /Account/ForgotPasswordConfirmation
         [AllowAnonymous]
         public ActionResult ForgotPasswordConfirmation()
         {
             return View();
         }

         //
         // GET: /Account/ResetPassword
         [AllowAnonymous]
         public ActionResult ResetPassword(string code)
         {
             return code == null ? View("Error") : View();
         }

         //
         // POST: /Account/ResetPassword
         [HttpPost]
         [AllowAnonymous]
         [ValidateAntiForgeryToken]
         public async Task<ActionResult> ResetPassword(ResetPasswordViewModel model)
         {
             if (!ModelState.IsValid)
             {
                 return View(model);
             }
             var user = await UserManager.FindByNameAsync(model.Email);
             if (user == null)
             {
                 // Don't reveal that the user does not exist
                 return RedirectToAction("ResetPasswordConfirmation", "Account");
             }
             var result = await UserManager.ResetPasswordAsync(user.Id, model.Code, model.Password);
             if (result.Succeeded)
             {
                 return RedirectToAction("ResetPasswordConfirmation", "Account");
             }
             AddErrors(result);
             return View();
         }

         //
         // GET: /Account/ResetPasswordConfirmation
         [AllowAnonymous]
         public ActionResult ResetPasswordConfirmation()
         {
             return View();
         }

         //
         // GET: /Account/SendCode
         [AllowAnonymous]
         public async Task<ActionResult> SendCode(string returnUrl, bool rememberMe)
         {
             var userId = await SignInManager.GetVerifiedUserIdAsync();
             if (userId == null)
             {
                 return View("Error");
             }
             var userFactors = await UserManager.GetValidTwoFactorProvidersAsync(userId);
             var factorOptions = userFactors.Select(purpose => new SelectListItem { Text = purpose, Value = purpose }).ToList();
             return View(new SendCodeViewModel { Providers = factorOptions, ReturnUrl = returnUrl, RememberMe = rememberMe });
         }

         //
         // POST: /Account/SendCode
         [HttpPost]
         [AllowAnonymous]
         [ValidateAntiForgeryToken]
         public async Task<ActionResult> SendCode(SendCodeViewModel model)
         {
             if (!ModelState.IsValid)
             {
                 return View();
             }

             // Generate the token and send it
             if (!await SignInManager.SendTwoFactorCodeAsync(model.SelectedProvider))
             {
                 return View("Error");
             }
             return RedirectToAction("VerifyCode", new { Provider = model.SelectedProvider, ReturnUrl = model.ReturnUrl, RememberMe = model.RememberMe });
         }


       */

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_userManager != null)
                {
                    _userManager.Dispose();
                    _userManager = null;
                }

                if (_signInManager != null)
                {
                    _signInManager.Dispose();
                    _signInManager = null;
                }
            }

            base.Dispose(disposing);
        }

        #region Helpers
        // Used for XSRF protection when adding external logins
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                if (error.StartsWith("Name "))
                    ModelState.AddModelError("", "");  //Fix bug - Username = Email
                else if (error.Contains("is already taken."))
                    ModelState.AddModelError("", "Uživatel s touto emailovou adresou již existuje");
                else if (error.StartsWith("Passwords must "))
                    ModelState.AddModelError("", "Heslo musí obsahovat alespoň 6 znaků a jedno číslo");
                else ModelState.AddModelError("", error);
            }
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        internal class ChallengeResult : HttpUnauthorizedResult
        {
            public ChallengeResult(string provider, string redirectUri)
                : this(provider, redirectUri, null)
            {
            }

            public ChallengeResult(string provider, string redirectUri, string userId)
            {
                LoginProvider = provider;
                RedirectUri = redirectUri;
                UserId = userId;
            }

            public string LoginProvider { get; set; }
            public string RedirectUri { get; set; }
            public string UserId { get; set; }

            public override void ExecuteResult(ControllerContext context)
            {
                var properties = new AuthenticationProperties { RedirectUri = RedirectUri };
                if (UserId != null)
                {
                    properties.Dictionary[XsrfKey] = UserId;
                }
                context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
            }
        }
        #endregion
    }
}