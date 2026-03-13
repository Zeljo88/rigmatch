# RigMatch

RigMatch is a talent platform focused on turning unstructured CVs into structured professional profiles for employer-side review, search, and matching.

The current product flow is:

1. Employer registers and logs in
2. Employer uploads a candidate CV in PDF format
3. The backend extracts text from the CV
4. Azure OpenAI parses the CV into structured profile data
5. RigMatch normalizes job roles against an internal standard role catalog
6. Employer reviews uncertain role mappings and edits parsed data
7. The structured profile is saved into the company CV library
8. Employer creates projects and sees matching candidates based on structured requirements

## Current Scope

The application currently includes:

- employer authentication with company ownership
- CV upload and PDF text extraction
- AI-based CV parsing using Azure OpenAI
- role normalization with:
  - standard roles
  - hard aliases
  - soft aliases
  - description-assisted scoring
- company-specific suggested alias learning from manual reviews
- CV library with search and filters
- candidate detail view
- employer project creation and candidate matching
- Azure deployment preparation with GitHub Actions

## How Role Matching Works

RigMatch does not rely only on raw CV job titles.

For each experience entry, the system tries to match the role using:

1. exact standard role match
2. known aliases
3. title heuristics
4. job description signals when the title is unclear

High-confidence matches can be auto-mapped. Lower-confidence matches stay marked for employer review.

Manual reviewed mappings are stored as suggested aliases per company. If the same reviewed mapping is confirmed multiple times, it can be promoted into reusable aliases for future CVs.

## Candidate and Project Matching

Projects are matched against saved candidate profiles using structured data rather than only free text.

Current matching signals include:

- primary role match
- additional acceptable roles
- required and preferred skills
- required and preferred certifications
- minimum experience years
- location
- preferred education

This keeps matching explainable and easier to trust.

## Tech Stack

### Frontend

- Angular 17
- Angular Material

### Backend

- .NET 9 Web API
- Entity Framework Core

### AI Parsing

- Azure OpenAI

### Storage and Data

- local development:
  - SQLite
  - local file storage
- Azure deployment target:
  - Azure Database for PostgreSQL Flexible Server
  - Azure Blob Storage

## Repository Structure

- [frontend](c:/DemoProjects/rigmatch/frontend): Angular application
- [backend](c:/DemoProjects/rigmatch/backend): .NET Web API
- [infra](c:/DemoProjects/rigmatch/infra): Azure Bicep infrastructure
- [docs](c:/DemoProjects/rigmatch/docs): deployment notes and supporting documentation
- [.github/workflows](c:/DemoProjects/rigmatch/.github/workflows): GitHub Actions workflows

## Running Locally

### Backend

From `backend/`:

```powershell
dotnet build
dotnet run --urls http://localhost:5168
```

### Frontend

From `frontend/`:

```powershell
npm install
npm start
```

Frontend development runs on:

- `http://localhost:64765`

Backend development runs on:

- `http://localhost:5168`

## Configuration

Main backend configuration lives in:

- [appsettings.json](c:/DemoProjects/rigmatch/backend/appsettings.json)
- [appsettings.Development.json](c:/DemoProjects/rigmatch/backend/appsettings.Development.json)

The application is set up so that:

- development uses local storage and SQLite by default
- production can switch to PostgreSQL and Azure Blob Storage through configuration only

## Azure Deployment

Azure deployment preparation is already included:

- infrastructure template: [main.bicep](c:/DemoProjects/rigmatch/infra/main.bicep)
- GitHub Actions workflow: [deploy-azure.yml](c:/DemoProjects/rigmatch/.github/workflows/deploy-azure.yml)
- setup notes: [azure-deployment.md](c:/DemoProjects/rigmatch/docs/azure-deployment.md)

Planned Azure hosting model:

- Angular frontend -> Azure Static Web Apps
- .NET API -> Azure App Service
- database -> Azure Database for PostgreSQL Flexible Server
- CV files -> Azure Blob Storage
- monitoring -> Application Insights

## MVP Goal

The MVP is meant to answer:

Can we reliably convert messy CVs into structured, searchable employer-side profiles with limited manual correction?

If the answer is yes, RigMatch becomes the foundation for broader employer workflows such as candidate search, project staffing, and later marketplace features.
