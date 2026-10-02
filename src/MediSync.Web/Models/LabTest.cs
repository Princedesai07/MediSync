using System.ComponentModel.DataAnnotations;
namespace MediSync.Web.Models;
public class LabTest
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;
    public int? MedicalRecordId { get; set; }
    public MedicalRecord? MedicalRecord { get; set; }
    [Required, StringLength(160)] public string TestName { get; set; } = string.Empty;
    public LabTestStatus Status { get; set; } = LabTestStatus.Requested;
    public DateTime RequestedDate { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedDate { get; set; }
    [StringLength(2000)] public string? Result { get; set; }
    [StringLength(1000)] public string? ReportNotes { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
}
