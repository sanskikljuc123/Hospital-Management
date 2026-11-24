"""Draws the testing-flow diagrams used in the READMEs, from one list of steps.

Usage: python3 docs/images/make_testing_flow.py

Writes docs/images/testing-flow-overview.png (mind map, root Readme.md) and docs/images/testing-flow-lanes.png
(swimlanes, docs/testing/README.md). Both come from STEPS below, so a changed step changes both pictures.
Needs Chrome, Chromium or Microsoft Edge to turn the generated HTML into PNG (headless, no network).
"""
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent

# Who acts at a step: the colour, and the lane in the swimlane picture.
AI, PERSON, CI, NEUTRAL = "ai", "person", "ci", "neutral"
STYLE = {  # fill, stroke, title colour, text colour
    AI: ("#EEEDFE", "#534AB7", "#26215C", "#3C3489"),
    PERSON: ("#E1F5EE", "#0F6E56", "#04342C", "#085041"),
    CI: ("#FAECE7", "#993C1D", "#4A1B0C", "#712B13"),
    NEUTRAL: ("#F1EFE8", "#5F5E5A", "#2C2C2A", "#444441"),
}

# The single source of truth for both pictures: (who, lane, title, two-line detail, one-line summary).
STEPS = [
    (NEUTRAL, PERSON, "Input", ("A requirement, code change,", "new endpoint or a finding"),
     "Requirement, code change, endpoint or finding"),
    (AI, AI, "Analyse · qe-analyst", ("Acceptance criteria, gaps, proposed", "risks and abuse cases (advice only)"),
     "Criteria, gaps, proposed risks, abuse cases"),
    (PERSON, PERSON, "Decide · test owner", ("Rates the risk (impact × likelihood)", "in docs/testing/RISK_REGISTER.md"),
     "Rates the risk in RISK_REGISTER.md"),
    (AI, AI, "Design tests · qe-test-designer", ("Lowest level that proves the risk:", "unit, API or end-to-end"),
     "Lowest level that proves the risk"),
    (PERSON, PERSON, "Implement · developer", ("Code + tests tagged with the risk ID;", "scripts/verify.sh, branch smoke on push"),
     "Code + risk-tagged tests; verify.sh, smoke"),
    (AI, AI, "Review · qe-reviewer", ("Weakened or missing tests, auth changes;", "PR template with “red evidence”"),
     "Weakened tests, auth changes, red evidence"),
    (CI, CI, "Pull request gates · CI", ("G1 build + lint · G2 unit · G3 API · G4–G6", "G7 E2E · G9 mutation of new lines · G10 CodeQL"),
     "G1–G6 · G7 E2E · G9 mutation · G10 CodeQL"),
    (PERSON, PERSON, "Merge · code owner", ("Reviews the change and merges it", "when every gate is green (G8)"),
     "Merges when every gate is green (G8)"),
    (PERSON, PERSON, "Covered · test owner", ("Marks the risk Covered; from then on", "G4 fails if its last test disappears"),
     "From now on G4 keeps the risk's test"),
    (CI, CI, "Monitor · CI", ("Nightly mutation (≥ 90 %), flaky-test and", "E2E runs; weekly Dependabot updates"),
     "Nightly mutation, flaky, E2E; weekly Dependabot"),
]
LOOP_NOTE = "A failure in 7 or 10 goes back to 2"
TITLE = "Hospital Management: how a change is tested"

CSS = """html,body{margin:0;background:#fff}body{font-family:"Segoe UI","Helvetica Neue",Arial,sans-serif}
.h{font-size:26px;font-weight:600;fill:#2C2C2A}.sub{font-size:15px;fill:#5F5E5A}
.lane{font-size:19px;font-weight:600}.ls{font-size:14px}.lg{font-size:15px;fill:#444441}
.t{font-size:16px;font-weight:600}.tb{font-size:19px;font-weight:600}.s{font-size:14px}.sb{font-size:15px}
.n{font-size:14px;font-weight:700;fill:#fff}.nb{font-size:16px;font-weight:700;fill:#fff}
.loop{font-size:14px;fill:#A32D2D;font-style:italic}"""
ARROW = ('<defs><marker id="a" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="7" markerHeight="7" '
         'orient="auto-start-reverse"><path d="M1 1L9 5L1 9z" fill="context-stroke"/></marker></defs>')


def page(width, height, svg):
    return (f'<!doctype html><html lang="en"><head><meta charset="utf-8"><title>Testing flow</title>'
            f'<style>{CSS}body{{width:{width}px;height:{height}px}}</style></head><body>'
            f'<svg width="{width}" height="{height}" viewBox="0 0 {width} {height}" xmlns="http://www.w3.org/2000/svg">'
            f'{ARROW}<rect width="{width}" height="{height}" fill="#fff"/>{svg}</svg></body></html>')


def legend(y, x0):
    items = [(AI, "AI agent (advises only)", 230), (PERSON, "Person decides", 170), (CI, "Automated, no AI", 180)]
    out, x = [], x0
    for who, text, step in items:
        fill, stroke, _, _ = STYLE[who]
        out.append(f'<rect x="{x}" y="{y - 13}" width="16" height="16" rx="3" fill="{fill}" stroke="{stroke}"/>'
                   f'<text x="{x + 24}" y="{y}" class="lg">{text}</text>')
        x += step
    out.append(f'<text x="{x}" y="{y}" class="lg">↻ {LOOP_NOTE}</text>')
    return "".join(out)


def overview():
    """Mind map: the steps around one centre, 1–5 on the left and 6–10 on the right."""
    w, h, bw, bh = 1400, 900, 420, 110
    cx0, cx1, cy = 590, 810, 465
    out = [f'<text x="{w // 2}" y="44" text-anchor="middle" class="h">{TITLE}</text>',
           f'<text x="{w // 2}" y="72" text-anchor="middle" class="sub">Read 1 → 5 on the left, then 6 → 10 on the right</text>']
    boxes = []
    for i, (who, _, title, detail, _) in enumerate(STEPS):
        left = i < 5
        x = 50 if left else 930
        y = 100 + (i % 5) * 155
        my = y + bh // 2
        if left:
            out.append(f'<path d="M{cx0} {cy} C {cx0 - 70} {cy} {cx0 - 70} {my} {x + bw} {my}" fill="none" '
                       'stroke="#B4B2A9" stroke-width="2"/>')
        else:
            out.append(f'<path d="M{cx1} {cy} C {cx1 + 70} {cy} {cx1 + 70} {my} {x} {my}" fill="none" '
                       'stroke="#B4B2A9" stroke-width="2"/>')
        fill, stroke, tcol, scol = STYLE[who]
        boxes.append(f'<rect x="{x}" y="{y}" width="{bw}" height="{bh}" rx="12" fill="{fill}" stroke="{stroke}" stroke-width="1.5"/>'
                     f'<circle cx="{x + 36}" cy="{y + 35}" r="17" fill="{stroke}"/>'
                     f'<text x="{x + 36}" y="{y + 41}" text-anchor="middle" class="nb">{i + 1}</text>'
                     f'<text x="{x + 65}" y="{y + 42}" class="tb" fill="{tcol}">{title}</text>'
                     f'<text x="{x + 22}" y="{y + 74}" class="sb" fill="{scol}">{detail[0]}</text>'
                     f'<text x="{x + 22}" y="{y + 95}" class="sb" fill="{scol}">{detail[1]}</text>')
    fill, stroke, tcol, scol = STYLE[NEUTRAL]
    out += boxes
    out.append(f'<rect x="{cx0}" y="{cy - 45}" width="{cx1 - cx0}" height="90" rx="14" fill="{fill}" stroke="{stroke}" stroke-width="1.5"/>'
               f'<text x="{w // 2}" y="{cy - 6}" text-anchor="middle" class="tb" fill="{tcol}">Testing flow</text>'
               f'<text x="{w // 2}" y="{cy + 20}" text-anchor="middle" class="sb" fill="{scol}">risk-based, human in the loop</text>')
    out.append(legend(871, 300))
    return w, h, "".join(out)


def lanes():
    """Swimlanes: AI agents, people and CI as columns; the steps from top to bottom with arrows."""
    w, h, lane_w, bw, bh, row, top0 = 1240, 1060, 360, 330, 62, 84, 175
    lane_x = {AI: 40, PERSON: 420, CI: 800}
    lane_text = {AI: ("AI agents", "advise only"), PERSON: ("People", "decide and merge"), CI: ("CI", "automated, no AI")}
    loop_x = 1195

    def bx(lane):
        return lane_x[lane] + (lane_w - bw) // 2

    def top(i):
        return top0 + i * row

    out = [f'<text x="{w // 2}" y="46" text-anchor="middle" class="h">{TITLE}</text>',
           f'<text x="{w // 2}" y="76" text-anchor="middle" class="sub">Follow the numbers from top to bottom; '
           'the lane shows who acts at each step</text>']
    for lane, x in lane_x.items():
        fill, stroke, tcol, scol = STYLE[lane]
        name, sub = lane_text[lane]
        out.append(f'<rect x="{x}" y="100" width="{lane_w}" height="{top(len(STEPS)) - 100}" rx="14" fill="{fill}" '
                   f'fill-opacity="0.45" stroke="{stroke}" stroke-opacity="0.35"/>'
                   f'<text x="{x + lane_w // 2}" y="132" text-anchor="middle" class="lane" fill="{tcol}">{name}</text>'
                   f'<text x="{x + lane_w // 2}" y="154" text-anchor="middle" class="ls" fill="{scol}">{sub}</text>')
    for i in range(len(STEPS) - 1):
        a, b = STEPS[i][1], STEPS[i + 1][1]
        ax, bxx = bx(a) + bw // 2, bx(b) + bw // 2
        y0, y1 = top(i) + bh, top(i + 1) - 2
        ym = top(i) + bh + (row - bh) // 2
        d = f"M{ax} {y0} L{bxx} {y1}" if a == b else f"M{ax} {y0} L{ax} {ym} L{bxx} {ym} L{bxx} {y1}"
        out.append(f'<path d="{d}" fill="none" stroke="#888780" stroke-width="2" marker-end="url(#a)"/>')
    y2 = top(1) + bh // 2
    for i in (6, 9):
        yi = top(i) + bh // 2
        out.append(f'<path d="M{bx(CI) + bw} {yi} L{loop_x} {yi}" fill="none" stroke="#A32D2D" stroke-width="2" '
                   'stroke-dasharray="6 5"/>')
    out.append(f'<path d="M{loop_x} {top(9) + bh // 2} L{loop_x} {y2} L{bx(AI) + bw + 2} {y2}" fill="none" '
               'stroke="#A32D2D" stroke-width="2" stroke-dasharray="6 5" marker-end="url(#a)"/>'
               f'<text x="{bx(CI) + bw // 2}" y="{y2 - 10}" text-anchor="middle" class="loop">{LOOP_NOTE}</text>')
    for i, (who, lane, title, _, summary) in enumerate(STEPS):
        fill, stroke, tcol, scol = STYLE[who]
        x, y = bx(lane), top(i)
        out.append(f'<rect x="{x}" y="{y}" width="{bw}" height="{bh}" rx="10" fill="{fill}" stroke="{stroke}" stroke-width="1.6"/>'
                   f'<circle cx="{x + 26}" cy="{y + 22}" r="15" fill="{stroke}"/>'
                   f'<text x="{x + 26}" y="{y + 27}" text-anchor="middle" class="n">{i + 1}</text>'
                   f'<text x="{x + 50}" y="{y + 28}" class="t" fill="{tcol}">{title}</text>'
                   f'<text x="{x + 16}" y="{y + 51}" class="s" fill="{scol}">{summary}</text>')
    return w, h, "".join(out)


def find_browser():
    names = ["chrome", "google-chrome", "chromium", "chromium-browser", "msedge", "microsoft-edge"]
    for name in names:
        if shutil.which(name):
            return shutil.which(name)
    for path in [r"C:\Program Files\Google\Chrome\Application\chrome.exe",
                 r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                 "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"]:
        if os.path.exists(path):
            return path
    sys.exit("No Chrome, Chromium or Edge found to render the PNG files.")


def render(browser, name, width, height, svg, workdir):
    html = Path(workdir, name + ".html")
    html.write_text(page(width, height, svg), encoding="utf-8")
    png = HERE / (name + ".png")
    subprocess.run([browser, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=2",
                    f"--window-size={width},{height}", f"--screenshot={png}", html.resolve().as_uri()],
                   check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    print(f"wrote {png.relative_to(HERE.parent.parent)}")


def main():
    browser = find_browser()
    with tempfile.TemporaryDirectory() as workdir:
        render(browser, "testing-flow-overview", *overview(), workdir)
        render(browser, "testing-flow-lanes", *lanes(), workdir)


if __name__ == "__main__":
    main()
