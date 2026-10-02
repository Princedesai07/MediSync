using MediSync.Web.Data;
using MediSync.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace MediSync.Web.Controllers;
[Authorize(Roles = "Patient")]
public class PatientPortalController : Controller
{
    private readonly ApplicationDbContext _db; private readonly UserManager<ApplicationUser> _userManager;
    public PatientPortalController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) { _db = db; _userManager = userManager; }
    private async Task<Patient?> CurrentPatientAsync() { var user = await _userManager.GetUserAsync(User); return user is null ? null : await _db.Patients.FirstOrDefaultAsync(p => p.ApplicationUserId == user.Id); }
    public IActionResult Index() => RedirectToAction("Index", "Dashboard");
    public async Task<IActionResult> History()
    {
        var patient = await CurrentPatientAsync(); if (patient is null) return NotFound();
        var data = await _db.Patients
            .Include(p => p.Appointments).ThenInclude(a => a.Doctor)
            .Include(p => p.MedicalRecords).ThenInclude(r => r.Doctor)
            .Include(p => p.LabTests).ThenInclude(l => l.Doctor)
            .Include(p => p.Prescriptions).ThenInclude(pr => pr.Doctor)
            .Include(p => p.Prescriptions).ThenInclude(pr => pr.Items).ThenInclude(i => i.Medicine)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == patient.Id);
        return View(data);
    }
    public async Task<IActionResult> Profile() { var patient = await CurrentPatientAsync(); return patient is null ? NotFound() : View(patient); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(Patient input)
    {
        var patient = await CurrentPatientAsync(); if (patient is null || input.Id != patient.Id) return Forbid();
        if (!ModelState.IsValid) return View(patient);

        var email = input.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError(nameof(Patient.Email), "Email is required for the patient portal account.");
            return View(patient);
        }

        if (patient.ApplicationUserId is not null)
        {
            var user = await _userManager.FindByIdAsync(patient.ApplicationUserId);
            if (user is not null)
            {
                var existing = await _userManager.FindByEmailAsync(email);
                if (existing is not null && existing.Id != user.Id)
                {
                    ModelState.AddModelError(nameof(Patient.Email), "That email is already used by another account.");
                    return View(patient);
                }

                user.Email = email;
                user.UserName = email;
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    foreach (var error in updateResult.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                    return View(patient);
                }
            }
        }

        patient.Phone = input.Phone?.Trim() ?? string.Empty;
        patient.Email = email;
        patient.Address = input.Address?.Trim();
        patient.EmergencyContact = input.EmergencyContact?.Trim();
        await _db.SaveChangesAsync();
        TempData["Message"] = "Profile updated successfully.";
        return RedirectToAction(nameof(Profile));
    }
}
