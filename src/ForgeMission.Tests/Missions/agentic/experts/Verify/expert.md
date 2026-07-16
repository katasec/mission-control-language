---
name: Verify
kind: exec
command: python3
args: [./verify.py]
input: the agent's final answer
inputs: [output]
outputKey: output
output: the answer stamped with a VERIFIED marker
---

Post-agent verification stand-in for the 42.3 integration tests: stamps the agent's final
answer with a VERIFIED marker, proving the post-agent segment ran on the final continuation.
