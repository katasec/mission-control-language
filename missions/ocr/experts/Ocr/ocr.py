import hashlib
import json
import os
import sys
from pathlib import Path


def main() -> int:
    source = Path(os.environ.get("FORGE_SOURCE_FILE", ""))
    output_dir = Path(os.environ.get("FORGE_OUTPUT_DIR", ""))
    mode = os.environ.get("FORGE_MODE", "text").strip().lower() or "text"

    if mode not in {"text", "pdf"}:
        write_json("", "fail", f"Unsupported OCR mode '{mode}'. Expected 'text' or 'pdf'.")
        return 0

    if not source.is_file():
        write_json("", "fail", "FORGE_SOURCE_FILE did not point to a staged input file.")
        return 0

    output_dir.mkdir(parents=True, exist_ok=True)
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    stem = safe_stem(source)
    summary = f"OCR placeholder for {source.name}; sha256={digest}; mode={mode}"

    if mode == "pdf":
        write_pdf(output_dir / f"{stem}.pdf", summary)
    else:
        (output_dir / f"{stem}.txt").write_text(summary + "\n", encoding="utf-8")

    write_json(summary, "pass", "")
    return 0


def safe_stem(path: Path) -> str:
    stem = "".join(ch if ch.isalnum() or ch in {"-", "_"} else "_" for ch in path.stem)
    return stem or "ocr-output"


def write_pdf(path: Path, text: str) -> None:
    escaped = text.replace("\\", "\\\\").replace("(", "\\(").replace(")", "\\)")
    content = f"BT /F1 12 Tf 72 720 Td ({escaped}) Tj ET"
    objects = [
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        f"<< /Length {len(content.encode('utf-8'))} >>\nstream\n{content}\nendstream",
    ]

    pdf = bytearray(b"%PDF-1.4\n")
    offsets = [0]
    for index, obj in enumerate(objects, start=1):
        offsets.append(len(pdf))
        pdf.extend(f"{index} 0 obj\n{obj}\nendobj\n".encode("utf-8"))

    xref = len(pdf)
    pdf.extend(f"xref\n0 {len(objects) + 1}\n0000000000 65535 f \n".encode("utf-8"))
    for offset in offsets[1:]:
        pdf.extend(f"{offset:010d} 00000 n \n".encode("utf-8"))
    pdf.extend(
        f"trailer << /Size {len(objects) + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n".encode("utf-8")
    )
    path.write_bytes(pdf)


def write_json(summary: str, status: str, reason: str) -> None:
    payload = {"summary": summary, "status": status}
    if reason:
        payload["reason"] = reason
    sys.stdout.write(json.dumps(payload))


if __name__ == "__main__":
    raise SystemExit(main())
