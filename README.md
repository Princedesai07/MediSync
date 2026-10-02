# MediSync — Healthcare Coordination & Management System

MediSync is a student-built ASP.NET Core MVC healthcare coordination and management system developed for academic demonstration at Dharmsinh Desai University.

## Highlights

- Role-based access for Admin, Doctor, Receptionist, Laboratory, Pharmacist and Patient
- Patient registration and patient portal
- 15-minute appointment scheduling with doctor availability and double-booking protection
- Doctor consultation and medical records
- Laboratory requests and results
- Prescription workflow and pharmacy stock management
- Patient medical history
- Ownership checks and server-side authorization
- Light/dark theme
- Responsive healthcare-focused UI
- Rich fictional demo data for college demonstrations

## Technology

- C# / .NET 10
- ASP.NET Core MVC
- Razor Views
- Entity Framework Core
- SQL Server / LocalDB for development
- ASP.NET Core Identity
- HTML / CSS / Bootstrap / JavaScript

## Local setup

Requirements:

- .NET 10 SDK
- SQL Server LocalDB (recommended for the supplied development configuration)

Run:

```powershell
dotnet restore
dotnet build
dotnet run
```

The development configuration uses the `MediSyncDb` LocalDB database. On a fresh development machine the application bootstraps the schema and creates the demo data automatically.

## Demo accounts

Default password for the supplied demo accounts:

`MediSync@123`

Core accounts:

| Role | Email |
|---|---|
| Admin | admin@medisync.local |
| Doctor | doctor@medisync.local |
| Receptionist | reception@medisync.local |
| Laboratory | lab@medisync.local |
| Pharmacist | pharmacy@medisync.local |
| Patient | patient@medisync.local |

The rich demo seed also creates additional fictional doctors, patients, receptionists, laboratory users and pharmacists. See `docs/DEMO_DATA.md` for the complete demo dataset and credentials.

> Demo credentials are for academic demonstration only. Change or remove seeded accounts before using a deployed environment for anything beyond a college demonstration.

## Project documentation

- `docs/ARCHITECTURE.md` — application architecture
- `docs/WORKFLOW.md` — end-to-end healthcare workflow
- `docs/ROLE_MATRIX.md` — role capabilities
- `docs/SECURITY_AUDIT.md` — security and ownership checks
- `docs/DEMO_DATA.md` — seeded demo users and data
- `docs/GITHUB.md` — recommended Git workflow
- `docs/DEPLOYMENT.md` — deployment preparation and free-tier checklist

## Academic scope

MediSync is an academic project. It is not intended to replace a production hospital information system, provide medical diagnosis, process real insurance claims, or handle real patient data.


## GitHub & Azure deployment

MediSync includes GitHub Actions for build validation and Azure App Service deployment. See [`docs/AZURE_DEPLOYMENT.md`](docs/AZURE_DEPLOYMENT.md) for the zero-cost college-demo deployment path and required GitHub/Azure configuration.

The deployed demo should use fictional seed data only. Never commit production connection strings, passwords or publish profiles.
