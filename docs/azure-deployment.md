# RigMatch Azure Deployment

## Target shape

- Frontend: Azure Static Web Apps
- API: Azure App Service (Linux)
- Database: Azure Database for PostgreSQL Flexible Server
- File storage: Azure Blob Storage
- Monitoring: Application Insights
- AI parsing: existing Azure OpenAI deployment

## Local vs production

- Development stays local by default:
  - SQLite
  - local `uploads/`
  - localhost API URL
  - optional mock parsing
- Production switches by configuration:
  - PostgreSQL connection string
  - Blob Storage provider
  - Azure-hosted API URL
  - real Azure OpenAI settings

## GitHub repository variables

Set these in GitHub `Settings -> Secrets and variables -> Actions -> Variables`:

- `AZURE_RESOURCE_GROUP`
- `AZURE_LOCATION`
- `AZURE_STATIC_WEB_APP_LOCATION`
- `RIGMATCH_NAME_PREFIX`
- `POSTGRES_ADMIN_USERNAME`
- `AZURE_OPENAI_DEPLOYMENT_NAME`

Recommended:

- `AZURE_LOCATION=swedencentral`
- `AZURE_STATIC_WEB_APP_LOCATION=swedencentral` if supported in your subscription, otherwise use the nearest supported Static Web Apps region

## GitHub repository secrets

Set these in GitHub `Settings -> Secrets and variables -> Actions -> Secrets`:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `POSTGRES_ADMIN_PASSWORD`
- `JWT_SIGNING_KEY`
- `AZURE_OPENAI_ENDPOINT`
- `AZURE_OPENAI_API_KEY`

## Azure prerequisites

Before the workflow can deploy, configure GitHub OIDC against Azure:

1. Create or reuse a Microsoft Entra application / service principal.
2. Add a federated credential for the GitHub repository and branch/environment you want to deploy from.
3. Grant it enough access on the target resource group or subscription.

Minimum practical role for the first setup:

- `Contributor` on the target resource group

## Deployment order

The workflow in `.github/workflows/deploy-azure.yml` does this:

1. Login to Azure with OIDC
2. Create or update the resource group
3. Deploy infrastructure from `infra/main.bicep`
4. Configure API app settings
5. Publish and deploy the .NET API
6. Build the Angular frontend with the deployed API URL
7. Fetch the Static Web Apps deployment token
8. Upload the frontend build to Static Web Apps

## Manual checks after first deployment

1. Open `https://<api-host>/health`
2. Open the Static Web Apps URL
3. Register a new employer
4. Upload a PDF CV
5. Save a parsed CV
6. Create a project and verify candidate matching
