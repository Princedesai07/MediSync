using MediSync.Web.Data;
using MediSync.Web.Models;
using MediSync.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace MediSync.Web.Controllers;
[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db; private readonly UserManager<ApplicationUser> _userManager;
    public DashboardController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) { _db = db; _userManager = userManager; }
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User); if (user is null) return Challenge();
        var roles = await _userManager.GetRolesAsync(user); var role = roles.FirstOrDefault();
        var vm = new DashboardViewModel
        {
            TotalPatients = await _db.Patients.CountAsync(), TotalDoctors = await _db.Doctors.CountAsync(),
            TodaysAppointments = await _db.Appointments.CountAsync(a => a.AppointmentDate.Date == DateTime.Today),
            PendingAppointments = await _db.Appointments.CountAsync(a => a.Status == AppointmentStatus.Pending),
            CompletedAppointments = await _db.Appointments.CountAsync(a => a.Status == AppointmentStatus.Completed),
            PendingLabTests = await _db.LabTests.CountAsync(l => l.Status != LabTestStatus.Completed && l.Status != LabTestStatus.Cancelled),
            CompletedLabTests = await _db.LabTests.CountAsync(l => l.Status == LabTestStatus.Completed),
            TotalMedicines = await _db.Medicines.CountAsync(), LowStockMedicines = await _db.Medicines.CountAsync(m => m.StockQuantity <= m.LowStockThreshold),
            TotalMedicalRecords = await _db.MedicalRecords.CountAsync(), TotalPrescriptions = await _db.Prescriptions.CountAsync(),
            PendingPrescriptions = await _db.Prescriptions.CountAsync(p => p.Status != PrescriptionStatus.Dispensed && p.Status != PrescriptionStatus.Cancelled),
            ProcessingPrescriptions = await _db.Prescriptions.CountAsync(p => p.Status == PrescriptionStatus.Processing),
            DispensedPrescriptions = await _db.Prescriptions.CountAsync(p => p.Status == PrescriptionStatus.Dispensed),
            InProgressLabTests = await _db.LabTests.CountAsync(l => l.Status == LabTestStatus.InProgress),
            ExpiredMedicines = await _db.Medicines.CountAsync(m => m.ExpiryDate.Date < DateTime.Today),
            UpcomingAppointments = await _db.Appointments.CountAsync(a => a.AppointmentDate >= DateTime.Now && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Rejected)
        };
        if (role == SeedData.DoctorRole)
        {
            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.ApplicationUserId == user.Id);
            if (doctor is not null)
            {
                vm.TodaysAppointments = await _db.Appointments.CountAsync(a => a.DoctorId == doctor.Id && a.AppointmentDate.Date == DateTime.Today);
                vm.PendingAppointments = await _db.Appointments.CountAsync(a => a.DoctorId == doctor.Id && a.Status == AppointmentStatus.Pending);
                vm.CompletedAppointments = await _db.Appointments.CountAsync(a => a.DoctorId == doctor.Id && a.Status == AppointmentStatus.Completed);
                vm.PendingLabTests = await _db.LabTests.CountAsync(l => l.DoctorId == doctor.Id && l.Status != LabTestStatus.Completed && l.Status != LabTestStatus.Cancelled);
                vm.InProgressLabTests = await _db.LabTests.CountAsync(l => l.DoctorId == doctor.Id && l.Status == LabTestStatus.InProgress);
                vm.TotalPrescriptions = await _db.Prescriptions.CountAsync(p => p.DoctorId == doctor.Id);
                vm.TotalMedicalRecords = await _db.MedicalRecords.CountAsync(r => r.DoctorId == doctor.Id);
                vm.TotalPatients = await _db.Appointments.Where(a => a.DoctorId == doctor.Id).Select(a => a.PatientId).Distinct().CountAsync();
                vm.UpcomingAppointments = await _db.Appointments.CountAsync(a => a.DoctorId == doctor.Id && a.AppointmentDate >= DateTime.Now && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Rejected);
            }
        }
        else if (role == SeedData.PatientRole)
        {
            var patient = await _db.Patients.FirstOrDefaultAsync(p => p.ApplicationUserId == user.Id);
            if (patient is not null)
            {
                var now = DateTime.Now;
                var appointments = _db.Appointments.Where(a => a.PatientId == patient.Id);
                vm.MyName = patient.FullName;
                vm.MyEmail = patient.Email;
                vm.MyPhone = patient.Phone;
                vm.MyGender = patient.Gender;
                vm.MyDateOfBirth = patient.DateOfBirth;
                vm.MyEmergencyContact = patient.EmergencyContact;
                vm.MyAddress = patient.Address;
                vm.MyRegistrationDate = patient.RegistrationDate;
                vm.MyAppointments = await appointments.CountAsync();
                vm.MyMedicalRecords = await _db.MedicalRecords.CountAsync(r => r.PatientId == patient.Id);
                vm.MyLabResults = await _db.LabTests.CountAsync(l => l.PatientId == patient.Id && l.Status == LabTestStatus.Completed);
                vm.MyPrescriptions = await _db.Prescriptions.CountAsync(p => p.PatientId == patient.Id);
                var next = await appointments.Where(a => a.AppointmentDate >= now && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Rejected).OrderBy(a => a.AppointmentDate).Include(a => a.Doctor).FirstOrDefaultAsync();
                vm.NextAppointment = next is null ? "No upcoming appointment" : $"{next.AppointmentDate:dd MMM yyyy HH:mm} · {next.Doctor.FullName}";
            }
        }
        else if (role == SeedData.ReceptionistRole)
        {
            vm.TodaysAppointments = await _db.Appointments.CountAsync(a => a.AppointmentDate.Date == DateTime.Today && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Rejected);
            vm.PendingAppointments = await _db.Appointments.CountAsync(a => a.Status == AppointmentStatus.Pending);
            vm.UpcomingAppointments = await _db.Appointments.CountAsync(a => a.AppointmentDate >= DateTime.Now && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Rejected);
        }
        else if (role == SeedData.LaboratoryRole)
        {
            vm.TodaysAppointments = await _db.LabTests.CountAsync(l => l.RequestedDate.Date == DateTime.Today);
            vm.PendingLabTests = await _db.LabTests.CountAsync(l => l.Status == LabTestStatus.Requested);
            vm.InProgressLabTests = await _db.LabTests.CountAsync(l => l.Status == LabTestStatus.InProgress);
            vm.CompletedLabTests = await _db.LabTests.CountAsync(l => l.Status == LabTestStatus.Completed);
        }
        else if (role == SeedData.PharmacistRole)
        {
            vm.PendingPrescriptions = await _db.Prescriptions.CountAsync(p => p.Status == PrescriptionStatus.Pending);
            vm.ProcessingPrescriptions = await _db.Prescriptions.CountAsync(p => p.Status == PrescriptionStatus.Processing);
            vm.DispensedPrescriptions = await _db.Prescriptions.CountAsync(p => p.Status == PrescriptionStatus.Dispensed);
            vm.TotalMedicines = await _db.Medicines.CountAsync();
            vm.LowStockMedicines = await _db.Medicines.CountAsync(m => m.StockQuantity <= m.LowStockThreshold);
            vm.ExpiredMedicines = await _db.Medicines.CountAsync(m => m.ExpiryDate.Date < DateTime.Today);
        }
        return role switch { SeedData.PatientRole => View("Patient", vm), SeedData.DoctorRole => View("Doctor", vm), SeedData.ReceptionistRole => View("Receptionist", vm), SeedData.LaboratoryRole => View("Laboratory", vm), SeedData.PharmacistRole => View("Pharmacist", vm), _ => View("Admin", vm) };
    }
}
