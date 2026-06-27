---
name: ExtractControls
kind: json_extract
input: Raw JSON string with compliance control scores from ControlChecker
output: Individual control scores unpacked into context bag
inputKeys:
  controls: string
outputKeys:
  encryption_score: float
  access_score: float
  logging_score: float
  patch_score: float
  controls_found: float
  config_path: string
---
