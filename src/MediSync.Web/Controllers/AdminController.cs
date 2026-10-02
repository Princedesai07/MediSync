using MediSync.Web.Data;
using MediSync.Web.Models;
using MediSync.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediSync.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpGet]
    public async Task<IActionResult> Users(string? search, string? role, string? status)
    {
        var users = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            users = users.Where(u =>
                (u.FirstName + " " + u.LastName).Contains(search) ||
                (u.Email ?? "").Contains(search));
        }

        var rows = new List<AdminUserRowViewModel>();
        foreach (var user in await users.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToListAsync())
        {
            var roles = await _userManager.GetRolesAsync(user);
            var userRole = roles.FirstOrDefault() ?? "No role";
            var isActive = !user.LockoutEnabled || user.LockoutEnd is null || user.LockoutEnd <= DateTimeOffset.UtcNow;

            if (!string.IsNullOrWhiteSpace(role) && !string.Equals(role, userRole, StringComparison.OrdinalIgnoreCase))
                continue;
            if (status == "active" && !isActive) continue;
            if (status == "inactive" && isActive) continue;

            string? profileType = null;
            if (userRole == SeedData.DoctorRole && await _db.Doctors.AnyAsync(d => d.ApplicationUserId == user.Id)) profileType = "Doctor";
            if (userRole == SeedData.PatientRole && await _db.Patients.AnyAsync(p => p.ApplicationUserId == user.Id)) profileType = "Patient";

            rows.Add(new AdminUserRowViewModel { User = user, Role = userRole, IsActive = isActive, ProfileType = profileType });
        }

        return View(new AdminUsersViewModel { Search = search, Role = role, Status = status, Users = rows });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUserStatus(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (user.Id == _userManager.GetUserId(User))
        {
            TempData["Error"] = "You cannot deactivate your own administrator account.";
            return RedirectToAction(nameof(Users));
        }

        var isActive = !user.LockoutEnabled || user.LockoutEnd is null || user.LockoutEnd <= DateTimeOffset.UtcNow;

        if (isActive)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var activeWork = await GetActiveWorkMessageAsync(user.Id, roles);
            if (activeWork is not null)
            {
                TempData["Error"] = activeWork;
                return RedirectToAction(nameof(Users));
            }
        }

        user.LockoutEnabled = true;
        user.LockoutEnd = isActive ? DateTimeOffset.UtcNow.AddYears(100) : null;
        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
            await _userManager.UpdateSecurityStampAsync(user);

        if (!result.Succeeded)
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
        else
            TempData["Message"] = isActive ? $"{user.Email} has been deactivated." : $"{user.Email} has been activated.";

        return RedirectToAction(nameof(Users));
    }

    private async Task<string?> GetActiveWorkMessageAsync(string userId, IList<string> roles)
    {
        if (roles.Contains(SeedData.DoctorRole))
        {
            var doctor = await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.ApplicationUserId == userId);
            if (doctor is not null)
            {
                if (await _db.Appointments.AnyAsync(a => a.DoctorId == doctor.Id && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed)))
                    return "This doctor has pending or confirmed appointments. Complete or cancel the active appointments before deactivating the account.";
                if (await _db.LabTests.AnyAsync(t => t.DoctorId == doctor.Id && (t.Status == LabTestStatus.Requested || t.Status == LabTestStatus.InProgress)))
                    return "This doctor has pending laboratory work. Complete or cancel the active lab requests before deactivating the account.";
                if (await _db.Prescriptions.AnyAsync(p => p.DoctorId == doctor.Id && (p.Status == PrescriptionStatus.Pending || p.Status == PrescriptionStatus.Processing)))
                    return "This doctor has pending or processing prescriptions. Finish or cancel them before deactivating the account.";
            }
        }

        if (roles.Contains(SeedData.PatientRole))
        {
            var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.ApplicationUserId == userId);
            if (patient is not null)
            {
                if (await _db.Appointments.AnyAsync(a => a.PatientId == patient.Id && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed)))
                    return "This patient has pending or confirmed appointments. Complete or cancel the active appointments before deactivating the account.";
                if (await _db.LabTests.AnyAsync(t => t.PatientId == patient.Id && (t.Status == LabTestStatus.Requested || t.Status == LabTestStatus.InProgress)))
                    return "This patient has pending laboratory work. Complete or cancel the active lab tests before deactivating the account.";
                if (await _db.Prescriptions.AnyAsync(p => p.PatientId == patient.Id && (p.Status == PrescriptionStatus.Pending || p.Status == PrescriptionStatus.Processing)))
                    return "This patient has pending or processing prescriptions. Finish or cancel them before deactivating the account.";
            }
        }

        if (roles.Contains(SeedData.LaboratoryRole) &&
            await _db.LabTests.AnyAsync(t => (t.Status == LabTestStatus.Requested || t.Status == LabTestStatus.InProgress)))
            return "This laboratory account has pending lab work. Complete the lab queue before deactivating the account.";

        if (roles.Contains(SeedData.PharmacistRole) &&
            await _db.Prescriptions.AnyAsync(p => (p.Status == PrescriptionStatus.Pending || p.Status == PrescriptionStatus.Processing)))
            return "This pharmacist account has pending or processing prescriptions. Finish the pharmacy queue before deactivating the account.";

        return null;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (user.Id == _userManager.GetUserId(User))
        {
            TempData["Error"] = "You cannot delete your own administrator account.";
            return RedirectToAction(nameof(Users));
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(SeedData.DoctorRole) || roles.Contains(SeedData.PatientRole))
        {
            TempData["Error"] = "Doctor and Patient accounts are linked to clinical profiles. Deactivate them instead of deleting them.";
            return RedirectToAction(nameof(Users));
        }

        var result = await _userManager.DeleteAsync(user);
        TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
            ? $"{user.Email} has been deleted."
            : string.Join(" ", result.Errors.Select(e => e.Description));
        return RedirectToAction(nameof(Users));
    }
}
