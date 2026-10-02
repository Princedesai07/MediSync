using MediSync.Web.Data;
using MediSync.Web.Models;
using MediSync.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediSync.Web.Controllers;

[Authorize(Roles = "Admin,Doctor,Receptionist")]
public class DoctorsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public DoctorsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? search, string? specialization)
    {
        IQueryable<Doctor> query = _db.Doctors.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(d => (d.FirstName + " " + d.LastName).Contains(search) ||
                                     d.Specialization.Contains(search) ||
                                     (d.Email ?? "").Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(specialization))
            query = query.Where(d => d.Specialization == specialization);

        ViewBag.Search = search;
        ViewBag.Specialization = specialization;
        ViewBag.Specializations = await _db.Doctors.AsNoTracking()
            .Where(d => d.Specialization != "")
            .Select(d => d.Specialization)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        return View(await query.OrderBy(d => d.LastName).ThenBy(d => d.FirstName).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var doctor = await _db.Doctors
            .Include(d => d.Appointments).ThenInclude(a => a.Patient)
            .FirstOrDefaultAsync(d => d.Id == id);
        return doctor is null ? NotFound() : View(doctor);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Create() => View(new DoctorManagementViewModel());

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(DoctorManagementViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Email))
            ModelState.AddModelError(nameof(model.Email), "An email is required because every doctor profile can have a login account.");
        if (string.IsNullOrWhiteSpace(model.AccountPassword))
            ModelState.AddModelError(nameof(model.AccountPassword), "Set a temporary password for the doctor account.");
        ValidateAvailability(model.Availability);

        if (!ModelState.IsValid) return View(model);

        var normalizedEmail = model.Email!.Trim();
        if (await _userManager.FindByEmailAsync(normalizedEmail) is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            EmailConfirmed = true,
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim()
        };
        var createResult = await _userManager.CreateAsync(user, model.AccountPassword!);
        if (!createResult.Succeeded)
        {
            AddIdentityErrors(createResult);
            return View(model);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, SeedData.DoctorRole);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            AddIdentityErrors(roleResult);
            return View(model);
        }

        _db.Doctors.Add(new Doctor
        {
            ApplicationUserId = user.Id,
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            Specialization = model.Specialization.Trim(),
            Department = model.Department.Trim(),
            Phone = model.Phone.Trim(),
            Email = normalizedEmail,
            Availability = model.Availability.Trim()
        });
        await _db.SaveChangesAsync();
        TempData["Message"] = $"Dr. {model.FirstName} {model.LastName} and the login account were created.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var doctor = await _db.Doctors.FindAsync(id);
        if (doctor is null) return NotFound();
        return View(new DoctorManagementViewModel
        {
            Id = doctor.Id,
            ApplicationUserId = doctor.ApplicationUserId,
            FirstName = doctor.FirstName,
            LastName = doctor.LastName,
            Specialization = doctor.Specialization,
            Department = doctor.Department,
            Phone = doctor.Phone,
            Email = doctor.Email,
            Availability = doctor.Availability
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, DoctorManagementViewModel model)
    {
        if (id != model.Id) return BadRequest();
        ValidateAvailability(model.Availability);
        if (!ModelState.IsValid) return View(model);

        var doctor = await _db.Doctors.FindAsync(id);
        if (doctor is null) return NotFound();

        var email = model.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError(nameof(model.Email), "Doctor email is required.");
            return View(model);
        }

        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser is not null && existingUser.Id != doctor.ApplicationUserId)
        {
            ModelState.AddModelError(nameof(model.Email), "That email is already used by another account.");
            return View(model);
        }

        doctor.FirstName = model.FirstName.Trim();
        doctor.LastName = model.LastName.Trim();
        doctor.Specialization = model.Specialization.Trim();
        doctor.Department = model.Department.Trim();
        doctor.Phone = model.Phone.Trim();
        doctor.Email = email;
        doctor.Availability = model.Availability.Trim();

        if (doctor.ApplicationUserId is not null)
        {
            var user = await _userManager.FindByIdAsync(doctor.ApplicationUserId);
            if (user is not null)
            {
                user.FirstName = doctor.FirstName;
                user.LastName = doctor.LastName;
                user.Email = email;
                user.UserName = email;
                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    AddIdentityErrors(result);
                    return View(model);
                }
            }
        }

        await _db.SaveChangesAsync();
        TempData["Message"] = "Doctor profile updated.";
        return RedirectToAction(nameof(Index));
    }

    private void ValidateAvailability(string? availability)
    {
        if (string.IsNullOrWhiteSpace(availability))
        {
            ModelState.AddModelError(nameof(DoctorManagementViewModel.Availability), "Doctor availability is required. Use the format 'Mon-Fri 09:00-14:00'.");
            return;
        }

        var match = System.Text.RegularExpressions.Regex.Match(availability.Trim(), @"^(?<days>[A-Za-z]{3})(?:-(?<endDay>[A-Za-z]{3}))?\s+(?<start>\d{1,2}:\d{2})-(?<end>\d{1,2}:\d{2})$");
        if (!match.Success || !TimeSpan.TryParse(match.Groups["start"].Value, out var start) || !TimeSpan.TryParse(match.Groups["end"].Value, out var end) || start >= end || !IsDay(match.Groups["days"].Value) || (match.Groups["endDay"].Success && !IsDay(match.Groups["endDay"].Value)))
            ModelState.AddModelError(nameof(DoctorManagementViewModel.Availability), "Use a valid availability such as 'Mon-Fri 09:00-14:00'.");
    }

    private static bool IsDay(string value) => value.ToLowerInvariant() is "sun" or "mon" or "tue" or "wed" or "thu" or "fri" or "sat";

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
    }
}
