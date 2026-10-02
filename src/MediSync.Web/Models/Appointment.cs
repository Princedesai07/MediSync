using System.ComponentModel.DataAnnotations;
namespace MediSync.Web.Models;
public class Appointment
{
    public int Id { get; set; }
    [Required] public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    [Required] public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;
    [Required] public DateTime AppointmentDate { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    [StringLength(500)] public string? Reason { get; set; }
    public MedicalRecord? MedicalRecord { get; set; }
}
