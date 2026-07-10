#!/usr/bin/env python3
"""Editor: deterministic PDF surgery. Delete the pages the Planner selected and prepend a
generated cover slide. Kept pages are copied losslessly (pikepdf) so the Verifier can prove
nothing on them changed. Reads a JSON object on stdin, writes a JSON object on stdout.

stdin : {"output": "<Planner plan JSON>", "source_pdf": "/work/input.pdf", "work_dir": "/work"}
stdout: {"result": {output_pdf, source_pdf, removed, expected}, "status": "pass"|"fail", "reason": ...}
"""
import sys, io, json, re

TITLE    = "Understanding Quran"      # fixed branding
SUBTITLE = "Weekly Family Halaqa"     # fixed branding
PRESENTER = "Jain Bint Ameer Batcha"  # fixed presenter (v1: single known presenter)


def fail(reason):
    print(json.dumps({"result": "", "status": "fail", "reason": reason}))
    sys.exit(0)  # a clean fail envelope, not a process error (loop feedback handles it)


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
    date   = str(plan.get("date", "")).strip() or "(no date)"

    src = pikepdf.open(source_pdf)
    n   = len(src.pages)
    bad = [p for p in remove if p < 1 or p > n]
    if bad:
        fail(f"Plan asks to remove pages outside the 1..{n} range: {bad}.")

    keep = [i for i in range(1, n + 1) if i not in remove]
    box  = src.pages[0].mediabox
    w, h = float(box[2]) - float(box[0]), float(box[3]) - float(box[1])

    out   = pikepdf.new()
    cover = pikepdf.open(make_cover(w, h, date))
    out.pages.append(cover.pages[0])
    for i in keep:
        out.pages.append(src.pages[i - 1])

    output_pdf = f"{work_dir.rstrip('/')}/output.pdf"
    out.save(output_pdf)

    result = {
        "output_pdf": output_pdf,
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
