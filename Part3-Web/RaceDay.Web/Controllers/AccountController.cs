using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Models;

namespace RaceDay.Web.Controllers;

public class AccountController : Controller
{
    private readonly IRaceDayApi _api;
    public AccountController(IRaceDayApi api) => _api = api;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (HttpContext.Session.GetUser() is { } user) return RedirectToHome(user);
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            var auth = await _api.LoginAsync(model.Email, model.Password);

            HttpContext.Session.SetUser(new SessionUser(auth.UserId, auth.FullName, auth.Role));
            TempData["Success"] = $"Welcome back, {auth.FullName.Split(' ')[0]}.";

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return LocalRedirect(model.ReturnUrl);
            return RedirectToHome(new SessionUser(auth.UserId, auth.FullName, auth.Role));
        }
        catch (ApiException ex)
        {
            // Wrong password, API down, etc. - show a clear message, never the raw response
            ModelState.AddModelError(string.Empty,
                ex.StatusCode == 401 ? "That email and password don't match. Check them and try again." : ex.Message);
            model.Password = "";
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (HttpContext.Session.GetUser() is { } user) return RedirectToHome(user);
        return View(new RegisterViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            await _api.RegisterAsync(model.FullName, model.Email, model.Password, model.Role);
        }
        catch (ApiException ex)
        {
            ModelState.AddApiErrors(ex);
            return View(model);
        }

        // Log the new user straight in
        try
        {
            var auth = await _api.LoginAsync(model.Email, model.Password);
            var user = new SessionUser(auth.UserId, auth.FullName, auth.Role);
            HttpContext.Session.SetUser(user);
            TempData["Success"] = $"Welcome to RaceDay, {auth.FullName.Split(' ')[0]}! Your account is ready.";
            return RedirectToHome(user);
        }
        catch (ApiException)
        {
            TempData["Success"] = "Your account has been created. Log in to get started.";
            return RedirectToAction(nameof(Login));
        }
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        if (HttpContext.Session.GetUser() is not null) await _api.LogoutAsync();
        HttpContext.Session.Clear();
        TempData["Success"] = "You've been logged out.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    private IActionResult RedirectToHome(SessionUser user) =>
        user.IsOrganiser ? RedirectToAction("Dashboard", "Organiser") : RedirectToAction("Index", "Home");
}
