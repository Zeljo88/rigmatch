# Azure Deployment Plan

> **Status:** Ready for Validation

Generated: 2026-03-11T13:20:00+01:00

---

## 1. Project Overview

**Goal:** Add employer authentication to RigMatch so CVs and projects belong to authenticated employer users and their company instead of the development-only `X-Company-Id` header.

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
| Employer authentication | Existing API and frontend | JWT auth on top of .NET API and Angular app |

### Supporting Services

| Service | Purpose |
|---------|---------|
| SQLite | Development persistence |
| Azure OpenAI | CV parsing and extraction |
| JWT auth | Employer login/session enforcement |

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
- [x] Add employer user persistence and auth configuration
- [x] Add register/login/me API and JWT issuance
- [x] Secure CV/project endpoints to use authenticated company context
- [x] Add frontend login/register/session flow
- [x] Remove dependency on `X-Company-Id` in normal app flow
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
| `.azure/plan.md` | Feature plan | ✅ |
| `backend/Data/Entities/EmployerUser.cs` | Employer auth persistence | ✅ |
| `backend/Controllers/AuthController.cs` | Auth API | ✅ |
| `backend/Services/*` | JWT/session/auth helpers | ✅ |
| `frontend/src/app/*` | Login/register/session UI | ✅ |

---

## 9. Next Steps

> Current: Validation

1. Restart backend so the new auth schema and JWT middleware are active.
2. Register the first employer account in the frontend and test login, CV library, and projects.
3. Replace the development signing key before any shared deployment.
