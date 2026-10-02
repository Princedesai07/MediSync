# MediSync — Free Azure Deployment

This guide is for the college/demo deployment of MediSync. It keeps the existing ASP.NET Core + SQL Server architecture instead of replacing EF Core with another database provider.

## Recommended free path

Microsoft currently offers Azure for Students for eligible full-time university students, with no credit card required, $100 credit and free service allowances. Azure App Service has an always-free allowance, and Azure SQL Database has a separate free offer with 100,000 vCore-seconds and 32 GB storage per database per month. Always monitor the Azure portal and select auto-pause/stop behavior where offered so the project cannot accidentally incur charges.

## 1. Create Azure resources

Create:

- Azure App Service (ASP.NET Core web app)
- Azure SQL Database (free offer if eligible)

Use a unique app name such as `medisync-prince-demo-2026`.

## 2. Azure SQL connection string

After creating the Azure SQL database, copy its ADO.NET/SQL Server connection string. Do **not** put the real password in GitHub.

In App Service → Environment variables / Configuration, add:

```text
ConnectionStrings__DefaultConnection=<YOUR_AZURE_SQL_CONNECTION_STRING>
```

Also add:

```text
MediSync__EnableDemoSeed=true
```

The second setting intentionally enables the fictional demo dataset on the college demo instance. Set it to `false` for a real production deployment.

## 3. GitHub Actions

The repository contains:

- `.github/workflows/ci.yml` — build validation
- `.github/workflows/azure-deploy.yml` — build + deploy

In GitHub repository settings create a **repository variable**:

```text
AZURE_WEBAPP_NAME=<your-app-service-name>
```

Then create a **repository secret**:

```text
AZURE_WEBAPP_PUBLISH_PROFILE=<contents of the Azure App Service publish-profile file>
```

Microsoft documents publish-profile based App Service deployment through `azure/webapps-deploy@v3`. For stronger production security, OIDC/federated credentials can be used later instead of a publish profile.

## 4. Push to GitHub

From the repository root:

```powershell
git init
git branch -M main
git add .
git commit -m "feat: prepare MediSync for GitHub and Azure deployment"
git remote add origin https://github.com/<YOUR_USERNAME>/<YOUR_REPOSITORY>.git
git push -u origin main
```

The CI workflow should run automatically. After the Azure variables/secrets are configured, the deployment workflow will publish the application.

## 5. Demo safety

- Demo accounts use fictional data only.
- Do not deploy real patient information.
- Do not commit connection strings, passwords, publish profiles or API keys.
- Change/remove demo accounts before any non-college use.
- Monitor Azure free usage limits.

## Current architecture

```text
Browser
   ↓
Azure App Service
   ↓
ASP.NET Core MVC / Identity / EF Core
   ↓
Azure SQL Database
```

The local development configuration remains SQL Server LocalDB, so the same project continues to work on the college PC.
