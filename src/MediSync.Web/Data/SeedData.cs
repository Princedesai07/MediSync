using MediSync.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediSync.Web.Data;

public static class SeedData
{
    public const string AdminRole = "Admin";
    public const string DoctorRole = "Doctor";
    public const string ReceptionistRole = "Receptionist";
    public const string LaboratoryRole = "Laboratory";
    public const string PharmacistRole = "Pharmacist";
    public const string PatientRole = "Patient";

    public static async Task InitializeAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<ApplicationDbContext>();
        foreach (var role in new[] { AdminRole, DoctorRole, ReceptionistRole, LaboratoryRole, PharmacistRole, PatientRole })
            if (!await roleManager.RoleExistsAsync(role)) await roleManager.CreateAsync(new IdentityRole(role));

        var doctorUser = await EnsureUserAsync(userManager, "doctor@medisync.local", "Aarav", "Shah", DoctorRole);
        _ = await EnsureUserAsync(userManager, "admin@medisync.local", "System", "Admin", AdminRole);
        _ = await EnsureUserAsync(userManager, "reception@medisync.local", "Riya", "Patel", ReceptionistRole);
        _ = await EnsureUserAsync(userManager, "lab@medisync.local", "Neel", "Joshi", LaboratoryRole);
        _ = await EnsureUserAsync(userManager, "pharmacy@medisync.local", "Meera", "Desai", PharmacistRole);
        var patientUser = await EnsureSeedPatientUserAsync(db, userManager);
        await RepairPatientIdentityLinksAsync(db, userManager);

        var doctor = await db.Doctors.FirstOrDefaultAsync(d => d.ApplicationUserId == doctorUser.Id);
        if (doctor is null)
        {
            doctor = new Doctor { ApplicationUserId = doctorUser.Id, FirstName = "Aarav", LastName = "Shah", Specialization = "General Medicine", Department = "Outpatient", Phone = "9000000001", Email = doctorUser.Email, Availability = "Mon-Fri 09:00-14:00" };
            db.Doctors.Add(doctor);
        }
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.ApplicationUserId == patientUser.Id);
        if (patient is null)
        {
            patient = new Patient { ApplicationUserId = patientUser.Id, FirstName = "Kunal", LastName = "Desai", DateOfBirth = new DateTime(2002, 4, 18), Gender = "Male", Phone = "9000000002", Email = patientUser.Email, Address = "Nadiad, Gujarat", EmergencyContact = "9000000003", RegistrationDate = DateTime.UtcNow.Date };
            db.Patients.Add(patient);
        }
        await db.SaveChangesAsync();

        if (!await db.Medicines.AnyAsync())
        {
            db.Medicines.AddRange(
                new Medicine { Name = "Paracetamol 500mg", Manufacturer = "MediCare Labs", Category = "Analgesic", Description = "Pain and fever relief", StockQuantity = 120, LowStockThreshold = 20, ExpiryDate = DateTime.UtcNow.Date.AddMonths(18), Price = 2.50m },
                new Medicine { Name = "Cetirizine 10mg", Manufacturer = "HealthFirst", Category = "Antihistamine", Description = "Allergy symptom relief", StockQuantity = 60, LowStockThreshold = 10, ExpiryDate = DateTime.UtcNow.Date.AddMonths(14), Price = 3.25m },
                new Medicine { Name = "Pantoprazole 40mg", Manufacturer = "Wellness Pharma", Category = "Gastro", Description = "Acid reduction", StockQuantity = 15, LowStockThreshold = 20, ExpiryDate = DateTime.UtcNow.Date.AddMonths(10), Price = 4.00m });
            await db.SaveChangesAsync();
        }

        if (!await db.Appointments.AnyAsync())
        {
            db.Appointments.Add(new Appointment { PatientId = patient.Id, DoctorId = doctor.Id, AppointmentDate = DateTime.Today.AddHours(10), Status = AppointmentStatus.Completed, Reason = "Fever and headache" });
            await db.SaveChangesAsync();
        }
        var appointment = await db.Appointments.OrderBy(a => a.Id).FirstAsync();
        if (!await db.MedicalRecords.AnyAsync())
        {
            db.MedicalRecords.Add(new MedicalRecord { PatientId = patient.Id, DoctorId = doctor.Id, AppointmentId = appointment.Id, VisitDate = appointment.AppointmentDate, Symptoms = "Fever, mild headache", Diagnosis = "Viral fever - preliminary", DoctorNotes = "Rest, fluids and monitor temperature.", FollowUpInstructions = "Review if symptoms persist beyond 3 days." });
            await db.SaveChangesAsync();
        }
        var record = await db.MedicalRecords.OrderBy(r => r.Id).FirstAsync();
        if (!await db.LabTests.AnyAsync())
        {
            db.LabTests.Add(new LabTest { PatientId = patient.Id, DoctorId = doctor.Id, MedicalRecordId = record.Id, TestName = "CBC", Status = LabTestStatus.Requested, RequestedDate = DateTime.UtcNow, Notes = "Routine blood count" });
            await db.SaveChangesAsync();
        }
        if (!await db.Prescriptions.AnyAsync())
        {
            var medicine = await db.Medicines.FirstAsync(m => m.Name.StartsWith("Paracetamol"));
            var prescription = new Prescription { PatientId = patient.Id, DoctorId = doctor.Id, MedicalRecordId = record.Id, CreatedDate = DateTime.UtcNow, Status = PrescriptionStatus.Pending, Instructions = "Take after food; hydrate well." };
            db.Prescriptions.Add(prescription);
            await db.SaveChangesAsync();
            db.PrescriptionItems.Add(new PrescriptionItem { PrescriptionId = prescription.Id, MedicineId = medicine.Id, Dosage = "1 tablet", Frequency = "Twice daily", Duration = "3 days", Quantity = 6, UnitPrice = medicine.Price });
            await db.SaveChangesAsync();
        }

        // Populate a richer, repeatable development/demo dataset. This is intentionally
        // separate from the original baseline seed so existing student-created data
        // is not overwritten or duplicated on every application start.
        await EnsureRichDemoDataAsync(db, userManager);
    }

    private static async Task EnsureRichDemoDataAsync(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        const string demoMarkerEmail = "dr.meera@medisync.local";
        if (await userManager.FindByEmailAsync(demoMarkerEmail) is not null)
            return;

        var today = DateTime.Today;
        var password = "MediSync@123";

        async Task<ApplicationUser> CreateDemoUserAsync(string email, string firstName, string lastName, string role)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = firstName,
                    LastName = lastName
                };
                var result = await userManager.CreateAsync(user, password);
                if (!result.Succeeded)
                    throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                var roleResult = await userManager.AddToRoleAsync(user, role);
                if (!roleResult.Succeeded)
                    throw new InvalidOperationException(string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            }

            return user;
        }

        // Demo operational users
        var receptionistUsers = new[]
        {
            ("reception2@medisync.local", "Anaya", "Shah"),
            ("reception3@medisync.local", "Vivek", "Patel")
        };
        foreach (var item in receptionistUsers)
            await CreateDemoUserAsync(item.Item1, item.Item2, item.Item3, ReceptionistRole);

        var laboratoryUsers = new[]
        {
            ("lab2@medisync.local", "Ishita", "Mehta"),
            ("lab3@medisync.local", "Kabir", "Joshi")
        };
        foreach (var item in laboratoryUsers)
            await CreateDemoUserAsync(item.Item1, item.Item2, item.Item3, LaboratoryRole);

        var pharmacistUsers = new[]
        {
            ("pharmacy2@medisync.local", "Nisha", "Shah"),
            ("pharmacy3@medisync.local", "Harsh", "Patel")
        };
        foreach (var item in pharmacistUsers)
            await CreateDemoUserAsync(item.Item1, item.Item2, item.Item3, PharmacistRole);

        // Demo doctors
        var doctorSeeds = new[]
        {
            ("dr.meera@medisync.local", "Meera", "Patel", "Cardiology", "Cardiology", "Mon-Fri 09:00-14:00", "9000100001"),
            ("dr.rohan@medisync.local", "Rohan", "Desai", "Orthopedics", "Orthopedics", "Mon-Fri 10:00-15:00", "9000100002"),
            ("dr.kavya@medisync.local", "Kavya", "Shah", "Dermatology", "Dermatology", "Mon-Fri 09:00-13:00", "9000100003"),
            ("dr.neel@medisync.local", "Neel", "Joshi", "Pediatrics", "Pediatrics", "Mon-Sat 10:00-14:00", "9000100004"),
            ("dr.isha@medisync.local", "Isha", "Trivedi", "ENT", "ENT", "Tue-Sat 09:00-13:00", "9000100005")
        };

        var demoDoctors = new List<Doctor>();
        foreach (var item in doctorSeeds)
        {
            var user = await CreateDemoUserAsync(item.Item1, item.Item2, item.Item3, DoctorRole);
            var doctor = await db.Doctors.FirstOrDefaultAsync(d => d.ApplicationUserId == user.Id);
            if (doctor is null)
            {
                doctor = new Doctor
                {
                    ApplicationUserId = user.Id,
                    FirstName = item.Item2,
                    LastName = item.Item3,
                    Specialization = item.Item4,
                    Department = item.Item5,
                    Availability = item.Item6,
                    Phone = item.Item7,
                    Email = item.Item1
                };
                db.Doctors.Add(doctor);
                await db.SaveChangesAsync();
            }
            demoDoctors.Add(doctor);
        }

        // Demo patients
        var patientSeeds = new[]
        {
            ("Aarohi", "Mehta", new DateTime(1998, 2, 14), "Female", "9010000001", "Ahmedabad, Gujarat"),
            ("Dhruv", "Patel", new DateTime(1995, 7, 21), "Male", "9010000002", "Nadiad, Gujarat"),
            ("Mira", "Shah", new DateTime(2001, 11, 3), "Female", "9010000003", "Vadodara, Gujarat"),
            ("Yash", "Trivedi", new DateTime(1989, 5, 19), "Male", "9010000004", "Anand, Gujarat"),
            ("Diya", "Joshi", new DateTime(2010, 8, 9), "Female", "9010000005", "Nadiad, Gujarat"),
            ("Arjun", "Mehta", new DateTime(1978, 1, 27), "Male", "9010000006", "Ahmedabad, Gujarat"),
            ("Niyati", "Desai", new DateTime(1992, 12, 11), "Female", "9010000007", "Kheda, Gujarat"),
            ("Manav", "Shah", new DateTime(2004, 3, 6), "Male", "9010000008", "Vadodara, Gujarat"),
            ("Riya", "Patel", new DateTime(1986, 9, 24), "Female", "9010000009", "Anand, Gujarat"),
            ("Kunal", "Mehta", new DateTime(1971, 6, 15), "Male", "9010000010", "Nadiad, Gujarat"),
            ("Tanvi", "Joshi", new DateTime(1999, 10, 30), "Female", "9010000011", "Ahmedabad, Gujarat"),
            ("Aditya", "Rana", new DateTime(1990, 4, 2), "Male", "9010000012", "Nadiad, Gujarat")
        };

        var demoPatients = new List<Patient>();
        for (var i = 0; i < patientSeeds.Length; i++)
        {
            var item = patientSeeds[i];
            var email = $"patient.demo{i + 1}@medisync.local";
            var user = await CreateDemoUserAsync(email, item.Item1, item.Item2, PatientRole);
            var patient = await db.Patients.FirstOrDefaultAsync(p => p.ApplicationUserId == user.Id);
            if (patient is null)
            {
                patient = new Patient
                {
                    ApplicationUserId = user.Id,
                    FirstName = item.Item1,
                    LastName = item.Item2,
                    DateOfBirth = item.Item3,
                    Gender = item.Item4,
                    Phone = item.Item5,
                    Email = email,
                    Address = item.Item6,
                    EmergencyContact = $"90200000{i + 1:00}",
                    RegistrationDate = today.AddDays(-(i + 2))
                };
                db.Patients.Add(patient);
                await db.SaveChangesAsync();
            }
            demoPatients.Add(patient);
        }

        // Medicines: enough variety for pharmacy screenshots, including a low-stock item.
        var medicineSeeds = new[]
        {
            ("Amoxicillin 500mg", "CureWell", "Antibiotic", "Antibacterial medicine", 80, 20, 24, 5.50m),
            ("Ibuprofen 400mg", "MediCare Labs", "Analgesic", "Pain and inflammation relief", 45, 15, 20, 4.75m),
            ("Azithromycin 500mg", "HealthFirst", "Antibiotic", "Antibiotic medicine", 32, 10, 16, 8.25m),
            ("ORS Sachet", "HydraCare", "Electrolyte", "Oral rehydration support", 150, 30, 30, 1.50m),
            ("Vitamin D3 60000 IU", "Wellness Pharma", "Supplement", "Vitamin D supplement", 25, 10, 18, 12.00m),
            ("Metformin 500mg", "GlucoCare", "Antidiabetic", "Blood glucose management medicine", 70, 20, 22, 3.80m),
            ("Amlodipine 5mg", "CardioLife", "Antihypertensive", "Blood pressure medicine", 18, 20, 14, 4.20m),
            ("Omeprazole 20mg", "Wellness Pharma", "Gastro", "Acid reduction medicine", 55, 15, 21, 3.60m),
            ("Cough Syrup 100ml", "Respira", "Respiratory", "Cough relief syrup", 40, 10, 12, 6.50m),
            ("Antiseptic Solution 100ml", "SafeCare", "Antiseptic", "Topical antiseptic solution", 35, 10, 26, 5.00m)
        };

        var demoMedicines = new List<Medicine>();
        foreach (var item in medicineSeeds)
        {
            var medicine = await db.Medicines.FirstOrDefaultAsync(m => m.Name == item.Item1);
            if (medicine is null)
            {
                medicine = new Medicine
                {
                    Name = item.Item1, Manufacturer = item.Item2, Category = item.Item3, Description = item.Item4,
                    StockQuantity = item.Item5, LowStockThreshold = item.Item6, ExpiryDate = today.AddMonths(item.Item7), Price = item.Item8
                };
                db.Medicines.Add(medicine);
                await db.SaveChangesAsync();
            }
            demoMedicines.Add(medicine);
        }

        // Create a visible mix of appointment states without using conflicting slots.
        var appointmentSpecs = new[]
        {
            (0, 0, -3, 10, 0, AppointmentStatus.Completed, "Routine cardiac consultation"),
            (1, 1, -4, 11, 0, AppointmentStatus.Completed, "Knee pain evaluation"),
            (2, 2, 3, 10, 30, AppointmentStatus.Confirmed, "Skin consultation"),
            (3, 3, 3, 11, 15, AppointmentStatus.Pending, "Child wellness consultation"),
            (4, 4, 4, 9, 30, AppointmentStatus.Pending, "ENT follow-up"),
            (5, 0, -1, 12, 0, AppointmentStatus.Cancelled, "Cancelled follow-up"),
            (6, 1, -2, 12, 30, AppointmentStatus.Rejected, "Rejected duplicate request")
        };

        var demoAppointments = new List<Appointment>();
        foreach (var spec in appointmentSpecs)
        {
            var appointmentDate = today.AddDays(spec.Item3).AddHours(spec.Item4).AddMinutes(spec.Item5);
            var existing = await db.Appointments.FirstOrDefaultAsync(a => a.PatientId == demoPatients[spec.Item1].Id && a.DoctorId == demoDoctors[spec.Item2].Id && a.AppointmentDate == appointmentDate);
            if (existing is null)
            {
                existing = new Appointment
                {
                    PatientId = demoPatients[spec.Item1].Id, DoctorId = demoDoctors[spec.Item2].Id, AppointmentDate = appointmentDate,
                    Status = spec.Item6, Reason = spec.Item7
                };
                db.Appointments.Add(existing);
                await db.SaveChangesAsync();
            }
            demoAppointments.Add(existing);
        }

        // Completed appointments get medical records, and those records feed lab/prescription demos.
        var completedAppointments = demoAppointments.Where(a => a.Status == AppointmentStatus.Completed).ToList();
        var record1 = await EnsureDemoMedicalRecordAsync(db, completedAppointments[0], "Chest discomfort after exertion", "Hypertension follow-up", "Monitor blood pressure and maintain medication adherence.", "Review in 2 weeks.");
        var record2 = await EnsureDemoMedicalRecordAsync(db, completedAppointments[1], "Knee pain while climbing stairs", "Mild knee strain", "Rest, physiotherapy exercises and avoid excessive load.", "Review if pain persists.");

        await EnsureDemoLabTestAsync(db, record1, "Lipid Profile", LabTestStatus.Completed, "Total cholesterol: 182 mg/dL; LDL: 104 mg/dL", "Routine cardiovascular screening.");
        await EnsureDemoLabTestAsync(db, record2, "X-Ray Knee", LabTestStatus.InProgress, null, "Radiology processing in progress.");

        var prescription1 = await EnsureDemoPrescriptionAsync(db, record1, PrescriptionStatus.Dispensed, "Continue prescribed medicines and follow diet recommendations.");
        var prescription2 = await EnsureDemoPrescriptionAsync(db, record2, PrescriptionStatus.Processing, "Take after meals and continue physiotherapy.");
        var prescription3 = await EnsureDemoPrescriptionAsync(db, record2, PrescriptionStatus.Pending, "Use only as directed by the doctor.");

        await EnsureDemoPrescriptionItemAsync(db, prescription1, demoMedicines[5], "1 tablet", "Once daily", "30 days", 30);
        await EnsureDemoPrescriptionItemAsync(db, prescription2, demoMedicines[1], "1 tablet", "Twice daily", "5 days", 10);
        await EnsureDemoPrescriptionItemAsync(db, prescription3, demoMedicines[3], "1 sachet", "Twice daily", "3 days", 6);

    }

    private static async Task<MedicalRecord> EnsureDemoMedicalRecordAsync(ApplicationDbContext db, Appointment appointment, string symptoms, string diagnosis, string notes, string followUp)
    {
        var record = await db.MedicalRecords.FirstOrDefaultAsync(r => r.AppointmentId == appointment.Id);
        if (record is not null) return record;
        record = new MedicalRecord
        {
            PatientId = appointment.PatientId, DoctorId = appointment.DoctorId, AppointmentId = appointment.Id, VisitDate = appointment.AppointmentDate,
            Symptoms = symptoms, Diagnosis = diagnosis, DoctorNotes = notes, FollowUpInstructions = followUp
        };
        db.MedicalRecords.Add(record);
        await db.SaveChangesAsync();
        return record;
    }

    private static async Task<LabTest> EnsureDemoLabTestAsync(ApplicationDbContext db, MedicalRecord record, string name, LabTestStatus status, string? result, string notes)
    {
        var test = await db.LabTests.FirstOrDefaultAsync(t => t.MedicalRecordId == record.Id && t.TestName == name);
        if (test is not null) return test;
        test = new LabTest
        {
            PatientId = record.PatientId, DoctorId = record.DoctorId, MedicalRecordId = record.Id, TestName = name,
            Status = status, RequestedDate = record.VisitDate, Result = result, ReportNotes = result is null ? null : "Demo laboratory result", Notes = notes,
            CompletedDate = status == LabTestStatus.Completed ? record.VisitDate.AddHours(4) : null
        };
        db.LabTests.Add(test);
        await db.SaveChangesAsync();
        return test;
    }

    private static async Task<Prescription> EnsureDemoPrescriptionAsync(ApplicationDbContext db, MedicalRecord record, PrescriptionStatus status, string instructions)
    {
        var existing = await db.Prescriptions.FirstOrDefaultAsync(p => p.MedicalRecordId == record.Id && p.Status == status);
        if (existing is not null) return existing;
        var prescription = new Prescription
        {
            PatientId = record.PatientId, DoctorId = record.DoctorId, MedicalRecordId = record.Id, CreatedDate = record.VisitDate.AddMinutes(10), Status = status, Instructions = instructions
        };
        db.Prescriptions.Add(prescription);
        await db.SaveChangesAsync();
        return prescription;
    }

    private static async Task EnsureDemoPrescriptionItemAsync(ApplicationDbContext db, Prescription prescription, Medicine medicine, string dosage, string frequency, string duration, int quantity)
    {
        if (await db.PrescriptionItems.AnyAsync(i => i.PrescriptionId == prescription.Id && i.MedicineId == medicine.Id)) return;
        db.PrescriptionItems.Add(new PrescriptionItem
        {
            PrescriptionId = prescription.Id, MedicineId = medicine.Id, Dosage = dosage, Frequency = frequency, Duration = duration, Quantity = quantity, UnitPrice = medicine.Price
        });
        await db.SaveChangesAsync();
    }


    private static async Task RepairPatientIdentityLinksAsync(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        // Every Patient profile should have exactly one linked Identity account.
        // Older versions of the demo could leave a Patient row without
        // ApplicationUserId (for example a patient created before portal-login
        // accounts were introduced). Repair those links during startup.
        var patients = await db.Patients
            .Where(p => string.IsNullOrWhiteSpace(p.ApplicationUserId))
            .OrderBy(p => p.Id)
            .ToListAsync();

        foreach (var patient in patients)
        {
            if (string.IsNullOrWhiteSpace(patient.Email))
                continue;

            var email = patient.Email.Trim();
            var existingUser = await userManager.FindByEmailAsync(email);

            if (existingUser is not null)
            {
                var roles = await userManager.GetRolesAsync(existingUser);
                if (roles.Count == 0 || roles.Contains(PatientRole))
                {
                    if (!roles.Contains(PatientRole))
                        await userManager.AddToRoleAsync(existingUser, PatientRole);

                    patient.ApplicationUserId = existingUser.Id;
                    existingUser.FirstName = patient.FirstName;
                    existingUser.LastName = patient.LastName;
                    await userManager.UpdateAsync(existingUser);
                    continue;
                }

                // The email belongs to another role; do not silently take over
                // that account. The patient remains unlinked for Admin review.
                continue;
            }

            var newUser = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = patient.FirstName,
                LastName = patient.LastName
            };

            // Existing orphaned patient profiles need a usable portal account.
            // Admin/Receptionist can subsequently change the account details.
            var createResult = await userManager.CreateAsync(newUser, "MediSync@123");
            if (!createResult.Succeeded)
                continue;

            var roleResult = await userManager.AddToRoleAsync(newUser, PatientRole);
            if (!roleResult.Succeeded)
            {
                await userManager.DeleteAsync(newUser);
                continue;
            }

            patient.ApplicationUserId = newUser.Id;
        }

        await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> EnsureSeedPatientUserAsync(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        const string seedEmail = "patient@medisync.local";
        const string seedFirstName = "Kunal";
        const string seedLastName = "Desai";

        // Prefer an existing Patient record over the original seed email.
        // This is important when the demo patient's email has been changed by Admin/Receptionist.
        var matchingPatients = await db.Patients
            .Where(p => p.FirstName == seedFirstName && p.LastName == seedLastName)
            .OrderByDescending(p => p.ApplicationUserId != null)
            .ThenByDescending(p => p.Email != seedEmail)
            .ThenBy(p => p.Id)
            .ToListAsync();

        var canonicalPatient = matchingPatients.FirstOrDefault(p =>
            !string.Equals(p.Email, seedEmail, StringComparison.OrdinalIgnoreCase))
            ?? matchingPatients.FirstOrDefault();

        if (canonicalPatient is not null && !string.IsNullOrWhiteSpace(canonicalPatient.ApplicationUserId))
        {
            var canonicalUser = await userManager.FindByIdAsync(canonicalPatient.ApplicationUserId);
            if (canonicalUser is not null)
            {
                if (!await userManager.IsInRoleAsync(canonicalUser, PatientRole))
                    await userManager.AddToRoleAsync(canonicalUser, PatientRole);

                // Remove duplicate seed-created Patient rows, keeping the existing edited record.
                var duplicates = matchingPatients.Where(p => p.Id != canonicalPatient.Id).ToList();
                foreach (var duplicate in duplicates)
                {
                    var duplicateAppointments = await db.Appointments.Where(a => a.PatientId == duplicate.Id).ToListAsync();
                    foreach (var item in duplicateAppointments) item.PatientId = canonicalPatient.Id;

                    var duplicateRecords = await db.MedicalRecords.Where(r => r.PatientId == duplicate.Id).ToListAsync();
                    foreach (var item in duplicateRecords) item.PatientId = canonicalPatient.Id;

                    var duplicateLabTests = await db.LabTests.Where(l => l.PatientId == duplicate.Id).ToListAsync();
                    foreach (var item in duplicateLabTests) item.PatientId = canonicalPatient.Id;

                    var duplicatePrescriptions = await db.Prescriptions.Where(pr => pr.PatientId == duplicate.Id).ToListAsync();
                    foreach (var item in duplicatePrescriptions) item.PatientId = canonicalPatient.Id;

                    var duplicateUserId = duplicate.ApplicationUserId;
                    db.Patients.Remove(duplicate);
                    if (!string.IsNullOrWhiteSpace(duplicateUserId) && duplicateUserId != canonicalUser.Id)
                    {
                        var duplicateUser = await userManager.FindByIdAsync(duplicateUserId);
                        if (duplicateUser is not null) await userManager.DeleteAsync(duplicateUser);
                    }
                }

                await db.SaveChangesAsync();
                return canonicalUser;
            }
        }

        var user = await EnsureUserAsync(userManager, seedEmail, seedFirstName, seedLastName, PatientRole);
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.ApplicationUserId == user.Id);
        if (patient is null)
        {
            db.Patients.Add(new Patient
            {
                ApplicationUserId = user.Id,
                FirstName = seedFirstName,
                LastName = seedLastName,
                DateOfBirth = new DateTime(2002, 4, 18),
                Gender = "Male",
                Phone = "9000000002",
                Email = user.Email,
                Address = "Nadiad, Gujarat",
                EmergencyContact = "9000000003",
                RegistrationDate = DateTime.UtcNow.Date
            });
            await db.SaveChangesAsync();
        }

        return user;
    }

    private static async Task<ApplicationUser> EnsureUserAsync(UserManager<ApplicationUser> userManager, string email, string firstName, string lastName, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FirstName = firstName, LastName = lastName };
            var result = await userManager.CreateAsync(user, "MediSync@123");
            if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
        if (!await userManager.IsInRoleAsync(user, role)) await userManager.AddToRoleAsync(user, role);
        return user;
    }
}
