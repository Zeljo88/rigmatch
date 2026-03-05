# Azure Deployment Plan

> **Status:** Executing

Generated: 2026-03-05T00:00:00+01:00

---

## 1. Project Overview

**Goal:** Build Day 1-6 of RigMatch MVP with company focus: single CV upload, parsed edit/save flow, and company CV library listing.

**Path:** New Project

---

## 2. Requirements

| Attribute | Value |
|-----------|-------|
| Classification | Development |
| Scale | Small |
| Budget | Cost-Optimized |
| **Subscription** | To be confirmed before Azure deployment |
| **Location** | To be confirmed before Azure deployment |

---

## 3. Components Detected

| Component | Type | Technology | Path |
|-----------|------|------------|------|
| backend | API | .NET Web API | `backend/` |
| frontend | Frontend | Angular | `frontend/` |

---

## 4. Recipe Selection

**Selected:** AZD (Bicep)

**Rationale:** New Azure-first multi-component app; simplest baseline path for later deployment stages.

---

## 5. Architecture

**Stack:** App Service

### Service Mapping

| Component | Azure Service | SKU |
|-----------|---------------|-----|
| frontend | Azure App Service / Static Web Apps (TBD later) | Basic/Free during MVP |
| backend | Azure App Service | Basic |

### Supporting Services

| Service | Purpose |
|---------|---------|
| Log Analytics | Centralized logging |
| Application Insights | Monitoring & APM |
| Key Vault | Secrets management |
| Managed Identity | Service-to-service auth |

---

## 6. Execution Checklist

### Phase 1: Planning
- [x] Analyze workspace
- [x] Gather requirements
- [ ] Confirm subscription and location with user
- [x] Scan codebase
- [x] Select recipe
- [x] Plan architecture
- [x] **User approved this plan**

### Phase 2: Execution
- [x] Research components (load references, invoke skills)
- [ ] Generate infrastructure files (deferred to Azure prep stage)
- [x] Generate application configuration
- [ ] Generate Dockerfiles (if containerized, deferred)
- [ ] Update plan status to "Ready for Validation"

### Phase 3: Validation
- [ ] Invoke azure-validate skill
- [ ] All validation checks pass
- [ ] Update plan status to "Validated"
- [ ] Record validation proof below

### Phase 4: Deployment
- [ ] Invoke azure-deploy skill
- [ ] Deployment successful
- [ ] Update plan status to "Deployed"

---

## 7. Validation Proof

Pending.

---

## 8. Files to Generate

| File | Purpose | Status |
|------|---------|--------|
| `.azure/plan.md` | This plan | ✅ |
| `backend/*` | API scaffold + CV upload endpoint | ✅ |
| `frontend/*` | Angular scaffold + upload UI | ✅ |

---

## 9. Next Steps

> Current: Day 6 company CV library workflow complete, Day 7 polish and resilience next

1. Start Day 7 by adding loading/error polish and validation for company library workflow.
2. Stabilize frontend runtime by aligning Node version with Angular support matrix.
