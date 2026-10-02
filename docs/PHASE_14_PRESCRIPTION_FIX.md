# Phase 14 — Prescription Validation Fix

## Fixes
- Doctor prescription form remains locked to the selected medical record after a validation error/postback.
- Server re-asserts PatientId and DoctorId from the MedicalRecord and rejects records owned by another doctor.
- Quantity is no longer a manually entered value. It is calculated from dosage × frequency × duration.
- Supported frequencies include once/twice/three/four times daily and every 4/6/8/12/24 hours.
- Duration supports days, weeks, and months (months treated as 30 days).
- Server recalculates quantity; browser JavaScript is only a convenience preview.
- Validation errors are now visible, including property-level errors.
- Pharmacy stock is checked against the calculated quantity.
