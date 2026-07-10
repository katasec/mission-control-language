#!/usr/bin/env python3
"""Editor: deterministic PDF surgery. Delete the pages the Planner selected and prepend a
generated cover slide. Kept pages are copied losslessly (pikepdf) so the Verifier can prove
nothing on them changed. Reads a JSON object on stdin, writes a JSON object on stdout.

stdin : {"output": "<Planner plan JSON>", "source_pdf": "/work/input.pdf", "work_dir": "/work"}
stdout: {"result": {output_pdf, source_pdf, removed, expected}, "status": "pass"|"fail", "reason": ...}
"""
import sys, io, json, re
from datetime import datetime

TITLE    = "Understanding Quran"      # fixed branding
SUBTITLE = "Weekly Family Halaqa"     # fixed branding
PRESENTER = "Jain Bint Ameer Batcha"  # fixed presenter (v1: single known presenter)


def fail(reason):
    print(json.dumps({"result": "", "status": "fail", "reason": reason}))
    sys.exit(0)  # a clean fail envelope, not a process error (loop feedback handles it)


def format_dates(iso):
    """The Planner emits ONE canonical ISO date ("YYYY-MM-DD"); we derive BOTH downstream forms here,
    deterministically — the cover display ("4th Jul 2026") and the dotted file stem ("04.07.2026").
    Keeping formatting in code (not the LLM) removes a class of date-format error. On an unparseable
    value, degrade to the raw string for the cover and an empty file stem (→ output.pdf fallback)."""
    try:
        d = datetime.strptime((iso or "").strip(), "%Y-%m-%d")
    except ValueError:
        return ((iso or "").strip() or "(no date)"), ""
    suffix = "th" if 11 <= d.day % 100 <= 13 else {1: "st", 2: "nd", 3: "rd"}.get(d.day % 10, "th")
    display = f"{d.day}{suffix} {d.strftime('%b %Y')}"   # 4th Jul 2026
    dotted  = d.strftime("%d.%m.%Y")                     # 04.07.2026 (zero-padded, DD.MM.YYYY)
    return display, dotted


def extract_json(text):
    """The Planner is an LLM; tolerate stray prose or ```json fences around the object."""
    text = (text or "").strip()
    text = re.sub(r"^```(?:json)?|```$", "", text, flags=re.MULTILINE).strip()
    start, depth = text.find("{"), 0
    if start < 0:
        return None
    for i in range(start, len(text)):
        depth += (text[i] == "{") - (text[i] == "}")
        if depth == 0:
            try:
                return json.loads(text[start:i + 1])
            except json.JSONDecodeError:
                return None
    return None


def make_cover(w, h, date):
    from reportlab.pdfgen import canvas
    from reportlab.lib.colors import HexColor
    buf = io.BytesIO()
    c = canvas.Canvas(buf, pagesize=(w, h))
    c.setFillColor(HexColor("#000000"))
    c.rect(0, 0, w, h, fill=1, stroke=0)
    c.setFillColor(HexColor("#FFFFFF"))
    y = h * 0.62
    for text, size, bold in [(TITLE, 40, True), (SUBTITLE, 30, True), (date, 26, False), (PRESENTER, 24, False)]:
        c.setFont("Helvetica-Bold" if bold else "Helvetica", size)
        c.drawCentredString(w / 2, y, text)
        y -= size * 1.9
    c.showPage()
    c.save()
    buf.seek(0)
    return buf


def main():
    import pikepdf
    data = json.load(sys.stdin)
    plan = extract_json(data.get("output", ""))
    if plan is None:
        fail("Planner did not produce a valid JSON plan.")

    source_pdf = data.get("source_pdf")
    work_dir   = data.get("work_dir") or "."
    if not source_pdf:
        fail("No source_pdf provided to the editor.")

    remove = sorted({int(p) for p in plan.get("remove_pages", [])})
    # One canonical ISO date in; both display + dotted-file forms derived deterministically here.
    date_display, date_file = format_dates(plan.get("date"))

    src = pikepdf.open(source_pdf)
    n   = len(src.pages)
    bad = [p for p in remove if p < 1 or p > n]
    if bad:
        fail(f"Plan asks to remove pages outside the 1..{n} range: {bad}.")

    keep = [i for i in range(1, n + 1) if i not in remove]
    box  = src.pages[0].mediabox
    w, h = float(box[2]) - float(box[0]), float(box[3]) - float(box[1])

    out   = pikepdf.new()
    cover = pikepdf.open(make_cover(w, h, date_display))
    out.pages.append(cover.pages[0])
    for i in keep:
        out.pages.append(src.pages[i - 1])

    # Fixed-convention output path the runner collects (38.9 §5 #4). The pretty download name is a
    # separate field the orchestrator uses — the bytes on disk stay at /work/output.pdf.
    output_pdf  = f"{work_dir.rstrip('/')}/output.pdf"
    out.save(output_pdf)
    output_name = f"Family Halaqa {date_file}.pdf" if date_file else "output.pdf"

    result = {
        "output_pdf": output_pdf,
        "output_name": output_name,
        "source_pdf": source_pdf,
        "removed": remove,
        "expected": n - len(remove) + 1,
    }
    print(json.dumps({"result": result, "status": "pass", "reason": None}))


if __name__ == "__main__":
    try:
        main()
    except Exception as e:  # never leak a stack trace as the step output
        fail(f"Editor error: {type(e).__name__}: {e}")
