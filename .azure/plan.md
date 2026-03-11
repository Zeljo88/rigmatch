# Azure Deployment Plan

> **Status:** Ready for Validation

Generated: 2026-03-11T13:20:00+01:00

---

## 1. Project Overview

**Goal:** Add employer project management and structured candidate matching to RigMatch on top of the existing company CV library and standardized candidate profiles.

**Path:** Add Components

---

## 2. Requirements

| Attribute | Value |
|-----------|-------|
| Classification | Development |
| Scale | Small |
| Budget | Cost-Optimized |
| **Subscription** | Existing Azure resources unchanged for this feature |
| **Location** | Existing Azure resources unchanged for this feature |

---

## 3. Components Detected

| Component | Type | Technology | Path |
|-----------|------|------------|------|
| RigMatch frontend | Frontend | Angular | `frontend/` |
| RigMatch API | API | .NET 9 Web API | `backend/` |
| SQLite data store | Database | SQLite | `backend/rigmatch.db` |

---

## 4. Recipe Selection

**Selected:** Existing local development workflow

**Rationale:** This task adds application features only. Azure infrastructure, deployment topology, and service footprint remain unchanged.

---

## 5. Architecture

**Stack:** Local development app backed by Azure OpenAI for CV parsing

### Service Mapping

| Component | Azure Service | SKU |
|-----------|---------------|-----|
| CV parsing | Azure OpenAI | Existing S0 deployment |
| Employer projects | Existing API and frontend | Existing local app components |

### Supporting Services

| Service | Purpose |
|---------|---------|
| SQLite | Development persistence |
| Azure OpenAI | CV parsing and extraction |

---

## 6. Execution Checklist

### Phase 1: Planning
- [x] Analyze workspace
- [x] Gather requirements
- [x] Confirm feature direction from user discussion
- [x] Scan codebase
- [x] Select implementation approach
- [x] Plan architecture
- [x] **User approved this plan**

### Phase 2: Execution
- [x] Add project persistence and schemas
- [x] Add project CRUD and matching API
- [x] Add project library, form, and detail UI
- [x] Update plan status to "Ready for Validation"

### Phase 3: Validation
- [x] Build backend
- [x] Build frontend
- [x] Update validation proof

### Phase 4: Deployment
- [ ] Not part of this task

---

## 7. Validation Proof

| Check | Command Run | Result | Timestamp |
|-------|-------------|--------|-----------|
| Backend build | `dotnet build backend/RigMatch.Api.csproj -p:OutDir=bin/verify/` | ✅ Pass | 2026-03-11 |
| Frontend build | `npm run build` | ✅ Pass (bundle warning only) | 2026-03-11 |

**Validated by:** local build verification
**Validation timestamp:** 2026-03-11

---

## 8. Files to Generate

| File | Purpose | Status |
|------|---------|--------|
| `.azure/plan.md` | Feature plan | ✅ |
| `backend/Data/Entities/CompanyProject.cs` | Project persistence | ✅ |
| `backend/Controllers/CompanyProjectsController.cs` | Employer project API | ✅ |
| `backend/Services/ProjectMatchingService.cs` | Candidate matching | ✅ |
| `frontend/src/app/*` | Employer project UI | ✅ |

---

## 9. Next Steps

> Current: Execution

1. Add backend project entity, bootstrap schema, and matching service.
2. Add frontend project create/library/detail screens inside the existing app shell.
3. Build backend and frontend and record validation proof.
