---
name: Extract
kind: exec
command: python3
args: [./extract.py]
inputs: [source_file]
outputKey: source_text
input: uploaded image or PDF staged by the hosted artifact bridge
output: full OCR text extracted from the source artifact
outputKeys:
  source_text: string
---

Extracts full text from an uploaded image or PDF using Tesseract. PDF inputs are rasterized with
pdftoppm before OCR. This expert returns text in context for downstream synthesis; it does not
write output artifacts.
