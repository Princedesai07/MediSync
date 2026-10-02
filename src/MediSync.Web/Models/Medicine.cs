using System.ComponentModel.DataAnnotations;
namespace MediSync.Web.Models;
public class Medicine
{
    public int Id { get; set; }
    [Required, StringLength(160)] public string Name { get; set; } = string.Empty;
    [StringLength(120)] public string Manufacturer { get; set; } = string.Empty;
    [StringLength(120)] public string Category { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [Range(0, 1000000)] public int StockQuantity { get; set; }
    [Range(0, 1000000)] public int LowStockThreshold { get; set; } = 10;
    [DataType(DataType.Date)] public DateTime ExpiryDate { get; set; }
    [Range(0, 1000000)] public decimal Price { get; set; }
    public ICollection<PrescriptionItem> PrescriptionItems { get; set; } = new List<PrescriptionItem>();
}
