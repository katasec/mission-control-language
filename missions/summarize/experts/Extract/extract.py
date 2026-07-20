import json
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path


def main() -> int:
    source = Path(os.environ.get("FORGE_SOURCE_FILE", ""))

    if not source.is_file():
        write_json("", "fail", "FORGE_SOURCE_FILE did not point to a staged input file.")
        return 0

    try:
        text = extract_text(source).strip()
    except ExtractToolError as ex:
        write_json("", "fail", str(ex))
        return 0

    if not text:
        write_json("", "fail", "No OCR text was detected in the source artifact.")
        return 0

    write_json(text, "pass", "")
    return 0


def extract_text(source: Path) -> str:
    if shutil.which("tesseract") is None:
        raise ExtractToolError("tesseract is not installed or not on PATH.")

    if not is_pdf(source):
        return run_tool(["tesseract", str(source), "stdout"])

    if shutil.which("pdftoppm") is None:
        raise ExtractToolError("PDF extraction requires pdftoppm in the runner image.")

    pages = []
    with tempfile.TemporaryDirectory(prefix="forge-summarize-") as tmp:
        prefix = Path(tmp) / "page"
        run_tool(["pdftoppm", "-r", "200", "-png", str(source), str(prefix)])
        for page in sorted(Path(tmp).glob("page-*.png")):
            page_text = run_tool(["tesseract", str(page), "stdout"]).strip()
            if page_text:
                pages.append(page_text)

    return "\n\n".join(pages)


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
        raise ExtractToolError(f"Extraction tool not found: {args[0]}") from ex
    except subprocess.CalledProcessError as ex:
        detail = (ex.stderr or ex.stdout or "").strip()
        raise ExtractToolError(f"Extraction tool failed: {detail}") from ex
    except subprocess.TimeoutExpired as ex:
        raise ExtractToolError("Extraction tool timed out after 120 seconds.") from ex

    return result.stdout


def write_json(source_text: str, status: str, reason: str) -> None:
    payload = {"source_text": source_text, "status": status}
    if reason:
        payload["reason"] = reason
    sys.stdout.write(json.dumps(payload))


class ExtractToolError(Exception):
    pass


if __name__ == "__main__":
    raise SystemExit(main())
