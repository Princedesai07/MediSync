using System.ComponentModel.DataAnnotations;
using MediSync.Web.Models;

namespace MediSync.Web.ViewModels;

public class DoctorManagementViewModel
{
    public int Id { get; set; }
    public string? ApplicationUserId { get; set; }

    [Required, StringLength(80)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(80)] public string LastName { get; set; } = string.Empty;
    [Required, StringLength(120)] public string Specialization { get; set; } = string.Empty;
    [StringLength(120)] public string Department { get; set; } = string.Empty;
    [Phone, StringLength(30)] public string Phone { get; set; } = string.Empty;
    [EmailAddress, StringLength(160)] public string? Email { get; set; }
    [StringLength(250)] public string Availability { get; set; } = string.Empty;

    [StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string? AccountPassword { get; set; }
}

public class PatientManagementViewModel
{
    public int Id { get; set; }
    public string? ApplicationUserId { get; set; }

    [Required, StringLength(80)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(80)] public string LastName { get; set; } = string.Empty;
    [DataType(DataType.Date)] public DateTime DateOfBirth { get; set; }
    [StringLength(20)] public string Gender { get; set; } = string.Empty;
    [Phone, StringLength(30)] public string Phone { get; set; } = string.Empty;
    [EmailAddress, StringLength(160)] public string? Email { get; set; }
    [StringLength(250)] public string? Address { get; set; }
    [StringLength(120)] public string? EmergencyContact { get; set; }

    [StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string? AccountPassword { get; set; }
}

public class AdminUserRowViewModel
{
    public required ApplicationUser User { get; init; }
    public required string Role { get; init; }
    public bool IsActive { get; init; }
    public string? ProfileType { get; init; }
}

public class AdminUsersViewModel
{
    public string? Search { get; set; }
    public string? Role { get; set; }
    public string? Status { get; set; }
    public IReadOnlyList<AdminUserRowViewModel> Users { get; set; } = [];
}
