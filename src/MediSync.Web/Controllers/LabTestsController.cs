using MediSync.Web.Data;
using MediSync.Web.Models;
using MediSync.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MediSync.Web.Controllers;

[Authorize]
public class LabTestsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    public LabTestsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) { _db = db; _userManager = userManager; }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(SeedData.ReceptionistRole) || roles.Contains(SeedData.PharmacistRole)) return Forbid();

        IQueryable<LabTest> q = _db.LabTests
            .Include(l => l.Patient).Include(l => l.Doctor).Include(l => l.MedicalRecord)
            .AsNoTracking();

        if (roles.Contains(SeedData.PatientRole)) q = q.Where(l => l.Patient.ApplicationUserId == user.Id);
        else if (roles.Contains(SeedData.DoctorRole)) q = q.Where(l => l.Doctor.ApplicationUserId == user.Id);

        return View(await q.OrderByDescending(l => l.RequestedDate).ToListAsync());
    }

    [Authorize(Roles = "Doctor,Admin")]
    public async Task<IActionResult> Create(int? medicalRecordId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var vm = new LabTestCreateViewModel();
        var records = _db.MedicalRecords.Include(r => r.Patient).Include(r => r.Doctor).AsQueryable();
        if (await _userManager.IsInRoleAsync(user, SeedData.DoctorRole))
            records = records.Where(r => r.Doctor.ApplicationUserId == user.Id);

        if (medicalRecordId.HasValue)
        {
            var record = await records.FirstOrDefaultAsync(r => r.Id == medicalRecordId.Value);
            if (record is not null)
            {
                vm.MedicalRecordId = record.Id;
                vm.PatientId = record.PatientId;
                vm.DoctorId = record.DoctorId;
            }
        }

        await Populate(vm, user);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Doctor,Admin")]
    public async Task<IActionResult> Create(LabTestCreateViewModel vm)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        MedicalRecord? record = null;
        if (vm.MedicalRecordId.HasValue)
        {
            record = await _db.MedicalRecords.Include(r => r.Patient).Include(r => r.Doctor)
                .FirstOrDefaultAsync(r => r.Id == vm.MedicalRecordId.Value);
            if (record is null) ModelState.AddModelError(nameof(vm.MedicalRecordId), "Medical record not found.");
            else
            {
                if (await _userManager.IsInRoleAsync(user, SeedData.DoctorRole) && record.Doctor.ApplicationUserId != user.Id) return Forbid();
                // The patient and doctor must come from the selected clinical record.
                vm.PatientId = record.PatientId;
                vm.DoctorId = record.DoctorId;
            }
        }
        else if (await _userManager.IsInRoleAsync(user, SeedData.DoctorRole))
        {
            ModelState.AddModelError(nameof(vm.MedicalRecordId), "Select a medical record for a doctor lab request.");
        }

        if (!await _db.Patients.AnyAsync(p => p.Id == vm.PatientId)) ModelState.AddModelError(nameof(vm.PatientId), "Patient not found.");
        if (!await _db.Doctors.AnyAsync(d => d.Id == vm.DoctorId)) ModelState.AddModelError(nameof(vm.DoctorId), "Doctor not found.");

        if (!ModelState.IsValid) { await Populate(vm, user); return View(vm); }

        _db.LabTests.Add(new LabTest
        {
            PatientId = vm.PatientId,
            DoctorId = vm.DoctorId,
            MedicalRecordId = vm.MedicalRecordId,
            TestName = vm.TestName,
            Notes = vm.Notes,
            Status = LabTestStatus.Requested,
            RequestedDate = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        TempData["Message"] = "Laboratory test requested successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Laboratory,Admin")]
    public async Task<IActionResult> UpdateStatus(int id, LabTestStatus status, string? result, string? reportNotes)
    {
        var test = await _db.LabTests.FindAsync(id);
        if (test is null) return NotFound();
        var transitionAllowed = (test.Status, status) switch
        {
            (LabTestStatus.Requested, LabTestStatus.Requested) => true,
            (LabTestStatus.Requested, LabTestStatus.InProgress) => true,
            (LabTestStatus.Requested, LabTestStatus.Cancelled) => true,
            (LabTestStatus.InProgress, LabTestStatus.InProgress) => true,
            (LabTestStatus.InProgress, LabTestStatus.Completed) => true,
            (LabTestStatus.InProgress, LabTestStatus.Cancelled) => true,
            (LabTestStatus.Completed, LabTestStatus.Completed) => true,
            (LabTestStatus.Cancelled, LabTestStatus.Cancelled) => true,
            _ => false
        };

        if (!transitionAllowed)
        {
            TempData["Error"] = $"Lab test cannot move from {test.Status} to {status}.";
            return RedirectToAction(nameof(Index));
        }

        if (status == LabTestStatus.Completed && string.IsNullOrWhiteSpace(result))
        { TempData["Error"] = "A completed lab test needs a result."; return RedirectToAction(nameof(Index)); }

        if (test.Status == LabTestStatus.Completed && status == LabTestStatus.Completed && string.IsNullOrWhiteSpace(result))
        { TempData["Error"] = "A completed lab test must keep its result."; return RedirectToAction(nameof(Index)); }

        test.Status = status;
        test.Result = result;
        test.ReportNotes = reportNotes;
        test.CompletedDate = status == LabTestStatus.Completed ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync();
        TempData["Message"] = status == LabTestStatus.Completed ? "Lab result completed." : "Lab test status updated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task Populate(LabTestCreateViewModel vm, ApplicationUser user)
    {
        var records = _db.MedicalRecords.Include(r => r.Patient).Include(r => r.Doctor).AsQueryable();
        if (await _userManager.IsInRoleAsync(user, SeedData.DoctorRole)) records = records.Where(r => r.Doctor.ApplicationUserId == user.Id);
        vm.MedicalRecords = await records.OrderByDescending(r => r.VisitDate)
            .Select(r => new SelectListItem($"#{r.Id} - {r.Patient.FirstName} {r.Patient.LastName} - {r.VisitDate:dd MMM yyyy}", r.Id.ToString())).ToListAsync();
        vm.Patients = await _db.Patients.OrderBy(p => p.LastName).Select(p => new SelectListItem(p.FullName, p.Id.ToString())).ToListAsync();
        vm.Doctors = await _db.Doctors.OrderBy(d => d.LastName).Select(d => new SelectListItem(d.FullName, d.Id.ToString())).ToListAsync();
    }
}
