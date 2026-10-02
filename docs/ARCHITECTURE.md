# MediSync Architecture

```text
Browser
  │
  ▼
ASP.NET Core MVC Controllers
  │
  ├── ASP.NET Core Identity Authentication
  ├── Role-based Authorization
  ├── Ownership / role scope checks
  │
  ▼
Entity Framework Core
  │
  ▼
SQL Server / LocalDB
```

## Main entities
- ApplicationUser
- Patient
- Doctor
- Appointment
- MedicalRecord
- LabTest
- Prescription
- PrescriptionItem
- Medicine

## Relationship direction
`ApplicationUser → Patient/Doctor`

`Patient ↔ Appointment ↔ Doctor`

`Patient → MedicalRecord ← Doctor`

`MedicalRecord → LabTest`

`MedicalRecord → Prescription → PrescriptionItem → Medicine`

## Extension principle
New features should follow: User → Role → Permission → Dashboard → Workflow → Database → Related modules.
