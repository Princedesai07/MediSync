using MediSync.Web.Data;
using MediSync.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace MediSync.Web.Controllers;

[Authorize(Roles = "Admin,Pharmacist")]
public class MedicinesController : Controller
{
    private readonly ApplicationDbContext _db;
    public MedicinesController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? q, bool lowStock = false)
    {
        var query = _db.Medicines.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(m => m.Name.Contains(q) || m.Category.Contains(q) || m.Manufacturer.Contains(q));
        }
        if (lowStock)
            query = query.Where(m => m.StockQuantity <= m.LowStockThreshold);

        ViewBag.Query = q;
        ViewBag.LowStock = lowStock;
        ViewBag.TotalCount = await _db.Medicines.CountAsync();
        ViewBag.LowStockCount = await _db.Medicines.CountAsync(m => m.StockQuantity <= m.LowStockThreshold);
        ViewBag.ExpiredCount = await _db.Medicines.CountAsync(m => m.ExpiryDate.Date < DateTime.Today);
        ViewBag.ExpiringSoonCount = await _db.Medicines.CountAsync(m => m.ExpiryDate.Date >= DateTime.Today && m.ExpiryDate.Date <= DateTime.Today.AddDays(30));

        return View(await query.OrderBy(m => m.Name).ToListAsync());
    }

    public IActionResult Create() => View(new Medicine { ExpiryDate = DateTime.Today.AddYears(1), LowStockThreshold = 10 });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Medicine model)
    {
        if (!ModelState.IsValid) return View(model);
        _db.Medicines.Add(model);
        await _db.SaveChangesAsync();
        TempData["Message"] = $"Medicine '{model.Name}' added successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var m = await _db.Medicines.FindAsync(id);
        return m is null ? NotFound() : View(m);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Medicine model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);
        _db.Update(model);
        await _db.SaveChangesAsync();
        TempData["Message"] = $"Medicine '{model.Name}' updated successfully.";
        return RedirectToAction(nameof(Index));
    }
}
