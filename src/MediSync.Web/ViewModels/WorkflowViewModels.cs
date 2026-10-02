using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
namespace MediSync.Web.ViewModels;
public class AppointmentCreateViewModel
{
    [Required] public int PatientId { get; set; }
    public string? PatientName { get; set; }
    [Required] public int DoctorId { get; set; }
    [Required, DataType(DataType.Date)] public DateTime AppointmentDay { get; set; } = DateTime.Today.AddDays(1);
    [Required] public string AppointmentTime { get; set; } = "09:00";
    // Server-composed date/time used by the appointment workflow.
    public DateTime AppointmentDate { get; set; }
    [StringLength(500)] public string? Reason { get; set; }
    public IEnumerable<SelectListItem> Patients { get; set; } = [];
    public IEnumerable<SelectListItem> Doctors { get; set; } = [];
}
public class MedicalRecordCreateViewModel
{
    [Required] public int AppointmentId { get; set; }
    [Required] public string Symptoms { get; set; } = string.Empty;
    [Required] public string Diagnosis { get; set; } = string.Empty;
    public string DoctorNotes { get; set; } = string.Empty;
    public string FollowUpInstructions { get; set; } = string.Empty;
    public IEnumerable<SelectListItem> Appointments { get; set; } = [];
}
public class LabTestCreateViewModel
{
    [Required] public int PatientId { get; set; }
    [Required] public int DoctorId { get; set; }
    public int? MedicalRecordId { get; set; }
    [Required, StringLength(160)] public string TestName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public IEnumerable<SelectListItem> Patients { get; set; } = [];
    public IEnumerable<SelectListItem> Doctors { get; set; } = [];
    public IEnumerable<SelectListItem> MedicalRecords { get; set; } = [];
}
public class PrescriptionCreateViewModel
{
    [Required] public int PatientId { get; set; }
    [Required] public int DoctorId { get; set; }
    public int? MedicalRecordId { get; set; }
    public string? Instructions { get; set; }
    [Required] public int MedicineId { get; set; }
    [Required] public string Dosage { get; set; } = "1 tablet";
    [Required] public string Frequency { get; set; } = "Once daily";
    [Required] public string Duration { get; set; } = "3 days";
    [Range(1, 10000)] public int Quantity { get; set; } = 1;
    public IEnumerable<SelectListItem> Patients { get; set; } = [];
    public IEnumerable<SelectListItem> Doctors { get; set; } = [];
    public IEnumerable<SelectListItem> MedicalRecords { get; set; } = [];
    public IEnumerable<SelectListItem> Medicines { get; set; } = [];
    public bool IsMedicalRecordLocked { get; set; }
}
