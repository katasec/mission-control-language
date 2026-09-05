# Task 4 browser observation — 2026-09-05

The Client Runtime was rebuilt from this working tree and served locally for in-app browser inspection. The final observation used `http://127.0.0.1:51790/` at the browser’s available 1280×720 light viewport.

| Observation | Result |
|---|---|
| Launcher | PASS — the existing create/open launcher was present before a local isolated Project was created. |
| Missions | PASS — the Project title, rail, empty-history copy, Composer, and bottom-aligned Settings navigation were visible without horizontal overflow. |
| Mission picker | PASS — the accessible `Mission` list contained only Janus and Naive. Selecting Naive changed the committed picker label. |
| Draft and Run | PASS — an empty draft left Run disabled; the input stayed editable; selecting Naive and entering an instruction enabled Run. |
| Explorer | PASS — Assets, Context, and the same summary-only Runs section rendered; no expert transcript was exposed. |
| Settings | PASS — the Settings destination is reachable from the rail and makes no non-existent preference actionable. |
| Browser diagnostics | PASS — the final rebuilt host presented no application-error banner or unhandled-error UI during the observations. |

Focused automated verification: `HomeSessionOperationTests`, `WorkbenchPresentationTests`, and `ConversationTranscriptViewTests` passed (14 tests). The focused Client Runtime build passed with zero warnings/errors.

## Token contrast measurements

Measured from the Workbench token values using WCAG relative luminance. All text pairs clear 4.5:1; rail marker/non-text contrast clears 3:1.

| Pair | Light | Dark |
|---|---:|---:|
| text / surface | 16.86:1 | 14.67:1 |
| muted text / surface | 5.42:1 | 7.98:1 |
| subtle text / surface | 4.78:1 | 6.67:1 |
| accent contrast / accent | 4.72:1 | 6.69:1 |
| danger / danger background | 6.05:1 | 8.11:1 |
| success / surface | 4.99:1 | 11.46:1 |
| rail text / rail | 14.72:1 | 14.72:1 |
| rail muted text / selected rail | 5.91:1 | 5.91:1 |
| rail marker / rail | 8.29:1 | 8.29:1 |

## Remaining acceptance evidence

The available browser automation did not expose an adjustable viewport, theme selector, zoom control, or a persisted screenshot-file export. Its live screenshots were inspected in the task session, but no image files could be saved beneath this evidence directory. The four reference rectangles, dark theme, 125/150/200% zoom, packaged-native parity, and durable Janus/Naive lifecycle/focus cases therefore remain **unverified** and must not be recorded as passed by task 5.