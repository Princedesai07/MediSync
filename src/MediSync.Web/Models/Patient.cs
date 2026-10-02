using System.ComponentModel.DataAnnotations;
namespace MediSync.Web.Models;
public class Patient
{
    public int Id { get; set; }
    [Required, StringLength(80)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(80)] public string LastName { get; set; } = string.Empty;
    [DataType(DataType.Date)] public DateTime DateOfBirth { get; set; }
    [StringLength(20)] public string Gender { get; set; } = string.Empty;
    [Phone] public string Phone { get; set; } = string.Empty;
    [EmailAddress] public string? Email { get; set; }
    [StringLength(250)] public string? Address { get; set; }
    [StringLength(120)] public string? EmergencyContact { get; set; }
    public DateTime RegistrationDate { get; set; } = DateTime.UtcNow.Date;
    public string? ApplicationUserId { get; set; }
    public ApplicationUser? ApplicationUser { get; set; }
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
    public ICollection<LabTest> LabTests { get; set; } = new List<LabTest>();
    public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
    public string FullName => $"{FirstName} {LastName}".Trim();
}
