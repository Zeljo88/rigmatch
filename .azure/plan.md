# Azure Deployment Plan

> **Status:** Validated

Generated: 2026-03-12T10:45:00+01:00

---

## 1. Project Overview

**Goal:** Prepare RigMatch for Azure deployment with GitHub Actions on push to `master`, using App Service for the .NET API, Static Web Apps for the Angular frontend, PostgreSQL for relational data, Blob Storage for CV files, Application Insights for monitoring, and the existing Azure OpenAI resource for parsing.

**Path:** Modernize Existing

---

## 2. Requirements

| Attribute | Value |
|-----------|-------|
| Classification | Development |
| Scale | Small |
| Budget | Cost-Optimized |
| **Subscription** | Existing Azure subscription targeted by GitHub OIDC workflow |
| **Location** | Sweden Central |

---

## 3. Components Detected

| Component | Type | Technology | Path |
|-----------|------|------------|------|
| RigMatch frontend | Frontend | Angular 17 | `frontend/` |
| RigMatch API | API | .NET 9 Web API | `backend/` |
| Local relational store | Database | SQLite | `backend/rigmatch*.db` |
| Local CV file storage | File storage | Filesystem uploads | `backend/uploads/` |

---

## 4. Recipe Selection

**Selected:** Bicep

**Rationale:** The user explicitly wants GitHub Actions deployment on merge to `master` with direct control over infrastructure provisioning and application deployment steps. Bicep plus a hand-authored workflow keeps the pipeline explicit for App Service, Static Web Apps, PostgreSQL, and Blob Storage without adding an extra deployment wrapper.

---

## 5. Architecture

**Stack:** App Service

### Service Mapping

| Component | Azure Service | SKU |
|-----------|---------------|-----|
| Angular frontend | Azure Static Web Apps | Free / Standard-ready |
| .NET API | Azure App Service (Linux) | B1 |
| API compute plan | Azure App Service Plan (Linux) | B1 |
| Relational data | Azure Database for PostgreSQL Flexible Server | Burstable |
| CV files | Azure Storage Account + Blob container | Standard LRS |
| CV parsing | Azure OpenAI | Existing deployment |

### Supporting Services

| Service | Purpose |
|---------|---------|
| Application Insights | API telemetry and diagnostics |
| Managed Identity | Future service-to-service access |
| GitHub Actions | CI/CD on push to `master` |

---

## 6. Execution Checklist

### Phase 1: Planning
- [x] Analyze workspace
- [x] Gather requirements
- [x] Confirm deployment direction and region with user
- [x] Scan codebase
- [x] Select recipe
- [x] Plan architecture
- [x] **User approved this plan**

### Phase 2: Execution
- [x] Research components (load references, inspect current code)
- [x] Refactor backend for PostgreSQL support
- [x] Refactor backend for Blob Storage-backed CV files
- [x] Refactor application configuration for production/runtime settings
- [x] Refactor frontend for production API configuration
- [x] Generate infrastructure files
- [x] Generate GitHub Actions workflow
- [x] Update plan status to "Ready for Validation"

### Phase 3: Validation
- [x] Invoke azure-validate skill
- [x] All validation checks pass
- [x] Update plan status to "Validated"
- [x] Record validation proof below

### Phase 4: Deployment
- [ ] Invoke azure-deploy skill
- [ ] Deployment successful
- [ ] Update plan status to "Deployed"

---

## 7. Validation Proof

> **⛔ REQUIRED**: The azure-validate skill MUST populate this section before setting status to `Validated`. If this section is empty and status is `Validated`, the validation was bypassed improperly.

| Check | Command Run | Result | Timestamp |
|-------|-------------|--------|-----------|
| Backend build | `dotnet build backend/RigMatch.Api.csproj -p:OutDir=bin/verify/` | ✅ Pass | 2026-03-12 |
| Frontend production build | `npm run build -- --configuration production` | ✅ Pass (bundle warning only) | 2026-03-12 |
| Bicep compilation | `az bicep build --file infra/main.bicep --stdout` | ✅ Pass | 2026-03-12 |
| Workflow YAML parse | `python -c "import yaml; ..."` | ✅ Pass | 2026-03-12 |

**Validated by:** azure-validate workflow
**Validation timestamp:** 2026-03-12

---

## 8. Files to Generate

| File | Purpose | Status |
|------|---------|--------|
| `.azure/plan.md` | Azure preparation plan | ✅ |
| `infra/main.bicep` | Azure infrastructure definition | ✅ |
| `.github/workflows/deploy-azure.yml` | CI/CD workflow | ✅ |
| `frontend/src/environments/*` | Frontend environment configuration | ✅ |
| `backend/Services/*` | Azure-aware file storage and config support | ✅ |
| `docs/azure-deployment.md` | Azure setup and GitHub secrets guide | ✅ |

---

## 9. Next Steps

> Current: Validation complete, ready for deployment

1. Add the required GitHub repository variables and secrets described in `docs/azure-deployment.md`.
2. Configure GitHub OIDC against the target Azure subscription and resource group.
3. Invoke azure-deploy when you want the first real deployment executed.
