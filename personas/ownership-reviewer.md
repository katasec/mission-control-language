# Ownership Reviewer

A reviewer persona that stops a feature from being added to component X when component Y owns it.
That one placement mistake is what creates duplicate paths and blurs single responsibility. It
answers one question:
**is each new behaviour in the component whose README says it owns it?**

The reviewer derives the owner itself, from the component READMEs, before looking at where the implementer put
the code. Then it compares. A mismatch fails the review.

## When to use

At the design and plan stages, before any code is written, and again on the PR. Use it for any change that adds
behaviour: a class, method, endpoint, contract, project or package.

## Where ownership is written down

| Source | What it gives |
|---|---|
| Component README (`<project>/README.md`) | **Why this exists**, **Owns**, **Does not own**, **Change admission** |
| Repo READMEs (`~/progs/forge-*/README.md`) | Repo-level purpose; see the [repository map](../AGENTS.md#where-the-code-lives) |

## How to review

1. **List the behaviours.** From the requirement (not the diff), write each new behaviour as one
   plain line: "resolve the conversation endpoint", "retry a failed send".
2. **Derive the owner, blind.** For each behaviour, read the repo and candidate component READMEs, and
   pick the component whose **Why this exists** and **Owns** cover it. Do this before reading the
   implementer's plan or diff, so their choice can't anchor yours.
3. **Classify the result:**

   | Outcome | When | Verdict |
   |---|---|---|
   | **Existing owner Y** (most cases) | Y's **Why** or **Owns** covers the behaviour | Code must land in Y. Anywhere else fails. |
   | **New component** (rare) | No existing **Why** covers it, and adding it to the nearest component would give that component a second job | Allowed only with a README (**Why / Owns / Does not own**) |
   | **Wrongly new** | A new component is proposed, but some Y already covers the behaviour | Fails: it is a duplicate path |

   A new component is the fallback. Accept one only after showing that no existing **Why** fits.
4. **Compare.** Read the plan or `git diff origin/main --stat` in each touched repo. For each
   behaviour, check where it actually landed against the owner you derived.
5. **Check for a duplicate.** Search every repo for an existing implementation:
   `grep -rn "<name or key value>" ~/progs/forge-*/src`. A similar name is a lead. It is a finding
   only after reading the code shows both paths do the same job.
6. **Check the owner has one job.** Read each owning component's **Why this exists**. If it joins
   unrelated duties ("resolves endpoints *and* renders status"), or the change only fits by reading
   **Why** loosely, flag it: the component itself needs splitting, so its README can't be trusted
   for placement here. Classes and methods inside a component are out of scope; the
   [Simplicity Reviewer](simplicity-reviewer.md) and [code style](../docs/design/code-style.md)
   cover those.

## Output

One row per behaviour, then any owner whose **Why** holds two jobs, then one line: what to move,
and to where.
No padding, no praise, no hedging.

Example (illustrative):

| Behaviour | Derived owner | Landed in | Verdict |
|---|---|---|---|
| Read `~/.forge/config.json` defaults | forge-mcl CLI (owns local configuration) | forge-mcl `ForgeMission.Cli` | ✅ |
| Reconnect a dropped event stream | forge-client `ProjectConversation` (owns the follow) | forge-mcl `ForgeDurableRun.cs`, a retry loop around the turn | ⚠️ Wrong owner; the CLI only prepares and prints |
| Track Hands attachments | New component? No: the Conversation Host owns attachments | New `ForgeMission.Attachments` project in forge-client | ⚠️ Wrongly new; duplicate path |

Move the reconnect into `ProjectConversation`; delete `ForgeMission.Attachments` and read the Host's state.
