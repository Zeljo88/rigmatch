import json
import re
import subprocess
import time
import uuid
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any
from urllib import request, error

BASE_URL = 'http://127.0.0.1:5168'
ROOT = Path('/home/azureuser/.openclaw/workspace-builderrigmatch/rigmatch/evaluation')
SPECS_PATH = ROOT / 'generated' / 'synthetic-cv-specs.json'
PDF_DIR = ROOT / 'pdfs'
OUT_JSON = ROOT / 'generated' / 'benchmark-results.json'
OUT_MD = ROOT / 'generated' / 'benchmark-report.md'


def normalize(text: str) -> str:
    return re.sub(r'[^a-z0-9]+', ' ', (text or '').lower()).strip()


def normalize_compact(text: str) -> str:
    return re.sub(r'[^a-z0-9]+', '', (text or '').lower())


def jaccard(expected: list[str], actual: list[str]) -> float:
    a = {normalize(x) for x in expected if x}
    b = {normalize(x) for x in actual if x}
    if not a and not b:
        return 1.0
    if not a or not b:
        return 0.0
    return len(a & b) / len(a | b)


def register_user() -> str:
    payload = {
        'companyName': 'RigMatch Eval Co',
        'fullName': 'RigMatch Tester',
        'email': f'tester-{uuid.uuid4().hex[:8]}@rigmatch.local',
        'password': 'TestPass123!'
    }
    req = request.Request(
        BASE_URL + '/auth/register',
        data=json.dumps(payload).encode(),
        headers={'Content-Type': 'application/json'}
    )
    with request.urlopen(req, timeout=30) as resp:
        data = json.loads(resp.read().decode())
    return data['token']


def upload_cv(token: str, pdf_path: Path, max_attempts: int = 5) -> dict[str, Any]:
    for attempt in range(1, max_attempts + 1):
        result = subprocess.run([
            'curl', '-sS', '-X', 'POST', BASE_URL + '/company/cv/upload',
            '-H', f'Authorization: Bearer {token}',
            '-F', f'file=@{pdf_path};type=application/pdf'
        ], capture_output=True, text=True, timeout=420)
        if result.returncode != 0:
            raise RuntimeError(f'curl failed for {pdf_path.name}: {result.stderr}')
        try:
            data = json.loads(result.stdout)
        except json.JSONDecodeError as ex:
            raise RuntimeError(f'Invalid JSON for {pdf_path.name}: {result.stdout[:500]}') from ex

        if isinstance(data, dict) and data.get('retryAfterSeconds'):
            wait_s = int(data.get('retryAfterSeconds') or 10)
            time.sleep(wait_s + 1)
            continue
        return data
    raise RuntimeError(f'Exceeded retry attempts for {pdf_path.name}')


def years_expected(spec: dict[str, Any]) -> float:
    total = 0.0
    current_year = 2026
    current_month = 3
    for _, _, start, end in spec['roles']:
        def parse_ym(value: str):
            if value.lower() == 'present':
                return current_year, current_month
            if re.fullmatch(r'\d{4}-\d{2}', value):
                y, m = value.split('-')
                return int(y), int(m)
            if re.fullmatch(r'\d{4}', value):
                return int(value), 1
            return None
        s = parse_ym(start)
        e = parse_ym(end)
        if not s or not e:
            continue
        months = (e[0] - s[0]) * 12 + (e[1] - s[1])
        total += max(months, 0) / 12.0
    return round(total)


def assess(spec: dict[str, Any], parsed: dict[str, Any]) -> dict[str, Any]:
    failures = []
    passed = []
    profile = parsed['parsedProfile']
    variant = spec['variant']

    expected_titles = [r[0] for r in spec['roles']]
    expected_companies = [r[1] for r in spec['roles']]
    actual_titles = profile.get('jobTitles') or []
    actual_companies = profile.get('companies') or []
    actual_skills = profile.get('skills') or []
    actual_certs = profile.get('certifications') or []
    actual_experiences = profile.get('experiences') or []

    if normalize(spec['name']) == normalize(profile.get('name', '')):
        passed.append('name')
    else:
        failures.append(failure(spec, 'name', profile.get('name', ''), 'Name extraction/parsing error', 'high'))

    # Product/schema gap: location not captured at all.
    failures.append(failure(spec, 'location', '', 'Profile schema does not capture location, so matching/search cannot rely on it', 'high', 'Add location extraction + normalized location model + review/edit support'))

    if normalize(spec['education']) in normalize(profile.get('highestEducation', '')) or normalize(profile.get('highestEducation', '')) in normalize(spec['education']):
        passed.append('highestEducation')
    else:
        failures.append(failure(spec, 'highestEducation', profile.get('highestEducation', ''), likely_cause(variant, 'highestEducation'), severity_for('highestEducation', variant)))

    if jaccard(expected_titles, actual_titles) >= 0.67:
        passed.append('jobTitles')
    else:
        failures.append(failure(spec, 'jobTitles', actual_titles, likely_cause(variant, 'jobTitles'), severity_for('jobTitles', variant)))

    if jaccard(expected_companies, actual_companies) >= 0.67:
        passed.append('companies')
    else:
        failures.append(failure(spec, 'companies', actual_companies, likely_cause(variant, 'companies'), severity_for('companies', variant)))

    if jaccard(spec['skills'], actual_skills) >= 0.5:
        passed.append('skills')
    else:
        failures.append(failure(spec, 'skills', actual_skills, likely_cause(variant, 'skills'), severity_for('skills', variant)))

    if jaccard(spec['certifications'], actual_certs) >= 0.5:
        passed.append('certifications')
    else:
        failures.append(failure(spec, 'certifications', actual_certs, likely_cause(variant, 'certifications'), severity_for('certifications', variant)))

    expected_years = years_expected(spec)
    actual_years = profile.get('experienceYears')
    if isinstance(actual_years, int) and abs(actual_years - expected_years) <= 2:
        passed.append('experienceYears')
    else:
        failures.append(failure(spec, 'experienceYears', actual_years, likely_cause(variant, 'experienceYears'), severity_for('experienceYears', variant)))

    # role normalization check on top two roles
    expected_primary_norm = [normalize(x) for x in expected_titles[:2]]
    normalized_standard_roles = [normalize((exp.get('standardRoleName') or exp.get('rawRoleTitle') or '')) for exp in actual_experiences[:2]]
    if expected_primary_norm and any(x in normalized_standard_roles for x in expected_primary_norm):
        passed.append('roleNormalization')
    else:
        failures.append(failure(spec, 'roleNormalization', normalized_standard_roles, likely_cause(variant, 'roleNormalization'), severity_for('roleNormalization', variant)))

    if variant in {'clean', 'long-senior', 'normalization-regression'}:
        unresolved = [exp for exp in actual_experiences[:2] if exp.get('needsReview')]
        if unresolved:
            failures.append(failure(spec, 'needsReview', unresolved, likely_cause(variant, 'needsReview'), severity_for('needsReview', variant)))
        else:
            passed.append('needsReview')

    return {
        'cv_id': spec['id'],
        'cv_type': spec['type'] if 'type' in spec else spec['variant'],
        'variant': variant,
        'passed_fields': passed,
        'failed_fields': failures,
        'parsed_profile': profile,
    }


def failure(spec: dict[str, Any], field: str, actual: Any, cause: str, severity: str, recommended_fix: str | None = None) -> dict[str, Any]:
    return {
        'field': field,
        'actual': actual,
        'likely_cause': cause,
        'severity': severity,
        'recommended_fix': recommended_fix or recommend_fix(field)
    }


def severity_for(field: str, variant: str) -> str:
    if field in {'name', 'location', 'jobTitles', 'roleNormalization'}:
        return 'high'
    if field in {'companies', 'highestEducation', 'needsReview'}:
        return 'high' if variant in {'clean', 'normalization-regression'} else 'medium'
    if field in {'certifications', 'skills', 'experienceYears'}:
        return 'medium'
    return 'medium'


def likely_cause(variant: str, field: str) -> str:
    if field == 'location':
        return 'Location is not present in the parsed profile schema'
    variant_map = {
        'two-column': 'Two-column layout likely disrupted reading order',
        'table-heavy': 'Table-heavy formatting likely reduced extraction fidelity',
        'ocr-like': 'OCR-like/noisy formatting degraded text extraction',
        'multilingual-ar-en': 'Mixed-language CV likely reduced extraction/normalization accuracy',
        'multilingual-fr-en': 'Mixed-language CV likely reduced extraction/normalization accuracy',
        'multilingual-es-en': 'Mixed-language CV likely reduced extraction/normalization accuracy',
        'short': 'Sparse source CV left little context for extraction',
        'short-messy': 'Sparse + inconsistent date formatting likely reduced parser confidence',
        'unusual-title': 'Non-standard internal role title challenged role normalization',
        'many-short-roles': 'Many short contracts increased chronology and role aggregation complexity',
        'all-caps': 'All-caps noisy formatting weakened normalization and token quality',
        'domain-adjacent': 'Role is domain-adjacent and may not map cleanly into current taxonomy',
        'normalization-regression': 'Known operator-family normalization edge case',
        'clean': 'Parser/normalizer missed a field that should be straightforward',
        'long-senior': 'Long senior CV likely hit summarization/truncation or chronology simplification',
    }
    return variant_map.get(variant, 'Parsing or normalization weakness in current pipeline')


def recommend_fix(field: str) -> str:
    fixes = {
        'location': 'Extend parsed profile schema and UI review flow to capture normalized location and mobility preferences.',
        'name': 'Improve header/contact extraction heuristics and preserve top-of-document text ordering.',
        'highestEducation': 'Strengthen education section extraction and multilingual education normalization.',
        'jobTitles': 'Improve experience-section segmentation and title extraction before role normalization.',
        'companies': 'Improve company/title boundary detection in experience parsing.',
        'skills': 'Normalize skill extraction with deduping, phrase preservation, and domain lexicon support.',
        'certifications': 'Normalize certifications using alias dictionaries (for example OPITO FOET vs FOET) and structured splitting rules.',
        'experienceYears': 'Recompute experience from parsed chronology with better handling of year-only dates and overlaps.',
        'roleNormalization': 'Expand role taxonomy and alias handling, and surface stronger reviewer evidence for uncertain mappings.',
        'needsReview': 'Tighten auto-match thresholds so straightforward CVs do not remain unresolved.',
    }
    return fixes.get(field, 'Investigate and harden the relevant parsing/normalization path.')


def category_for(field: str) -> str:
    return {
        'location': 'schema-and-review-flow',
        'name': 'text-extraction-and-header-parsing',
        'highestEducation': 'education-extraction',
        'jobTitles': 'experience-extraction',
        'companies': 'experience-extraction',
        'skills': 'skills-normalization',
        'certifications': 'certification-normalization',
        'experienceYears': 'chronology-calculation',
        'roleNormalization': 'role-normalization',
        'needsReview': 'validation-and-review-thresholds',
    }.get(field, 'other')


def main() -> None:
    specs = json.loads(SPECS_PATH.read_text())
    token = register_user()
    results = []
    category_counter = Counter()
    category_examples = defaultdict(list)

    for spec in specs:
        pdf_candidates = sorted(PDF_DIR.glob(f"{spec['id']}_*.pdf"))
        if not pdf_candidates:
            raise FileNotFoundError(f'No PDF found for {spec["id"]}')
        parsed = upload_cv(token, pdf_candidates[0])
        assessed = assess(spec, parsed)
        results.append(assessed)
        for failure_item in assessed['failed_fields']:
            category = category_for(failure_item['field'])
            category_counter[category] += 1
            if len(category_examples[category]) < 3:
                category_examples[category].append({
                    'cv_id': spec['id'],
                    'field': failure_item['field'],
                    'cause': failure_item['likely_cause'],
                    'fix': failure_item['recommended_fix']
                })
        time.sleep(2)

    summary = {
        'total_cvs': len(results),
        'failure_categories': dict(category_counter.most_common()),
        'category_examples': dict(category_examples),
        'results': results,
    }
    OUT_JSON.write_text(json.dumps(summary, indent=2))
    OUT_MD.write_text(render_markdown(summary))
    print(json.dumps({
        'total_cvs': len(results),
        'failure_categories': dict(category_counter.most_common()),
        'report_json': str(OUT_JSON),
        'report_md': str(OUT_MD)
    }, indent=2))


def render_markdown(summary: dict[str, Any]) -> str:
    lines = [
        '# RigMatch Synthetic CV Benchmark',
        '',
        f"- Total CVs evaluated: **{summary['total_cvs']}**",
        '',
        '## Failure categories',
    ]
    for category, count in summary['failure_categories'].items():
        lines.append(f'- **{category}**: {count}')
    lines += ['', '## Per-CV results']
    for item in summary['results']:
        lines += [
            '',
            f"### {item['cv_id']} — {item['variant']}",
            f"- Passed fields: {', '.join(item['passed_fields']) if item['passed_fields'] else 'none'}",
        ]
        if not item['failed_fields']:
            lines.append('- Failed fields: none')
        else:
            lines.append('- Failed fields:')
            for failure in item['failed_fields']:
                lines.append(
                    f"  - `{failure['field']}` | severity: **{failure['severity']}** | cause: {failure['likely_cause']} | fix: {failure['recommended_fix']}"
                )
    lines += ['', '## Grouped improvement recommendations']
    for category, examples in summary['category_examples'].items():
        lines.append(f'- **{category}**')
        for ex in examples:
            lines.append(f"  - {ex['cv_id']} / {ex['field']}: {ex['fix']}")
    return '\n'.join(lines)


if __name__ == '__main__':
    main()
