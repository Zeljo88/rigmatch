# RigMatch Synthetic CV Benchmark

- Total CVs evaluated: **20**

## Failure categories
- **schema-and-review-flow**: 20
- **certification-normalization**: 5
- **experience-extraction**: 5
- **skills-normalization**: 3
- **validation-and-review-thresholds**: 3
- **role-normalization**: 1
- **text-extraction-and-header-parsing**: 1

## Per-CV results

### CV01 — clean
- Passed fields: name, highestEducation, jobTitles, companies, skills, experienceYears, roleNormalization, needsReview
- Failed fields:
  - `location` | severity: **high** | cause: Profile schema does not capture location, so matching/search cannot rely on it | fix: Add location extraction + normalized location model + review/edit support
  - `certifications` | severity: **medium** | cause: Parser/normalizer missed a field that should be straightforward | fix: Normalize certifications using alias dictionaries (for example OPITO FOET vs FOET) and structured splitting rules.

### CV02 — clean
- Passed fields: name, highestEducation, jobTitles, companies, skills, certifications, experienceYears, roleNormalization, needsReview
- Failed fields:
  - `location` | severity: **high** | cause: Profile schema does not capture location, so matching/search cannot rely on it | fix: Add location extraction + normalized location model + review/edit support

### CV03 — two-column
- Passed fields: name, highestEducation, jobTitles, companies, skills, certifications, experienceYears, roleNormalization
- Failed fields:
  - `location` | severity: **high** | cause: Profile schema does not capture location, so matching/search cannot rely on it | fix: Add location extraction + normalized location model + review/edit support

### CV04 — multilingual-ar-en
- Passed fields: name, highestEducation, jobTitles, companies, skills, certifications, experienceYears, roleNormalization
- Failed fields:
  - `location` | severity: **high** | cause: Profile schema does not capture location, so matching/search cannot rely on it | fix: Add location extraction + normalized location model + review/edit support

### CV05 — multilingual-fr-en
- Passed fields: name, highestEducation, jobTitles, companies, skills, certifications, experienceYears, roleNormalization
- Failed fields:
  - `location` | severity: **high** | cause: Profile schema does not capture location, so matching/search cannot rely on it | fix: Add location extraction + normalized location model + review/edit support

### CV06 — short
- Passed fields: name, highestEducation, jobTitles, companies, certifications, experienceYears, roleNormalization
- Failed fields:
  - `location` | severity: **high** | cause: Profile schema does not capture location, so matching/search cannot rely on it | fix: Add location extraction + normalized location model + review/edit support
  - `skills` | severity: **medium** | cause: Sparse source CV left little context for extraction | fix: Normalize skill extraction with deduping, phrase preservation, and domain lexicon support.

### CV07 — short-messy
- Passed fields: name, highestEducation, companies, experienceYears, roleNormalization
- Failed fields:
  - `location` | severity: **high** | cause: Profile schema does not capture location, so matching/search cannot rely on it | fix: Add location extraction + normalized location model + review/edit support
  - `jobTitles` | severity: **high** | cause: Sparse + inconsistent date formatting likely reduced parser confidence | fix: Improve experience-section segmentation and title extraction before role normalization.
  - `skills` | severity: **medium** | cause: Sparse + inconsistent date formatting likely reduced parser confidence | fix: Normalize skill extraction with deduping, phrase preservation, and domain lexicon support.
  - `certifications` | severity: **medium** | cause: Sparse + inconsistent date formatting likely reduced parser confidence | fix: Normalize certifications using alias dictionaries (for example OPITO FOET vs FOET) and structured splitting rules.

### CV08 — long-senior
- Passed fields: name, highestEducation, jobTitles, companies, skills, certifications, experienceYears
- Failed fields:
  - `location` | severity: **high** | cause: Profile schema does not capture location, so matching/search cannot rely on it | fix: Add location extraction + normalized location model + review/edit support
  - `roleNormalization` | severity: **high** | cause: Long senior CV likely hit summarization/truncation or chronology simplification | fix: Expand role taxonomy and alias handling, and surface stronger reviewer evidence for uncertain mappings.
  - `needsReview` | severity: **medium** | cause: Long senior CV likely hit summarization/truncation or chronology simplification | fix: Tighten auto-match thresholds so straightforward CVs do not remain unresolved.

### CV09 — long-senior
- Passed fields: name, highestEducation, jobTitles, companies, skills, certifications, experienceYears, roleNormalization
- Failed fields:
  - `location` | severity: **high** | cause: Profile schema does not capture location, so matching/search cannot rely on it | fix: Add location extraction + normalized location model + review/edit support
  - `needsReview` | severity: **medium** | cause: Long senior CV likely hit summarization/truncation or chronology simplification | fix: Tighten auto-match thresholds so straightforward CVs do not remain unresolved.

### CV10 — unusual-title
- Passed fields: name, highestEducation, jobTitles, companies, skills, certifications, experienceYears, roleNormalization
- Failed fields:
  - `location` | severity: **high** | cause: Profile schema does not capture location, so matching/search cannot rely on it | fix: Add location extraction + normalized location model + review/edit support

### CV11 — unusual-title
- Passed fields: name, highestEducation, jobTitles, companies, skills, certifications, experienceYears, roleNormalization
- Failed fields:
  - `location` | severity: **high** | cause: Profile schema does not capture location, so matching/search cannot rely on it | fix: Add location extraction + normalized location model + review/edit support

### CV12 — clean
- Passed fields: name, highestEducation, companies, skills, certifications, experienceYears, roleNormalization
- Failed fields:
  - `location` | severity: **high** | cause: Profile schema does not capture location, so matching/search cannot rely on it | fix: Add location extraction + normalized location model + review/edit support
  - `jobTitles` | severity: **high** | cause: Parser/normalizer missed a field that should be straightforward | fix: Improve experience-section segmentation and title extraction before role normalization.
  - `needsReview` | severity: **high** | cause: Parser/normalizer missed a field that should be straightforward | fix: Tighten auto-match thresholds so straightforward CVs do not remain unresolved.

### CV13 — table-heavy
- Passed fields: name, highestEducation, jobTitles, companies, skills, experienceYears, roleNormalization
- Failed fields:
  - `location` | severity: **high** | cause: Profile schema does not capture location, so matching/search cannot rely on it | fix: Add location extraction + normalized location model + review/edit support
  - `certifications` | severity: **medium** | cause: Table-heavy formatting likely reduced extraction fidelity | fix: Normalize certifications using alias dictionaries (for example OPITO FOET vs FOET) and structured splitting rules.

### CV14 — ocr-like
- Passed fields: name, highestEducation, jobTitles, companies, certifications, experienceYears, roleNormalization
- Failed fields:
  - `location` | severity: **high** | cause: Profile schema does not capture location, so matching/search cannot rely on it | fix: Add location extraction + normalized location model + review/edit support
  - `skills` | severity: **medium** | cause: OCR-like/noisy formatting degraded text extraction | fix: Normalize skill extraction with deduping, phrase preservation, and domain lexicon support.

### CV15 — many-short-roles
- Passed fields: name, highestEducation, jobTitles, companies, skills, certifications, experienceYears, roleNormalization
- Failed fields:
  - `location` | severity: **high** | cause: Profile schema does not capture location, so matching/search cannot rely on it | fix: Add location extraction + normalized location model + review/edit support

### CV16 — clean
- Passed fields: name, highestEducation, jobTitles, companies, skills, experienceYears, roleNormalization, needsReview
- Failed fields:
  - `location` | severity: **high** | cause: Profile schema does not capture location, so matching/search cannot rely on it | fix: Add location extraction + normalized location model + review/edit support
  - `certifications` | severity: **medium** | cause: Parser/normalizer missed a field that should be straightforward | fix: Normalize certifications using alias dictionaries (for example OPITO FOET vs FOET) and structured splitting rules.

### CV17 — all-caps
- Passed fields: highestEducation, skills, experienceYears, roleNormalization
- Failed fields:
  - `name` | severity: **high** | cause: Name extraction/parsing error | fix: Improve header/contact extraction heuristics and preserve top-of-document text ordering.
  - `location` | severity: **high** | cause: Profile schema does not capture location, so matching/search cannot rely on it | fix: Add location extraction + normalized location model + review/edit support
  - `jobTitles` | severity: **high** | cause: All-caps noisy formatting weakened normalization and token quality | fix: Improve experience-section segmentation and title extraction before role normalization.
  - `companies` | severity: **medium** | cause: All-caps noisy formatting weakened normalization and token quality | fix: Improve company/title boundary detection in experience parsing.
  - `certifications` | severity: **medium** | cause: All-caps noisy formatting weakened normalization and token quality | fix: Normalize certifications using alias dictionaries (for example OPITO FOET vs FOET) and structured splitting rules.

### CV18 — domain-adjacent
- Passed fields: name, highestEducation, jobTitles, companies, skills, certifications, experienceYears, roleNormalization
- Failed fields:
  - `location` | severity: **high** | cause: Profile schema does not capture location, so matching/search cannot rely on it | fix: Add location extraction + normalized location model + review/edit support

### CV19 — multilingual-es-en
- Passed fields: name, highestEducation, companies, skills, certifications, experienceYears, roleNormalization
- Failed fields:
  - `location` | severity: **high** | cause: Profile schema does not capture location, so matching/search cannot rely on it | fix: Add location extraction + normalized location model + review/edit support
  - `jobTitles` | severity: **high** | cause: Mixed-language CV likely reduced extraction/normalization accuracy | fix: Improve experience-section segmentation and title extraction before role normalization.

### CV20 — normalization-regression
- Passed fields: name, highestEducation, jobTitles, companies, skills, certifications, experienceYears, roleNormalization, needsReview
- Failed fields:
  - `location` | severity: **high** | cause: Profile schema does not capture location, so matching/search cannot rely on it | fix: Add location extraction + normalized location model + review/edit support

## Grouped improvement recommendations
- **schema-and-review-flow**
  - CV01 / location: Add location extraction + normalized location model + review/edit support
  - CV02 / location: Add location extraction + normalized location model + review/edit support
  - CV03 / location: Add location extraction + normalized location model + review/edit support
- **certification-normalization**
  - CV01 / certifications: Normalize certifications using alias dictionaries (for example OPITO FOET vs FOET) and structured splitting rules.
  - CV07 / certifications: Normalize certifications using alias dictionaries (for example OPITO FOET vs FOET) and structured splitting rules.
  - CV13 / certifications: Normalize certifications using alias dictionaries (for example OPITO FOET vs FOET) and structured splitting rules.
- **skills-normalization**
  - CV06 / skills: Normalize skill extraction with deduping, phrase preservation, and domain lexicon support.
  - CV07 / skills: Normalize skill extraction with deduping, phrase preservation, and domain lexicon support.
  - CV14 / skills: Normalize skill extraction with deduping, phrase preservation, and domain lexicon support.
- **experience-extraction**
  - CV07 / jobTitles: Improve experience-section segmentation and title extraction before role normalization.
  - CV12 / jobTitles: Improve experience-section segmentation and title extraction before role normalization.
  - CV17 / jobTitles: Improve experience-section segmentation and title extraction before role normalization.
- **role-normalization**
  - CV08 / roleNormalization: Expand role taxonomy and alias handling, and surface stronger reviewer evidence for uncertain mappings.
- **validation-and-review-thresholds**
  - CV08 / needsReview: Tighten auto-match thresholds so straightforward CVs do not remain unresolved.
  - CV09 / needsReview: Tighten auto-match thresholds so straightforward CVs do not remain unresolved.
  - CV12 / needsReview: Tighten auto-match thresholds so straightforward CVs do not remain unresolved.
- **text-extraction-and-header-parsing**
  - CV17 / name: Improve header/contact extraction heuristics and preserve top-of-document text ordering.