using System.ComponentModel.DataAnnotations;
namespace MediSync.Web.Models;
public class PrescriptionItem
{
    public int Id { get; set; }
    public int PrescriptionId { get; set; }
    public Prescription Prescription { get; set; } = null!;
    public int MedicineId { get; set; }
    public Medicine Medicine { get; set; } = null!;
    [Required, StringLength(100)] public string Dosage { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Frequency { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Duration { get; set; } = string.Empty;
    [Range(1, 10000)] public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
