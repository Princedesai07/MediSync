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
public class AppointmentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AppointmentsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var roles = await _userManager.GetRolesAsync(user);
        IQueryable<Appointment> query = _db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Include(a => a.MedicalRecord)
            .AsNoTracking();

        if (roles.Contains(SeedData.PatientRole))
        {
            query = query.Where(a => a.Patient.ApplicationUserId == user.Id);
        }
        else if (roles.Contains(SeedData.DoctorRole))
        {
            query = query.Where(a => a.Doctor.ApplicationUserId == user.Id);
        }
        else if (roles.Contains(SeedData.LaboratoryRole) || roles.Contains(SeedData.PharmacistRole))
        {
            query = query.Where(a => a.Status == AppointmentStatus.Completed);
        }
        else if (!roles.Any(r => r is SeedData.AdminRole or SeedData.ReceptionistRole))
        {
            return Forbid();
        }

        return View(await query.OrderByDescending(a => a.AppointmentDate).ToListAsync());
    }

    [Authorize(Roles = "Admin,Receptionist,Patient")]
    public async Task<IActionResult> Create()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var roles = await _userManager.GetRolesAsync(user);
        var vm = new AppointmentCreateViewModel();
        vm.AppointmentDay = DateTime.Today.AddDays(1);
        vm.AppointmentTime = "09:00";
        vm.AppointmentDate = vm.AppointmentDay.Date.AddHours(9);

        if (roles.Contains(SeedData.PatientRole))
        {
            var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.ApplicationUserId == user.Id);
            if (patient is null) return NotFound("Your patient profile could not be found.");
            vm.PatientId = patient.Id;
            vm.PatientName = patient.FullName;
        }
        else
        {
            vm.Patients = await GetActivePatientListAsync();
        }

        vm.Doctors = await GetActiveDoctorListAsync();

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Receptionist,Patient")]
    public async Task<IActionResult> Create(AppointmentCreateViewModel vm)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var roles = await _userManager.GetRolesAsync(user);

        // Appointment slots are fixed 15-minute intervals. Compose the server-side
        // appointment timestamp from the separate date and time controls.
        if (!TimeSpan.TryParseExact(vm.AppointmentTime, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out var selectedTime))
        {
            ModelState.AddModelError(nameof(vm.AppointmentTime), "Select a valid appointment time.");
            selectedTime = TimeSpan.Zero;
        }

        if (selectedTime.Minutes % 15 != 0 || selectedTime.Seconds != 0)
            ModelState.AddModelError(nameof(vm.AppointmentTime), "Appointments can only be booked in 15-minute intervals.");

        vm.AppointmentDate = vm.AppointmentDay.Date.Add(selectedTime);

        if (roles.Contains(SeedData.PatientRole))
        {
            var patient = await _db.Patients.FirstOrDefaultAsync(p => p.ApplicationUserId == user.Id);
            if (patient is null) return NotFound("Your patient profile could not be found.");
            vm.PatientId = patient.Id;
            vm.PatientName = patient.FullName;
        }

        if (vm.AppointmentDate <= DateTime.Now)
            ModelState.AddModelError(nameof(vm.AppointmentDay), "Appointment date and time must be in the future.");

        var selectedPatient = await _db.Patients
            .Include(p => p.ApplicationUser)
            .FirstOrDefaultAsync(p => p.Id == vm.PatientId);
        if (selectedPatient is null)
            ModelState.AddModelError(nameof(vm.PatientId), "Select a valid patient.");
        else if (!IsActive(selectedPatient.ApplicationUser))
            ModelState.AddModelError(nameof(vm.PatientId), "This patient account is inactive and cannot receive new appointments.");

        var doctor = await _db.Doctors
            .Include(d => d.ApplicationUser)
            .FirstOrDefaultAsync(d => d.Id == vm.DoctorId);

        if (doctor is null)
            ModelState.AddModelError(nameof(vm.DoctorId), "Select a valid doctor.");
        else if (!IsActive(doctor.ApplicationUser))
            ModelState.AddModelError(nameof(vm.DoctorId), "This doctor is currently inactive and cannot receive new appointments.");
        else if (!IsWithinDoctorAvailability(doctor.Availability, vm.AppointmentDate, out var availabilityError))
            ModelState.AddModelError(nameof(vm.AppointmentTime), availabilityError);

        if (roles.Contains(SeedData.PatientRole) &&
            !await _db.Patients.AnyAsync(p => p.Id == vm.PatientId && p.ApplicationUserId == user.Id))
        {
            ModelState.AddModelError(nameof(vm.PatientId), "Patients can only book appointments for themselves.");
        }

        // Appointment slots are fixed 15-minute intervals. All new appointments are
        // normalized to HH:mm and must be a quarter-hour. The one-minute range below
        // also catches legacy rows that may still contain seconds/milliseconds.
        var slotStart = vm.AppointmentDate;
        var slotEnd = slotStart.AddMinutes(1);
        var slotTaken = await _db.Appointments.AnyAsync(a =>
            a.AppointmentDate >= slotStart &&
            a.AppointmentDate < slotEnd &&
            a.Status != AppointmentStatus.Cancelled &&
            a.Status != AppointmentStatus.Rejected &&
            (a.DoctorId == vm.DoctorId || a.PatientId == vm.PatientId));

        if (slotTaken)
            ModelState.AddModelError(nameof(vm.AppointmentTime), "This doctor or patient already has an active appointment in this 15-minute time slot.");

        if (!ModelState.IsValid)
        {
            await PopulateLists(vm, roles.Contains(SeedData.PatientRole));
            return View(vm);
        }

        _db.Appointments.Add(new Appointment
        {
            PatientId = vm.PatientId,
            DoctorId = vm.DoctorId,
            AppointmentDate = vm.AppointmentDate,
            Reason = vm.Reason?.Trim(),
            Status = AppointmentStatus.Pending
        });

        await _db.SaveChangesAsync();
        TempData["Message"] = "Appointment created and is now Pending.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Doctor,Receptionist,Patient")]
    public async Task<IActionResult> ChangeStatus(int id, AppointmentStatus status)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment is null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var roles = await _userManager.GetRolesAsync(user);
        var isAdmin = roles.Contains(SeedData.AdminRole);
        var isDoctor = roles.Contains(SeedData.DoctorRole);
        var isReceptionist = roles.Contains(SeedData.ReceptionistRole);
        var isPatient = roles.Contains(SeedData.PatientRole);

        if (isDoctor && appointment.Doctor.ApplicationUserId != user.Id) return Forbid();
        if (isPatient && appointment.Patient.ApplicationUserId != user.Id) return Forbid();

        var allowed = false;

        if (isAdmin)
        {
            allowed = (appointment.Status, status) switch
            {
                (AppointmentStatus.Pending, AppointmentStatus.Confirmed) => true,
                (AppointmentStatus.Pending, AppointmentStatus.Rejected) => true,
                (AppointmentStatus.Pending, AppointmentStatus.Cancelled) => true,
                (AppointmentStatus.Confirmed, AppointmentStatus.Completed) => true,
                (AppointmentStatus.Confirmed, AppointmentStatus.Cancelled) => true,
                _ => false
            };
        }
        else if (isDoctor)
        {
            allowed = (appointment.Status, status) switch
            {
                (AppointmentStatus.Pending, AppointmentStatus.Confirmed) => true,
                (AppointmentStatus.Confirmed, AppointmentStatus.Completed) => true,
                (AppointmentStatus.Pending, AppointmentStatus.Rejected) => true,
                (AppointmentStatus.Pending, AppointmentStatus.Cancelled) => true,
                (AppointmentStatus.Confirmed, AppointmentStatus.Cancelled) => true,
                _ => false
            };
        }
        else if (isReceptionist)
        {
            allowed = (appointment.Status, status) switch
            {
                (AppointmentStatus.Pending, AppointmentStatus.Cancelled) => true,
                (AppointmentStatus.Confirmed, AppointmentStatus.Cancelled) => true,
                _ => false
            };
        }
        else if (isPatient)
        {
            allowed = status == AppointmentStatus.Cancelled &&
                      appointment.Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed;
        }

        if (!allowed)
        {
            TempData["Error"] = "That appointment status transition is not permitted for your role.";
            return RedirectToAction(nameof(Index));
        }

        appointment.Status = status;
        await _db.SaveChangesAsync();
        TempData["Message"] = $"Appointment #{appointment.Id} is now {appointment.Status}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<SelectListItem>> GetActivePatientListAsync()
    {
        var now = DateTimeOffset.UtcNow;
        return await _db.Patients
            .Where(p => p.ApplicationUserId != null &&
                        p.ApplicationUser != null &&
                        (!p.ApplicationUser.LockoutEnabled || p.ApplicationUser.LockoutEnd == null || p.ApplicationUser.LockoutEnd <= now))
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Select(p => new SelectListItem(p.FullName, p.Id.ToString()))
            .ToListAsync();
    }

    private async Task<List<SelectListItem>> GetActiveDoctorListAsync()
    {
        var now = DateTimeOffset.UtcNow;
        return await _db.Doctors
            .Where(d => d.ApplicationUserId != null &&
                        d.ApplicationUser != null &&
                        (!d.ApplicationUser.LockoutEnabled || d.ApplicationUser.LockoutEnd == null || d.ApplicationUser.LockoutEnd <= now))
            .OrderBy(d => d.LastName).ThenBy(d => d.FirstName)
            .Select(d => new SelectListItem(d.FullName + " - " + d.Specialization, d.Id.ToString()))
            .ToListAsync();
    }

    private static bool IsActive(ApplicationUser? user)
    {
        if (user is null) return false;
        return !user.LockoutEnabled || user.LockoutEnd is null || user.LockoutEnd <= DateTimeOffset.UtcNow;
    }

    private static bool IsWithinDoctorAvailability(string availability, DateTime appointmentDate, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(availability))
        {
            error = "This doctor has no configured availability. Please ask an administrator to configure it first.";
            return false;
        }

        // Availability is stored as a human-friendly value such as
        // "Mon-Fri 09:00-14:00". Normalize whitespace/dash variations before parsing
        // so an otherwise valid schedule is not rejected because of formatting.
        var text = availability.Trim()
            .Replace('–', '-')
            .Replace('—', '-')
            .Replace(" ", string.Empty);

        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            @"^(?<days>[A-Za-z]{3})(?:-(?<endDay>[A-Za-z]{3}))?(?<start>\d{1,2}:\d{2})-(?<end>\d{1,2}:\d{2})$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            error = "Doctor availability must use a format such as 'Mon-Fri 09:00-14:00'.";
            return false;
        }

        if (!TimeSpan.TryParseExact(match.Groups["start"].Value, @"h\:mm", System.Globalization.CultureInfo.InvariantCulture, out var start) ||
            !TimeSpan.TryParseExact(match.Groups["end"].Value, @"h\:mm", System.Globalization.CultureInfo.InvariantCulture, out var end) ||
            start >= end || start.TotalMinutes < 0 || end.TotalMinutes > 24 * 60)
        {
            error = "Doctor availability contains an invalid time range.";
            return false;
        }

        if (!TryDayNumber(match.Groups["days"].Value, out var startDay))
        {
            error = "Doctor availability contains an invalid day.";
            return false;
        }

        var endDay = startDay;
        if (match.Groups["endDay"].Success && !TryDayNumber(match.Groups["endDay"].Value, out endDay))
        {
            error = "Doctor availability contains an invalid ending day.";
            return false;
        }

        var appointmentDay = (int)appointmentDate.DayOfWeek;
        var availableDay = startDay <= endDay
            ? appointmentDay >= startDay && appointmentDay <= endDay
            : appointmentDay >= startDay || appointmentDay <= endDay;

        // AppointmentDate is accepted only to minute precision; ignore stray seconds
        // that may be present when a browser posts a datetime-local value.
        var appointmentTime = new TimeSpan(appointmentDate.Hour, appointmentDate.Minute, 0);
        if (!availableDay || appointmentTime < start || appointmentTime >= end)
        {
            error = $"This doctor is available {availability}. Please choose a date and time within that schedule.";
            return false;
        }

        return true;
    }

    private static bool TryDayNumber(string value, out int day)
    {
        day = value.ToLowerInvariant() switch
        {
            "sun" => 0, "mon" => 1, "tue" => 2, "wed" => 3,
            "thu" => 4, "fri" => 5, "sat" => 6, _ => -1
        };
        return day >= 0;
    }

    private async Task PopulateLists(AppointmentCreateViewModel vm, bool patientOwnBooking)
    {
        if (!patientOwnBooking)
        {
            vm.Patients = await GetActivePatientListAsync();
        }

        vm.Doctors = await GetActiveDoctorListAsync();
    }
}
