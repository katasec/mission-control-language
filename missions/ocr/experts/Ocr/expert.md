---
name: Ocr
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

Writes a deterministic placeholder OCR artifact for the hosted artifact bridge demo. The real
PaddleOCR implementation will replace the script while keeping the same file staging contract.
