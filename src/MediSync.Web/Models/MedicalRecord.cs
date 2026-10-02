using System.ComponentModel.DataAnnotations;
namespace MediSync.Web.Models;
public class MedicalRecord
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;
    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;
    public DateTime VisitDate { get; set; } = DateTime.UtcNow;
    [StringLength(1000)] public string Symptoms { get; set; } = string.Empty;
    [StringLength(1000)] public string Diagnosis { get; set; } = string.Empty;
    [StringLength(2000)] public string DoctorNotes { get; set; } = string.Empty;
    [StringLength(1000)] public string FollowUpInstructions { get; set; } = string.Empty;
    public ICollection<LabTest> LabTests { get; set; } = new List<LabTest>();
    public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
}
