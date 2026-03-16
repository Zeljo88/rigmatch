# RigMatch — Prioritized Issues from 20-CV Benchmark

Source inputs:
- `evaluation/generated/benchmark-report.md`
- `evaluation/generated/benchmark-results.json`

Benchmark scope:
- 20 synthetic oil-industry CVs
- real Azure OpenAI parsing path
- varied layouts: clean, multilingual, short, long senior, unusual-title, table-heavy, OCR-like, all-caps

## Summary

Top failure categories from the benchmark:
- **schema-and-review-flow**: 20/20
- **experience-extraction**: 9/20
- **certification-normalization**: 3/20
- **skills-normalization**: 3/20
- **validation-and-review-thresholds**: 3/20
- **role-normalization**: 1/20
- **text-extraction-and-header-parsing**: 1/20

---

## P0 — Fix first

### 1. Add location to the parsing + storage + review model
**Why:** failed in **20/20** CVs because location is not represented in the parsed profile schema.

**Current problem**
- parser output does not capture location
- stored/finalized profile cannot hold reliable normalized location
- matching/search currently cannot trust location-based logic

**What to change**
- extend parsed profile model to include location
- extract location in parser prompt/schema
- add normalized location handling in backend
- surface location in edit/review UI
- only use location in matching when present and reviewed/trustworthy

**Acceptance criteria**
- uploaded CVs can store parsed location
- reviewer can edit/confirm location
- search/matching use reviewed location instead of missing/null values
- benchmark rerun shows location present on most clean CVs

### 2. Improve experience extraction and job-title segmentation
**Why:** failed in **9/20** CVs; biggest parsing weakness after location.

**Current problem**
- long senior CVs lose title fidelity
- mixed-language CVs reduce title extraction quality
- noisy/all-caps formatting weakens experience parsing
- unusual/internal titles are inconsistently preserved before normalization

**What to change**
- strengthen prompt/schema for preserving raw role title and company boundaries
- improve text preprocessing before AI parse for long/noisy CVs
- preserve chronology blocks more explicitly
- add regression tests for long-senior, multilingual, and all-caps layouts

**Acceptance criteria**
- clean CVs do not miss straightforward titles
- long-senior CVs preserve top roles better
- multilingual CVs retain expected role titles more often
- benchmark rerun reduces `experience-extraction` failures materially

### 3. Treat location and experience issues as benchmark-gating bugs, not “future enhancements”
**Why:** these are the dominant reasons the current system can look correct while still ranking/searching badly.

**Action**
- add benchmark rerun as a required validation step before matching changes

---

## P1 — Next after P0

### 4. Normalize certifications more deliberately
**Why:** failed in **3/20** CVs, including clean and table-heavy cases.

**Observed issue**
- examples like `OPITO FOET` split into separate tokens (`OPITO`, `FOET`)

**What to change**
- certification alias dictionary
- phrase-preserving normalization
- dedupe and canonical forms

**Acceptance criteria**
- combined cert phrases remain meaningful
- equivalent cert names normalize consistently

### 5. Normalize skills more deliberately
**Why:** failed in **3/20** CVs, especially sparse and OCR-like CVs.

**What to change**
- domain lexicon for oil & gas skills
- phrase preservation
- dedupe and cleanup of noisy tokens
- reviewer-friendly skill editing when extraction confidence is low

**Acceptance criteria**
- sparse/noisy CVs retain better skill coverage
- less brittle skill token splitting

### 6. Tighten review thresholds (`needsReview`)
**Why:** **3/20** CVs that should be reasonably straightforward still remained unresolved.

**What to change**
- tighten auto-match rules for easy cases
- make uncertain cases more explicit with stronger evidence/reason codes
- distinguish “confident auto-match” from “accepted but should review” more clearly

**Acceptance criteria**
- obviously clean CVs stop surfacing unnecessary review flags
- ambiguous cases still remain reviewable

---

## P2 — Important but smaller scope

### 7. Improve role normalization for edge cases
**Why:** only **1/20** direct benchmark failure, but still important for trust.

**Focus**
- long-senior CV role mapping drift
- operator-family and unusual-title regression cases
- better evidence shown to reviewer for why a role mapped a certain way

### 8. Improve header/name extraction for noisy formats
**Why:** **1/20** clear failure, but high severity when it happens.

**Focus**
- all-caps/noisy header handling
- preserve top-of-document text ordering
- more robust contact block detection

---

## Recommended implementation order
1. **location model + UI review flow**
2. **experience/title extraction improvements**
3. **certification normalization**
4. **skills normalization**
5. **review-threshold tightening**
6. **role normalization edge-case cleanup**
7. **header/name extraction hardening**

---

## Recommended validation loop
After each major fix:
1. rerun the 20-CV benchmark
2. compare failure-category counts to the current baseline
3. only then adjust matching behavior further

---

## Practical next 3 engineering tasks
1. Add `Location` through parsed profile, stored profile, save path, and review UI.
2. Improve experience/title extraction for long-senior + multilingual + noisy CVs.
3. Add certification normalization aliases/canonicalization and rerun the benchmark.
