# MediSync Core Workflow

## Patient journey
Patient registration → Appointment → Doctor consultation → Medical Record → Lab Test / Prescription → Laboratory / Pharmacy → Patient History.

## Appointment lifecycle
Pending → Confirmed → Completed.

Doctor actions are constrained to the forward consultation workflow, with cancellation available. Admin remains the system-level override role.

## Laboratory lifecycle
Requested → In Progress → Completed.

A result is required before a laboratory user can mark a test completed. A completed test cannot be moved backward by the provided base action.

## Pharmacy lifecycle
Pending → Processing / Dispensed.

The current base exposes a direct Process & Dispense action. Before dispensing, every prescription item must have valid quantity, an unexpired medicine and sufficient stock. Stock decrement is performed in a database transaction.

## Patient privacy
Patient-facing queries resolve the patient by the signed-in Identity user ID rather than trusting a patient ID supplied by the browser. This prevents changing a URL/hidden field from exposing another patient's records.
