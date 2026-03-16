import json
from pathlib import Path
from html import escape

ROOT = Path('/home/azureuser/.openclaw/workspace-builderrigmatch/rigmatch/evaluation')
SPECS = ROOT / 'generated' / 'synthetic-cv-specs.json'
OUT_HTML = ROOT / 'cvs'
OUT_MD = ROOT / 'generated' / 'markdown'

OUT_HTML.mkdir(parents=True, exist_ok=True)
OUT_MD.mkdir(parents=True, exist_ok=True)

specs = json.loads(SPECS.read_text())

STYLE = """
body { font-family: Arial, Helvetica, sans-serif; margin: 28px; color: #111827; }
.cv { max-width: 900px; margin: 0 auto; }
.header { border-bottom: 2px solid #cbd5e1; margin-bottom: 14px; padding-bottom: 10px; }
.header h1 { margin: 0; font-size: 28px; }
.header .meta { margin-top: 6px; color: #475569; font-size: 14px; }
.section { margin: 16px 0; }
.section h2 { font-size: 16px; margin: 0 0 8px 0; text-transform: uppercase; letter-spacing: 0.04em; color: #1e3a8a; }
.two-col { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; }
.role { margin-bottom: 12px; }
.role .title { font-weight: bold; }
.role .company { color: #1f2937; }
.role .dates { color: #64748b; font-size: 13px; }
.tag-list { display: flex; flex-wrap: wrap; gap: 6px; }
.tag { background: #eff6ff; border: 1px solid #bfdbfe; padding: 4px 8px; border-radius: 999px; font-size: 12px; }
.noisy { letter-spacing: 0.05em; }
.tableish table { width: 100%; border-collapse: collapse; font-size: 13px; }
.tableish th, .tableish td { border: 1px solid #cbd5e1; padding: 6px 8px; text-align: left; }
.small { font-size: 12px; color: #475569; }
.ocr { font-family: 'Courier New', monospace; font-size: 13px; line-height: 1.3; }
.ar { direction: rtl; text-align: right; font-family: Arial, sans-serif; }
hr { border: none; border-top: 1px solid #e2e8f0; margin: 14px 0; }
"""


def role_html(role):
    title, company, start, end = role
    return f"<div class='role'><div class='title'>{escape(title)}</div><div class='company'>{escape(company)}</div><div class='dates'>{escape(start)} – {escape(end)}</div></div>"


def role_md(role):
    title, company, start, end = role
    return f"- **{title}**, {company} ({start} - {end})"


def base_sections(spec):
    certs = ''.join(f"<span class='tag'>{escape(c)}</span>" for c in spec['certifications'])
    skills = ''.join(f"<span class='tag'>{escape(s)}</span>" for s in spec['skills'])
    roles = ''.join(role_html(r) for r in spec['roles'])
    return f"""
    <div class='section'><h2>Professional Summary</h2><p>{escape(spec['summary'])}</p></div>
    <div class='section'><h2>Experience</h2>{roles}</div>
    <div class='section'><h2>Education</h2><p>{escape(spec['education'])}</p></div>
    <div class='section'><h2>Certifications</h2><div class='tag-list'>{certs}</div></div>
    <div class='section'><h2>Core Skills</h2><div class='tag-list'>{skills}</div></div>
    """


def render_html(spec):
    header = f"""
    <div class='header'>
      <h1>{escape(spec['name'])}</h1>
      <div class='meta'>{escape(spec['title'])} · {escape(spec['location'])} · {escape(spec['language']).upper()}</div>
    </div>
    """

    if spec['variant'] == 'two-column':
        certs = '<br>'.join(escape(c) for c in spec['certifications'])
        skills = '<br>'.join(escape(s) for s in spec['skills'])
        roles = ''.join(role_html(r) for r in spec['roles'])
        content = f"""
        {header}
        <div class='two-col'>
          <div>
            <div class='section'><h2>Profile</h2><p>{escape(spec['summary'])}</p></div>
            <div class='section'><h2>Education</h2><p>{escape(spec['education'])}</p></div>
            <div class='section'><h2>Certificates</h2><p class='small'>{certs}</p></div>
            <div class='section'><h2>Skills</h2><p class='small'>{skills}</p></div>
          </div>
          <div>
            <div class='section'><h2>Career History</h2>{roles}</div>
          </div>
        </div>
        """
    elif spec['variant'] == 'multilingual-ar-en':
        ar_block = "<div class='section ar'><h2>ملخص</h2><p>مهندس مواقع آبار بخبرة في التدخلات والصيانة الميدانية ودعم العمليات البرية.</p></div>"
        content = header + ar_block + base_sections(spec)
    elif spec['variant'] == 'multilingual-fr-en':
        fr_block = "<div class='section'><h2>Résumé</h2><p>Géologue bilingue avec expérience en caractérisation de réservoirs et interprétation sismique.</p></div>"
        content = header + fr_block + base_sections(spec)
    elif spec['variant'] == 'multilingual-es-en':
        es_block = "<div class='section'><h2>Resumen</h2><p>Inspector de ductos bilingüe con experiencia en integridad, corrosión y reportes de campo.</p></div>"
        content = header + es_block + base_sections(spec)
    elif spec['variant'] == 'short':
        content = header + f"<div class='section'><h2>Summary</h2><p>{escape(spec['summary'])}</p></div><div class='section'><h2>Experience</h2>{''.join(role_html(r) for r in spec['roles'])}</div><div class='section'><h2>Certifications</h2><p>{', '.join(map(escape, spec['certifications']))}</p></div>"
    elif spec['variant'] == 'short-messy':
        roles = '<br>'.join(f"{escape(t)} / {escape(c)} / {escape(s)} - {escape(e)}" for t, c, s, e in spec['roles'])
        content = f"""
        {header}
        <div class='section noisy'>PROFILE: {escape(spec['summary'])}</div>
        <div class='section noisy'>CAREER HISTORY<br>{roles}</div>
        <div class='section noisy'>CERTS {', '.join(map(escape, spec['certifications']))}</div>
        <div class='section noisy'>EDUCATION {escape(spec['education'])}</div>
        """
    elif spec['variant'] == 'long-senior':
        extra = "<div class='section'><h2>Selected Projects</h2><ul><li>Brownfield debottlenecking</li><li>Asset surveillance program</li><li>Production optimization workstreams</li><li>Mentoring younger engineers</li></ul></div>"
        content = header + base_sections(spec) + extra
    elif spec['variant'] == 'table-heavy':
        rows = ''.join(f"<tr><td>{escape(t)}</td><td>{escape(c)}</td><td>{escape(s)}</td><td>{escape(e)}</td></tr>" for t, c, s, e in spec['roles'])
        content = f"""
        {header}
        <div class='section'><h2>Summary</h2><p>{escape(spec['summary'])}</p></div>
        <div class='section tableish'><h2>Experience Matrix</h2><table><thead><tr><th>Role</th><th>Company</th><th>Start</th><th>End</th></tr></thead><tbody>{rows}</tbody></table></div>
        <div class='section tableish'><h2>Skills Matrix</h2><table><tbody>{''.join(f'<tr><td>{escape(s)}</td><td>Advanced</td></tr>' for s in spec['skills'])}</tbody></table></div>
        <div class='section'><h2>Education</h2><p>{escape(spec['education'])}</p></div>
        """
    elif spec['variant'] == 'ocr-like':
        lines = [
            f"NAME {spec['name']}",
            f"ROLE {spec['title']}",
            f"LOCAT1ON {spec['location']}",
            "",
            spec['summary'].replace('i', '1').replace('o', '0'),
            "",
            "CAREER",
        ]
        lines += [f"{t} // {c} // {s} -- {e}" for t, c, s, e in spec['roles']]
        lines += ["", f"EDUCAT10N {spec['education']}", f"CERTS {' | '.join(spec['certifications'])}"]
        content = header + f"<div class='section ocr'>{'<br>'.join(escape(line) for line in lines)}</div>"
    elif spec['variant'] == 'many-short-roles':
        content = header + base_sections(spec) + "<div class='section'><h2>Consulting Notes</h2><p>Available for project-based offshore assignments, mobilization within 3 weeks.</p></div>"
    elif spec['variant'] == 'all-caps':
        roles = ''.join(f"<div class='role'><div class='title'>{escape(t.upper())}</div><div class='company'>{escape(c.upper())}</div><div class='dates'>{escape(s.upper())} - {escape(e.upper())}</div></div>" for t, c, s, e in spec['roles'])
        content = f"""
        <div class='header noisy'>
          <h1>{escape(spec['name'])}</h1>
          <div class='meta'>{escape(spec['title']).upper()} / {escape(spec['location']).upper()}</div>
        </div>
        <div class='section noisy'><h2>PROFILE</h2><p>{escape(spec['summary']).upper()}</p></div>
        <div class='section noisy'><h2>EXPERIENCE</h2>{roles}</div>
        <div class='section noisy'><h2>EDUCATION</h2><p>{escape(spec['education']).upper()}</p></div>
        <div class='section noisy'><h2>SKILLS</h2><p>{' | '.join(escape(s.upper()) for s in spec['skills'])}</p></div>
        """
    else:
        content = header + base_sections(spec)

    return f"""<!doctype html><html><head><meta charset='utf-8'><title>{escape(spec['id'])}</title><style>{STYLE}</style></head><body><div class='cv'>{content}</div></body></html>"""


def render_md(spec):
    lines = [
        f"# {spec['name']}",
        f"**Target title:** {spec['title']}",
        f"**Location:** {spec['location']}",
        f"**Language:** {spec['language']}",
        '',
        '## Summary',
        spec['summary'],
        '',
        '## Experience',
    ]
    lines.extend(role_md(r) for r in spec['roles'])
    lines += [
        '', '## Education', spec['education'], '',
        '## Certifications', ', '.join(spec['certifications']), '',
        '## Skills', ', '.join(spec['skills']), ''
    ]
    return '\n'.join(lines)


for spec in specs:
    html = render_html(spec)
    md = render_md(spec)
    (OUT_HTML / f"{spec['id']}_{spec['variant']}.html").write_text(html)
    (OUT_MD / f"{spec['id']}_{spec['variant']}.md").write_text(md)

print(f"generated {len(specs)} HTML CVs and {len(specs)} markdown source files")
