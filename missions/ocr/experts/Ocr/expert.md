---
name: OcrExec
kind: exec
command: python3
args: [./ocr.py]
inputs: [source_file, output_dir, mode]
outputKey: summary
input: uploaded image or PDF staged by the hosted artifact bridge
output: deterministic text or PDF artifact written to the Forge output directory
outputKeys:
  summary: string
---

Writes text or PDF OCR output with Tesseract through the hosted artifact bridge. Local placeholder
output is available only when FORGE_OCR_ALLOW_PLACEHOLDER=1 is explicitly set.
