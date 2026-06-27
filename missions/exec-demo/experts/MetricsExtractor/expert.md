---

name: MetricsExtractor
kind: json_extract
input: Raw JSON metrics from CodeAnalyser
output: Structured metrics unpacked into context bag
inputKeys:
  metrics: string
outputKeys:
  total_lines: int
  function_count: int
  avg_complexity: float
---

Unpacks the JSON metrics object into named context variables so the
CodeReviewer can reference each measurement directly by name.
