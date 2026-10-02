using MediSync.Web.Data;
using MediSync.Web.Models;
using MediSync.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediSync.Web.Controllers;

[Authorize(Roles = "Admin,Doctor,Receptionist")]
public class PatientsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public PatientsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? search, string? gender)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var roles = await _userManager.GetRolesAsync(user);

        IQueryable<Patient> query = _db.Patients.AsNoTracking();
        if (roles.Contains(SeedData.DoctorRole))
            query = query.Where(p => p.Appointments.Any(a => a.Doctor.ApplicationUserId == user.Id) ||
                                     p.MedicalRecords.Any(r => r.Doctor.ApplicationUserId == user.Id));
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(p => (p.FirstName + " " + p.LastName).Contains(search) ||
                                     (p.Email ?? "").Contains(search) || p.Phone.Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(gender)) query = query.Where(p => p.Gender == gender);

        ViewBag.Search = search;
        ViewBag.Gender = gender;
        return View(await query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(SeedData.DoctorRole) && !await _db.Patients.AnyAsync(p => p.Id == id &&
            (p.Appointments.Any(a => a.Doctor.ApplicationUserId == user.Id) || p.MedicalRecords.Any(r => r.Doctor.ApplicationUserId == user.Id))))
            return Forbid();

        var patient = await _db.Patients
            .Include(p => p.Appointments).ThenInclude(a => a.Doctor)
            .Include(p => p.MedicalRecords).ThenInclude(r => r.Doctor)
            .Include(p => p.LabTests)
            .Include(p => p.Prescriptions)
            .FirstOrDefaultAsync(p => p.Id == id);
        return patient is null ? NotFound() : View(patient);
    }

    [Authorize(Roles = "Admin,Receptionist")]
    public IActionResult Create() => View(new PatientManagementViewModel { DateOfBirth = DateTime.Today.AddYears(-18) });

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,Receptionist")]
    public async Task<IActionResult> Create(PatientManagementViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Email))
            ModelState.AddModelError(nameof(model.Email), "An email is required because every patient can have a portal login.");
        if (string.IsNullOrWhiteSpace(model.AccountPassword))
            ModelState.AddModelError(nameof(model.AccountPassword), "Set a temporary password for the patient portal account.");
        if (model.DateOfBirth > DateTime.Today)
            ModelState.AddModelError(nameof(model.DateOfBirth), "Date of birth cannot be in the future.");

        if (!ModelState.IsValid) return View(model);

        var email = model.Email!.Trim();
        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
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

        var roleResult = await _userManager.AddToRoleAsync(user, SeedData.PatientRole);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            AddIdentityErrors(roleResult);
            return View(model);
        }

        _db.Patients.Add(new Patient
        {
            ApplicationUserId = user.Id,
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            DateOfBirth = model.DateOfBirth,
            Gender = model.Gender.Trim(),
            Phone = model.Phone.Trim(),
            Email = email,
            Address = model.Address?.Trim(),
            EmergencyContact = model.EmergencyContact?.Trim(),
            RegistrationDate = DateTime.UtcNow.Date
        });
        await _db.SaveChangesAsync();
        TempData["Message"] = $"{model.FirstName} {model.LastName} and the patient portal account were created.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,Receptionist")]
    public async Task<IActionResult> Edit(int id)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient is null) return NotFound();
        return View(new PatientManagementViewModel
        {
            Id = patient.Id,
            ApplicationUserId = patient.ApplicationUserId,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            DateOfBirth = patient.DateOfBirth,
            Gender = patient.Gender,
            Phone = patient.Phone,
            Email = patient.Email,
            Address = patient.Address,
            EmergencyContact = patient.EmergencyContact
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,Receptionist")]
    public async Task<IActionResult> Edit(int id, PatientManagementViewModel model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var patient = await _db.Patients.FindAsync(id);
        if (patient is null) return NotFound();
        var email = model.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError(nameof(model.Email), "Patient email is required.");
            return View(model);
        }

        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser is not null && existingUser.Id != patient.ApplicationUserId)
        {
            ModelState.AddModelError(nameof(model.Email), "That email is already used by another account.");
            return View(model);
        }

        patient.FirstName = model.FirstName.Trim();
        patient.LastName = model.LastName.Trim();
        patient.DateOfBirth = model.DateOfBirth;
        patient.Gender = model.Gender.Trim();
        patient.Phone = model.Phone.Trim();
        patient.Email = email;
        patient.Address = model.Address?.Trim();
        patient.EmergencyContact = model.EmergencyContact?.Trim();

        if (patient.ApplicationUserId is not null)
        {
            var userAccount = await _userManager.FindByIdAsync(patient.ApplicationUserId);
            if (userAccount is not null)
            {
                userAccount.FirstName = patient.FirstName;
                userAccount.LastName = patient.LastName;
                userAccount.Email = email;
                userAccount.UserName = email;
                var result = await _userManager.UpdateAsync(userAccount);
                if (!result.Succeeded)
                {
                    AddIdentityErrors(result);
                    return View(model);
                }
            }
        }
        else
        {
            // Repair legacy/orphaned patient profiles when they are edited.
            // If an account already exists for the email, link it; otherwise
            // create the missing patient portal account.
            var linkedUser = existingUser;
            if (linkedUser is null)
            {
                linkedUser = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = patient.FirstName,
                    LastName = patient.LastName
                };

                var createResult = await _userManager.CreateAsync(linkedUser, model.AccountPassword ?? "MediSync@123");
                if (!createResult.Succeeded)
                {
                    AddIdentityErrors(createResult);
                    return View(model);
                }
            }

            if (!await _userManager.IsInRoleAsync(linkedUser, SeedData.PatientRole))
            {
                var roleResult = await _userManager.AddToRoleAsync(linkedUser, SeedData.PatientRole);
                if (!roleResult.Succeeded)
                {
                    AddIdentityErrors(roleResult);
                    return View(model);
                }
            }

            linkedUser.FirstName = patient.FirstName;
            linkedUser.LastName = patient.LastName;
            linkedUser.Email = email;
            linkedUser.UserName = email;
            var updateResult = await _userManager.UpdateAsync(linkedUser);
            if (!updateResult.Succeeded)
            {
                AddIdentityErrors(updateResult);
                return View(model);
            }

            patient.ApplicationUserId = linkedUser.Id;
        }

        await _db.SaveChangesAsync();
        TempData["Message"] = "Patient profile updated.";
        return RedirectToAction(nameof(Index));
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
    }
}
