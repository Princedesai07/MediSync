# Rich Demo Seed Implementation

The rich seed is intentionally idempotent and runs after the original baseline seed. It creates additional fictional users and healthcare records only when the demo doctor marker does not already exist.

The seed does not delete or overwrite existing user-created clinical data.

For a clean local demonstration database, delete the development `MediSyncDb` database and start the application again. The existing development bootstrap will recreate the schema and seed the baseline plus rich demo dataset.
