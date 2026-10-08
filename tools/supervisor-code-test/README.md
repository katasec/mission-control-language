# Code-writing comparison

Run either independent script in PowerShell, from any directory:

```powershell
./run-mcl.ps1
./run-codex.ps1
```

Both read `prompt.md`, shared settings and personas. MCL calls the provider directly through installed Forge and Hands; native Codex uses its own workspace tools and reviewer delegation. Requires Python 3, PowerShell 7, installed Forge with exported `MCL_API_KEY` for MCL, or authenticated Codex CLI for Codex. No credentials belong in this folder.

Each run prepares an empty `code/` directory and creates `retry_delay.py` and `test_retry_delay.py`. The MCL build expert returns the existing step envelope containing final decision JSON. The harness runs each generated suite plus the same ten acceptance tests. New results go into separate `results/mcl/<run-id>` and `results/codex/<run-id>` folders, ignored by Git.

The [successful sample](evidence/README.md) preserves the exact inputs, generated code, logs and outcomes. MCL used 135.566s; native Codex used 211.854s. Both passed the common tests. One sample does not establish general speed or semantic equivalence. Native explicitly selects medium reasoning; MCL uses the provider default. Their tool capabilities and context management differ.

The original design-only comparison remains in [supervisor-test](../supervisor-test/README.md). This is its code-writing variation, preserving the existing harness rather than adding another execution framework.
