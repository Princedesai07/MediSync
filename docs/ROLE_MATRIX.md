# MediSync Role & Permission Matrix

| Module | Admin | Doctor | Receptionist | Laboratory | Pharmacist | Patient |
|---|---|---|---|---|---|---|
| Dashboard | System | Clinical | Front desk | Lab | Pharmacy | Own care |
| Patients | Manage | Relevant patients | Register/update | No | No | Own only through portal |
| Doctors | Manage | View | View | No | No | No |
| Appointments | Manage | View relevant + status | Create/view | Completed context | Completed context | Create/view own |
| Medical records | View | Create/view relevant | No | No | No | View own |
| Lab tests | View | Request/view relevant | No | Process results | No | View own results |
| Prescriptions | View/manage | Create/view relevant | No | No | View/process | View own |
| Medicines | Manage | No | No | No | Manage stock | No |
| User management | View base user list | No | No | No | No | No |

Navigation hiding is only a UI convenience. Controller/action `[Authorize]` rules and ownership queries are the actual security boundary.
