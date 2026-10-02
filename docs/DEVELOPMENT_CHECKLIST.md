# MediSync Base Coverage

## Foundation included
- [x] ASP.NET Core MVC / .NET 10 project structure
- [x] SQL Server / LocalDB configuration
- [x] Entity Framework Core model layer
- [x] ASP.NET Core Identity authentication
- [x] Six application roles
- [x] Role-based controller/action authorization
- [x] Role-aware navigation
- [x] Role-specific dashboards
- [x] Patient ownership query checks
- [x] Anti-forgery on state-changing MVC forms

## Healthcare workflow included
- [x] Patient registration and profile
- [x] Doctor registry
- [x] Appointment creation
- [x] Appointment Pending → Confirmed → Completed doctor workflow
- [x] Medical record from completed appointment
- [x] Laboratory request and result status workflow
- [x] Prescription + prescription item + medicine relationship
- [x] Pharmacy dispensing + stock deduction
- [x] Patient history aggregation

## Intentionally left for the next development layer
- [ ] Full admin user creation / role assignment screen
- [ ] Full Identity account management and password recovery
- [ ] EF Core migrations
- [ ] Rich multi-item prescription editor
- [ ] Appointment conflict/doctor availability engine
- [ ] Search/filter/pagination/reporting
- [ ] Lab report file upload / print
- [ ] Audit log
- [ ] Automated tests
- [ ] Production deployment configuration

## Phase 1 - Admin Management (implemented)
- [x] Admin user search by name/email
- [x] Admin user filtering by role/status
- [x] Activate/deactivate Identity accounts
- [x] Prevent self-deactivation/self-deletion
- [x] Doctor creation creates linked Identity Doctor account
- [x] Patient creation creates linked Identity Patient account
- [x] Doctor profile editing synchronizes linked account
- [x] Patient profile editing synchronizes linked account
- [x] Doctor directory search/filter
- [x] Patient registry search/filter
- [x] Role-scoped patient visibility retained for doctors
