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
public class MedicalRecordsController : Controller
{
    private readonly ApplicationDbContext _db; private readonly UserManager<ApplicationUser> _userManager;
    public MedicalRecordsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) { _db = db; _userManager = userManager; }
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User); if (user is null) return Challenge(); var roles = await _userManager.GetRolesAsync(user);
        IQueryable<MedicalRecord> q = _db.MedicalRecords.Include(r => r.Patient).Include(r => r.Doctor).AsNoTracking();
        if (roles.Contains(SeedData.PatientRole)) q = q.Where(r => r.Patient.ApplicationUserId == user.Id);
        else if (roles.Contains(SeedData.DoctorRole)) q = q.Where(r => r.Doctor.ApplicationUserId == user.Id);
        else if (roles.Contains(SeedData.ReceptionistRole) || roles.Contains(SeedData.LaboratoryRole) || roles.Contains(SeedData.PharmacistRole)) return Forbid();
        return View(await q.OrderByDescending(r => r.VisitDate).ToListAsync());
    }
    [Authorize(Roles = "Doctor,Admin")]
    public async Task<IActionResult> Create(int? appointmentId)
    {
        var user = await _userManager.GetUserAsync(User); if (user is null) return Challenge(); var vm = new MedicalRecordCreateViewModel();
        var appointments = _db.Appointments.Where(a => a.Status == AppointmentStatus.Completed && !_db.MedicalRecords.Any(r => r.AppointmentId == a.Id));
        if (await _userManager.IsInRoleAsync(user, SeedData.DoctorRole)) appointments = appointments.Where(a => a.Doctor.ApplicationUserId == user.Id);
        vm.Appointments = await appointments.Include(a => a.Patient).Select(a => new SelectListItem($"#{a.Id} - {a.Patient.FirstName} {a.Patient.LastName} - {a.AppointmentDate:g}", a.Id.ToString())).ToListAsync();
        if (appointmentId.HasValue && await appointments.AnyAsync(a => a.Id == appointmentId.Value)) vm.AppointmentId = appointmentId.Value;
        return View(vm);
    }
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Doctor,Admin")]
    public async Task<IActionResult> Create(MedicalRecordCreateViewModel vm)
    {
        var appointment = await _db.Appointments.Include(a => a.Patient).Include(a => a.Doctor).FirstOrDefaultAsync(a => a.Id == vm.AppointmentId && a.Status == AppointmentStatus.Completed);
        if (appointment is null) ModelState.AddModelError(nameof(vm.AppointmentId), "Select a completed appointment.");
        var user = await _userManager.GetUserAsync(User); if (user is null) return Challenge();
        if (appointment is not null && await _userManager.IsInRoleAsync(user, SeedData.DoctorRole) && appointment.Doctor.ApplicationUserId != user.Id) return Forbid();
        if (!ModelState.IsValid) { await Populate(vm, user); return View(vm); }
        if (await _db.MedicalRecords.AnyAsync(r => r.AppointmentId == appointment!.Id)) { ModelState.AddModelError(nameof(vm.AppointmentId), "This appointment already has a medical record."); await Populate(vm, user); return View(vm); }
        _db.MedicalRecords.Add(new MedicalRecord { AppointmentId = appointment!.Id, PatientId = appointment.PatientId, DoctorId = appointment.DoctorId, VisitDate = appointment.AppointmentDate, Symptoms = vm.Symptoms, Diagnosis = vm.Diagnosis, DoctorNotes = vm.DoctorNotes, FollowUpInstructions = vm.FollowUpInstructions });
        await _db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }
    private async Task Populate(MedicalRecordCreateViewModel vm, ApplicationUser user)
    { var appointments = _db.Appointments.Where(a => a.Status == AppointmentStatus.Completed && !_db.MedicalRecords.Any(r => r.AppointmentId == a.Id)); if (await _userManager.IsInRoleAsync(user, SeedData.DoctorRole)) appointments = appointments.Where(a => a.Doctor.ApplicationUserId == user.Id); vm.Appointments = await appointments.Include(a => a.Patient).Select(a => new SelectListItem($"#{a.Id} - {a.Patient.FirstName} {a.Patient.LastName} - {a.AppointmentDate:g}", a.Id.ToString())).ToListAsync(); }
}
