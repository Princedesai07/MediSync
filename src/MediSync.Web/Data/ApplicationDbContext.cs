using MediSync.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MediSync.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<LabTest> LabTests => Set<LabTest>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<Medicine> Medicines => Set<Medicine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Patient>().HasOne(p => p.ApplicationUser).WithMany().HasForeignKey(p => p.ApplicationUserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Doctor>().HasOne(d => d.ApplicationUser).WithMany().HasForeignKey(d => d.ApplicationUserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Appointment>().HasOne(a => a.Patient).WithMany(p => p.Appointments).HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Appointment>().HasOne(a => a.Doctor).WithMany(d => d.Appointments).HasForeignKey(a => a.DoctorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MedicalRecord>().HasOne(r => r.Appointment).WithOne(a => a.MedicalRecord).HasForeignKey<MedicalRecord>(r => r.AppointmentId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<MedicalRecord>().HasOne(r => r.Patient).WithMany(p => p.MedicalRecords).HasForeignKey(r => r.PatientId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MedicalRecord>().HasOne(r => r.Doctor).WithMany(d => d.MedicalRecords).HasForeignKey(r => r.DoctorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LabTest>().HasOne(l => l.Patient).WithMany(p => p.LabTests).HasForeignKey(l => l.PatientId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LabTest>().HasOne(l => l.Doctor).WithMany(d => d.LabTests).HasForeignKey(l => l.DoctorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LabTest>().HasOne(l => l.MedicalRecord).WithMany(r => r.LabTests).HasForeignKey(l => l.MedicalRecordId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Prescription>().HasOne(p => p.Patient).WithMany(p => p.Prescriptions).HasForeignKey(p => p.PatientId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Prescription>().HasOne(p => p.Doctor).WithMany(d => d.Prescriptions).HasForeignKey(p => p.DoctorId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Prescription>().HasOne(p => p.MedicalRecord).WithMany(r => r.Prescriptions).HasForeignKey(p => p.MedicalRecordId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PrescriptionItem>().HasOne(i => i.Prescription).WithMany(p => p.Items).HasForeignKey(i => i.PrescriptionId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PrescriptionItem>().HasOne(i => i.Medicine).WithMany(m => m.PrescriptionItems).HasForeignKey(i => i.MedicineId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Medicine>().Property(m => m.Price).HasPrecision(18, 2);
        modelBuilder.Entity<PrescriptionItem>().Property(i => i.UnitPrice).HasPrecision(18, 2);
    }
}
