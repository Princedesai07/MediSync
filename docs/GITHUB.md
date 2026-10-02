# GitHub Setup

## Recommended repository

Create a public GitHub repository named something like:

`MediSync-Healthcare-Management-System`

Keep the repository focused on the project source and documentation. Do not upload ZIP backups, `bin/`, `obj/`, `.vs/`, local database files, connection strings, passwords, or Azure publish profiles.

## First push

Run these commands from the repository root after creating the empty GitHub repository:

```powershell
git init
git branch -M main
git add .
git commit -m "feat: initial MediSync release"
git remote add origin https://github.com/<USERNAME>/<REPOSITORY>.git
git push -u origin main
```

## Useful follow-up commits

Use small, descriptive commits rather than `final-final.zip` style snapshots. Examples:

```text
feat: add rich demo seed data
feat: add dark mode and responsive UI
fix: enforce appointment availability
fix: secure patient ownership checks
docs: add architecture and deployment guide
ci: add GitHub build workflow
ci: add Azure App Service deployment workflow
```

## GitHub Actions

Two workflows are included:

- `.github/workflows/ci.yml` — restores and builds the solution on pushes and pull requests to `main`.
- `.github/workflows/azure-deploy.yml` — publishes and deploys to Azure App Service after a push to `main` (and can also be run manually).

For Azure deployment configure this repository variable:

```text
AZURE_WEBAPP_NAME=<your Azure App Service name>
```

and this repository secret:

```text
AZURE_WEBAPP_PUBLISH_PROFILE=<contents of the App Service publish-profile file>
```

The application database connection string must be configured in Azure App Service, not in GitHub source code:

```text
ConnectionStrings__DefaultConnection=<Azure SQL connection string>
```

For the college demo only, enable the fictional seed dataset with:

```text
MediSync__EnableDemoSeed=true
```

## Repository checklist

- [ ] README is complete
- [ ] `.gitignore` is present
- [ ] No secrets are committed
- [ ] No real patient data is committed
- [ ] Build workflow passes
- [ ] Screenshots added later
- [ ] GitHub repository description and topics added
- [ ] Azure deployment variables/secrets configured only in GitHub/Azure settings
