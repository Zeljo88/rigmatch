# RigMatch Execution Plan

## Goal
Stabilize the current MVP before deeper parsing/matching improvements.

## Principles
- Fix trust-breaking bugs before adding sophistication.
- Add guardrails where product intent is already clear.
- Keep changes small and testable.
- Do not expand scope into a full matching redesign yet.

## Current priority order

### P0 — Fix trust-breaking issues first
1. **CV library search filter bug**
   - Fix backend search so `location` does not incorrectly flow into education filtering.
   - Acceptance criteria:
     - education filter only filters by education
     - location is not silently applied as education
     - unfiltered search behavior remains unchanged

2. **Candidate PDF download flow**
   - Replace browser-anchor download with authenticated HTTP download so JWT auth is preserved.
   - Acceptance criteria:
     - authenticated users can download candidate PDFs from candidate detail
     - unauthorized users still cannot access downloads
     - downloaded file is a PDF with a reasonable filename

3. **Zero-role-match candidates in project matching**
   - Do not surface candidates as normal matches when they do not match the primary or acceptable role set.
   - Acceptance criteria:
     - candidates with no role match are excluded from project results
     - exact and acceptable role matches still rank normally
     - non-project search/library behavior is unchanged

### P1 — Tighten product trust semantics
4. **Separate saved vs reviewed vs match-ready**
5. **Make required criteria act like hard requirements by default**
6. **Clarify match status/eligibility in project results**

### P2 — Improve review quality and data quality
7. **Improve mapping evidence in review flow**
8. **Handle unmapped / out-of-taxonomy roles explicitly**
9. **Improve duplicate workflow beyond warnings**
10. **Tighten skills/cert matching normalization**

## Testing focus during implementation
For each P0/P1 change:
- verify the main happy path manually
- add the smallest practical automated regression coverage available in the repo
- avoid broad refactors unless required for the fix

## Immediate working sequence
1. P0 search bug
2. P0 PDF download auth fix
3. P0 zero-role-match exclusion
4. commit
5. then move to saved/reviewed/match-ready semantics
