# Phase 22 — Non-LLM Expert Kinds

**Status:** Partial; resume only if an embedded-model use case requires it.

## Goal

Pipeline stages backed by something other than an LLM (ONNX scoring models, HTTP scoring
endpoints, rule-based processors), chosen by the `kind` field in expert frontmatter. The runner
dispatches on `kind` without reflection.

## Done (in forge-mcl)

`kind` dispatch in `ExpertLoader`; `OnnxExpertRunner` and `HttpExpertRunner`
(`src/ForgeMission.Core/Adapters/`) with tests and ONNX fixtures; `double` values in the context
bag. Language reference: [language.md](../design/language.md) (`kind` field, `kind: onnx`).

## Open

The UC-3 demo mission, which has not been built. Production log anomaly detection: a parser
normalises logs into context keys, an ONNX model scores them, and LLM experts explain the root
cause and write the incident report:

```fsharp
mission LogAnomalyAnalysis(logStream) = {
    LogParser
    -> AnomalyDetector
    -> RootCauseAnalyst
    -> IncidentReporter
}
```

Done when that mission runs with `forge run` on the default cloud path, with a small ONNX model
whose score drives the judge.
