import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
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

    try:
        summary = run_ocr(source, output_dir, stem, digest, mode)
    except OcrToolError as ex:
        write_json("", "fail", str(ex))
        return 0

    write_json(summary, "pass", "")
    return 0


def safe_stem(path: Path) -> str:
    stem = "".join(ch if ch.isalnum() or ch in {"-", "_"} else "_" for ch in path.stem)
    return stem or "ocr-output"


def run_ocr(source: Path, output_dir: Path, stem: str, digest: str, mode: str) -> str:
    if shutil.which("tesseract") is None:
        return write_placeholder(source, output_dir, stem, digest, mode)

    if mode == "text":
        text = extract_text(source)
        (output_dir / f"{stem}.txt").write_text(text, encoding="utf-8")
        return summarize(source, digest, mode, "tesseract", len(text.strip()))

    if is_pdf(source):
        text = extract_text(source)
        write_pdf(output_dir / f"{stem}.pdf", text or f"OCR text for {source.name}")
        return summarize(source, digest, mode, "tesseract", len(text.strip()))

    output_base = output_dir / stem
    run_tool(["tesseract", str(source), str(output_base), "pdf"])
    return summarize(source, digest, mode, "tesseract", 0)


def extract_text(source: Path) -> str:
    if not is_pdf(source):
        return run_tool(["tesseract", str(source), "stdout"])

    if shutil.which("pdftoppm") is None:
        raise OcrToolError("PDF text mode requires pdftoppm in the runner image.")

    pages = []
    with tempfile.TemporaryDirectory(prefix="forge-ocr-") as tmp:
        prefix = Path(tmp) / "page"
        run_tool(["pdftoppm", "-r", "200", "-png", str(source), str(prefix)])
        for page in sorted(Path(tmp).glob("page-*.png")):
            pages.append(run_tool(["tesseract", str(page), "stdout"]))

    return "\n\n".join(pages)


def write_placeholder(source: Path, output_dir: Path, stem: str, digest: str, mode: str) -> str:
    summary = summarize(source, digest, mode, "placeholder", 0)
    if mode == "pdf":
        write_pdf(output_dir / f"{stem}.pdf", summary)
    else:
        (output_dir / f"{stem}.txt").write_text(summary + "\n", encoding="utf-8")
    return summary


def summarize(source: Path, digest: str, mode: str, engine: str, chars: int) -> str:
    parts = [
        f"OCR for {source.name}",
        f"sha256={digest}",
        f"mode={mode}",
        f"engine={engine}",
    ]
    if chars > 0:
        parts.append(f"chars={chars}")
    return "; ".join(parts)


def is_pdf(source: Path) -> bool:
    return source.suffix.lower() == ".pdf"


def run_tool(args: list[str]) -> str:
    try:
        result = subprocess.run(
            args,
            check=True,
            capture_output=True,
            text=True,
            timeout=120,
        )
    except FileNotFoundError as ex:
        raise OcrToolError(f"OCR tool not found: {args[0]}") from ex
    except subprocess.CalledProcessError as ex:
        detail = (ex.stderr or ex.stdout or "").strip()
        raise OcrToolError(f"OCR tool failed: {detail}") from ex
    except subprocess.TimeoutExpired as ex:
        raise OcrToolError("OCR tool timed out after 120 seconds.") from ex

    return result.stdout


def write_pdf(path: Path, text: str) -> None:
    lines = [line.strip() for line in text.splitlines() if line.strip()]
    if not lines:
        lines = ["No OCR text was detected."]

    commands = ["BT /F1 12 Tf 72 720 Td"]
    for index, line in enumerate(lines[:40]):
        escaped = line[:110].replace("\\", "\\\\").replace("(", "\\(").replace(")", "\\)")
        if index > 0:
            commands.append("0 -16 Td")
        commands.append(f"({escaped}) Tj")
    commands.append("ET")
    content = "\n".join(commands)
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


class OcrToolError(Exception):
    pass


if __name__ == "__main__":
    raise SystemExit(main())
