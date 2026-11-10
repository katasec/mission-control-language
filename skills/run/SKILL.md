---
name: run
description: Launch the real app and use it the way a user would, to show a change works outside the tests. Use in the Live stage of the supervisor workflow.
---

# Run

Prove the change works in the installed product, not in tests.

1. Use the normal install and configuration: no overridden URL, stub or injected setting.
2. Do the user action the change affects, end to end.
3. Record: artifact · action · observed result · PASS/FAIL.
