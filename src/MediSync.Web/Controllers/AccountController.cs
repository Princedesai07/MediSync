using MediSync.Web.Data;
using MediSync.Web.Models;
using MediSync.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
namespace MediSync.Web.Controllers;
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _db;
    public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ApplicationDbContext db) { _userManager = userManager; _signInManager = signInManager; _db = db; }
    [AllowAnonymous] public IActionResult Login() => View(new LoginViewModel());
    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null) { ModelState.AddModelError(string.Empty, "Invalid login attempt."); return View(model); }
        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded) return LocalRedirect(returnUrl ?? Url.Action("Index", "Dashboard")!);
        ModelState.AddModelError(string.Empty, "Invalid email or password."); return View(model);
    }
    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout() { await _signInManager.SignOutAsync(); return RedirectToAction(nameof(Login)); }
    [AllowAnonymous] public IActionResult Register() => View(new RegisterViewModel());
    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = new ApplicationUser { UserName = model.Email, Email = model.Email, EmailConfirmed = true, FirstName = model.FirstName, LastName = model.LastName };
        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded) { foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description); return View(model); }
        await _userManager.AddToRoleAsync(user, SeedData.PatientRole);
        _db.Patients.Add(new Patient { ApplicationUserId = user.Id, FirstName = model.FirstName, LastName = model.LastName, Phone = model.Phone, Email = model.Email, RegistrationDate = DateTime.UtcNow.Date });
        await _db.SaveChangesAsync(); await _signInManager.SignInAsync(user, false);
        return RedirectToAction("Index", "Dashboard");
    }
    [AllowAnonymous] public IActionResult AccessDenied() => View();
}
