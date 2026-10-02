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
public class PrescriptionsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public PrescriptionsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    { _db = db; _userManager = userManager; }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(SeedData.ReceptionistRole) || roles.Contains(SeedData.LaboratoryRole)) return Forbid();

        IQueryable<Prescription> q = _db.Prescriptions
            .Include(p => p.Patient).Include(p => p.Doctor)
            .Include(p => p.MedicalRecord)
            .Include(p => p.Items).ThenInclude(i => i.Medicine)
            .AsNoTracking();

        if (roles.Contains(SeedData.PatientRole)) q = q.Where(p => p.Patient.ApplicationUserId == user.Id);
        else if (roles.Contains(SeedData.DoctorRole)) q = q.Where(p => p.Doctor.ApplicationUserId == user.Id);

        return View(await q.OrderByDescending(p => p.CreatedDate).ToListAsync());
    }

    [Authorize(Roles = "Doctor,Admin")]
    public async Task<IActionResult> Create(int? medicalRecordId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var isDoctor = await _userManager.IsInRoleAsync(user, SeedData.DoctorRole);
        var vm = new PrescriptionCreateViewModel();
        var records = _db.MedicalRecords.Include(r => r.Patient).Include(r => r.Doctor).AsQueryable();
        if (isDoctor) records = records.Where(r => r.Doctor.ApplicationUserId == user.Id);

        if (isDoctor && !medicalRecordId.HasValue)
        {
            TempData["Error"] = "Doctors must create prescriptions from one of their medical records.";
            return RedirectToAction("Index", "MedicalRecords");
        }

        if (medicalRecordId.HasValue)
        {
            var record = await records.FirstOrDefaultAsync(r => r.Id == medicalRecordId.Value);
            if (record is null)
            {
                if (isDoctor) return Forbid();
                TempData["Error"] = "Medical record not found.";
                return RedirectToAction(nameof(Index));
            }

            vm.MedicalRecordId = record.Id;
            vm.PatientId = record.PatientId;
            vm.DoctorId = record.DoctorId;
            vm.IsMedicalRecordLocked = isDoctor;
        }

        await Populate(vm, user);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Doctor,Admin")]
    public async Task<IActionResult> Create(PrescriptionCreateViewModel vm)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var isDoctor = await _userManager.IsInRoleAsync(user, SeedData.DoctorRole);
        MedicalRecord? record = null;
        if (vm.MedicalRecordId.HasValue)
        {
            record = await _db.MedicalRecords.Include(r => r.Patient).Include(r => r.Doctor)
                .FirstOrDefaultAsync(r => r.Id == vm.MedicalRecordId.Value);
            if (record is null) ModelState.AddModelError(nameof(vm.MedicalRecordId), "Medical record not found.");
            else
            {
                if (isDoctor && record.Doctor.ApplicationUserId != user.Id) return Forbid();
                vm.PatientId = record.PatientId;
                vm.DoctorId = record.DoctorId;
                vm.IsMedicalRecordLocked = isDoctor;
            }
        }
        else if (isDoctor)
        {
            return Forbid();
        }

        var calculatedQuantity = CalculateQuantity(vm.Dosage, vm.Frequency, vm.Duration);
        if (calculatedQuantity is null)
            ModelState.AddModelError(nameof(vm.Quantity), "Quantity could not be calculated. Use a numeric dosage, a supported frequency, and a duration in days/weeks/months.");
        else
            vm.Quantity = calculatedQuantity.Value;

        var medicine = await _db.Medicines.FindAsync(vm.MedicineId);
        if (medicine is null) ModelState.AddModelError(nameof(vm.MedicineId), "Medicine not found.");
        else if (medicine.ExpiryDate < DateTime.Today || (calculatedQuantity.HasValue && medicine.StockQuantity < calculatedQuantity.Value))
            ModelState.AddModelError(nameof(vm.Quantity), "Medicine is expired or does not have enough stock for the calculated quantity.");

        if (!await _db.Patients.AnyAsync(p => p.Id == vm.PatientId)) ModelState.AddModelError(nameof(vm.PatientId), "Patient not found.");
        if (!ModelState.IsValid) { await Populate(vm, user); return View(vm); }

        var prescription = new Prescription
        {
            PatientId = vm.PatientId, DoctorId = vm.DoctorId, MedicalRecordId = vm.MedicalRecordId,
            Instructions = vm.Instructions, Status = PrescriptionStatus.Pending
        };
        _db.Prescriptions.Add(prescription);
        await _db.SaveChangesAsync();
        _db.PrescriptionItems.Add(new PrescriptionItem
        {
            PrescriptionId = prescription.Id, MedicineId = medicine!.Id, Dosage = vm.Dosage,
            Frequency = vm.Frequency, Duration = vm.Duration, Quantity = vm.Quantity, UnitPrice = medicine.Price
        });
        await _db.SaveChangesAsync();
        TempData["Message"] = "Prescription created and sent to pharmacy.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Pharmacist,Admin")]
    public async Task<IActionResult> StartProcessing(int id)
    {
        var prescription = await _db.Prescriptions.FindAsync(id);
        if (prescription is null) return NotFound();
        if (prescription.Status != PrescriptionStatus.Pending)
        { TempData["Error"] = "Only pending prescriptions can be started."; return RedirectToAction(nameof(Index)); }
        prescription.Status = PrescriptionStatus.Processing;
        await _db.SaveChangesAsync();
        TempData["Message"] = "Prescription moved to processing.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Pharmacist,Admin")]
    public async Task<IActionResult> Dispense(int id)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        var prescription = await _db.Prescriptions.Include(p => p.Items).ThenInclude(i => i.Medicine).FirstOrDefaultAsync(p => p.Id == id);
        if (prescription is null) return NotFound();
        if (prescription.Status == PrescriptionStatus.Dispensed) return RedirectToAction(nameof(Index));
        if (prescription.Status != PrescriptionStatus.Processing)
        { TempData["Error"] = "Start processing the prescription before dispensing."; return RedirectToAction(nameof(Index)); }
        if (prescription.Items.Count == 0 || prescription.Items.Any(i => i.Quantity <= 0 || i.Medicine.ExpiryDate < DateTime.Today || i.Medicine.StockQuantity < i.Quantity))
        { TempData["Error"] = "Insufficient stock, expired medicine, or no valid items."; return RedirectToAction(nameof(Index)); }

        foreach (var item in prescription.Items) item.Medicine.StockQuantity -= item.Quantity;
        prescription.Status = PrescriptionStatus.Dispensed;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        TempData["Message"] = "Prescription dispensed and medicine stock updated.";
        return RedirectToAction(nameof(Index));
    }

    private static int? CalculateQuantity(string dosage, string frequency, string duration)
    {
        if (string.IsNullOrWhiteSpace(dosage) || string.IsNullOrWhiteSpace(frequency) || string.IsNullOrWhiteSpace(duration)) return null;

        var dosageMatch = System.Text.RegularExpressions.Regex.Match(dosage.Trim(), @"^(\d+(?:\.\d+)?)");
        if (!dosageMatch.Success || !decimal.TryParse(dosageMatch.Groups[1].Value, out var dosageAmount) || dosageAmount <= 0) return null;

        decimal dosesPerDay = frequency.Trim().ToLowerInvariant() switch
        {
            "once daily" or "once a day" => 1,
            "twice daily" or "twice a day" => 2,
            "three times daily" or "three times a day" => 3,
            "four times daily" or "four times a day" => 4,
            "every 4 hours" => 6,
            "every 6 hours" => 4,
            "every 8 hours" => 3,
            "every 12 hours" => 2,
            "every 24 hours" => 1,
            _ => 0
        };
        if (dosesPerDay == 0) return null;

        var durationMatch = System.Text.RegularExpressions.Regex.Match(duration.Trim().ToLowerInvariant(), @"^(\d+)\s*(day|days|week|weeks|month|months)$");
        if (!durationMatch.Success || !int.TryParse(durationMatch.Groups[1].Value, out var durationAmount) || durationAmount <= 0) return null;
        var unit = durationMatch.Groups[2].Value;
        decimal days = unit.StartsWith("week") ? durationAmount * 7 : unit.StartsWith("month") ? durationAmount * 30 : durationAmount;

        var total = dosageAmount * dosesPerDay * days;
        if (total <= 0 || total > 10000 || total != decimal.Truncate(total)) return null;
        return (int)total;
    }

    private async Task Populate(PrescriptionCreateViewModel vm, ApplicationUser user)
    {
        var records = _db.MedicalRecords.Include(r => r.Patient).Include(r => r.Doctor).AsQueryable();
        if (await _userManager.IsInRoleAsync(user, SeedData.DoctorRole)) records = records.Where(r => r.Doctor.ApplicationUserId == user.Id);
        vm.MedicalRecords = await records.OrderByDescending(r => r.VisitDate).Select(r => new SelectListItem($"#{r.Id} - {r.Patient.FirstName} {r.Patient.LastName} - {r.VisitDate:dd MMM yyyy}", r.Id.ToString())).ToListAsync();
        vm.Patients = await _db.Patients.OrderBy(p => p.LastName).Select(p => new SelectListItem(p.FullName, p.Id.ToString())).ToListAsync();
        vm.Doctors = await _db.Doctors.OrderBy(d => d.LastName).Select(d => new SelectListItem(d.FullName, d.Id.ToString())).ToListAsync();
        var canViewStock = await _userManager.IsInRoleAsync(user, SeedData.PharmacistRole) || await _userManager.IsInRoleAsync(user, SeedData.AdminRole);
        vm.Medicines = canViewStock
            ? await _db.Medicines.Where(m => m.StockQuantity > 0 && m.ExpiryDate >= DateTime.Today).OrderBy(m => m.Name).Select(m => new SelectListItem($"{m.Name} (Stock: {m.StockQuantity})", m.Id.ToString())).ToListAsync()
            : await _db.Medicines.Where(m => m.StockQuantity > 0 && m.ExpiryDate >= DateTime.Today).OrderBy(m => m.Name).Select(m => new SelectListItem(m.Name, m.Id.ToString())).ToListAsync();
    }
}
