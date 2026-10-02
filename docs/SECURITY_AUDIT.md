# MediSync Phase 13 — Security & Authorization Audit

## Scope

This audit hardens the role and ownership boundaries from v1.12. Navigation is not treated as a security boundary; controller/action authorization and server-side ownership checks remain authoritative.

## Verified server-side boundaries

- **Admin:** User Management is restricted to the Admin role. Admin can manage system-wide records.
- **Doctor:** Appointments, medical records, lab requests and prescriptions are filtered/validated against the logged-in doctor's linked Doctor profile.
- **Patient:** Patient portal data is resolved from the logged-in Identity user's linked Patient profile; profile POST also checks the submitted Patient ID against that profile.
- **Receptionist:** Patient and appointment operations are limited to front-desk actions. Clinical record, lab-result, prescription and medicine endpoints remain unavailable.
- **Laboratory:** Lab-result processing is restricted to Laboratory/Admin. Laboratory users cannot create prescriptions or medical records.
- **Pharmacist:** Prescription processing/dispensing and medicine management are restricted to Pharmacist/Admin. Pharmacists cannot create medical records or lab results.
- **Direct URL / ID tampering:** Doctor ownership is checked server-side for appointment status changes, medical-record creation, lab requests and prescriptions. Patient ownership is checked for appointment creation/status changes and patient portal profile/history.

## Phase 13 hardening changes

### 1. Laboratory workflow transitions

Lab status changes are now explicitly validated as:

- Requested → Requested / In Progress / Cancelled
- In Progress → In Progress / Completed / Cancelled
- Completed → Completed only
- Cancelled → Cancelled only

A completed result still requires a non-empty result value. A completed test cannot be reopened or moved backwards by changing the submitted status value.

### 2. Deactivated account sessions

Admin account deactivation now updates the Identity security stamp. The application cookie also checks the account's lockout state during principal validation and rejects a session belonging to a currently locked account. This prevents an already-authenticated deactivated account from continuing to use protected pages.

## Manual penetration-style test checklist

Run these tests after starting the application:

1. Patient A changes a Patient/Appointment/record ID in a URL or form → must receive `Forbid`/not expose Patient B data.
2. Patient A changes appointment status ID to Patient B's appointment → must be rejected.
3. Doctor A changes appointment ID to Doctor B's appointment → must be rejected.
4. Doctor A submits Doctor B's MedicalRecord ID while creating a lab test → must be rejected.
5. Doctor A submits Doctor B's MedicalRecord ID while creating a prescription → must be rejected.
6. Receptionist directly requests MedicalRecords/Create, LabTests/Create, Prescriptions/Create, or Medicines/Index → must be denied.
7. Laboratory directly requests Prescriptions/Create or MedicalRecords/Create → must be denied.
8. Pharmacist directly requests LabTests/UpdateStatus or MedicalRecords/Create → must be denied.
9. Non-Admin directly requests Admin/Users → must be denied.
10. Admin deactivates a user; that user attempts a new login → login must fail.
11. Admin deactivates an already logged-in user; that session makes another protected request → session must be rejected.
12. Attempt invalid lab transitions such as Completed → In Progress or Cancelled → Completed → must be rejected.
13. Attempt to dispense a Pending prescription directly → must be rejected.
14. Attempt to dispense a Processing prescription with insufficient/expired stock → must be rejected.

## Build note

The development container used for packaging does not include the .NET SDK, so compilation must be confirmed with `dotnet build` on the user's Windows development machine.
