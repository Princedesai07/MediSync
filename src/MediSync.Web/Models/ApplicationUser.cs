using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
namespace MediSync.Web.Models;
public class ApplicationUser : IdentityUser
{
    [Required, StringLength(80)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(80)] public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string FullName => $"{FirstName} {LastName}".Trim();
}
