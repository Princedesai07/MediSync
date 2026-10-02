# Phase 14 — Validation & Data Integrity Fixes

## Findings addressed
- Validation errors on Doctor/Patient management could appear as a silent form refresh because field-level errors were not included in the summary. The management forms now display all ModelState errors.
- Deactivated doctors and patients could still be selected for new appointments. Appointment creation now lists only active linked accounts and performs server-side active-account validation.
- Appointment time was not checked against the doctor's configured availability. Availability now uses the format `Mon-Fri 09:00-14:00`, is validated when a doctor is created/edited, and is enforced server-side when an appointment is created.
- Accounts with unfinished clinical/pharmacy workflow work can no longer be deactivated by Admin. Doctor, Patient, Laboratory and Pharmacist accounts are blocked from deactivation while relevant work remains pending/in progress.
- Completed/historical records do not by themselves block deactivation.

## Manual retest checklist
1. Duplicate patient/doctor email: visible validation message; no account created.
2. Invalid patient data: visible validation message; no silent refresh.
3. Inactive doctor: absent from appointment doctor list and rejected server-side if tampered.
4. Inactive patient: absent from appointment patient list and rejected server-side if tampered.
5. Appointment outside doctor's configured day/time: rejected server-side.
6. Doctor with Pending/Confirmed appointment: cannot be deactivated.
7. Patient with Pending/Confirmed appointment: cannot be deactivated.
8. Doctor with Requested/InProgress lab work or Pending/Processing prescription: cannot be deactivated.
9. Laboratory with Requested/InProgress lab work: cannot be deactivated.
10. Pharmacist with Pending/Processing prescription: cannot be deactivated.
11. Account with only completed historical work: can be deactivated.


## v1.17 — Appointment minute-slot normalization
- Normalizes posted appointment DateTime values to minute precision before validation and persistence.
- Double-booking checks use a one-minute range, catching existing appointments that contain seconds/milliseconds.
- Applies to both doctor and patient conflicts.
- Appointment create input explicitly uses datetime-local minute precision.
