# Phase 48 — Windows Application Control investigation

> **Status:** security cause resolved on 2026-09-18. Controlled evidence only; not default-path acceptance.

## Question

Is Windows Application Control blocking the Desktop process topology, the inherited-pipe protocol,
particular binaries/dependent DLLs, or the deployment layout?

## Finding summary

| Finding | Evidence | Rules out / establishes |
|---|---|---|
| The historical failure was Smart App Control enforcement, not audit mode. | 3077/3033 events name `VerifiedAndReputableDesktop`; current machine has Smart App Control **On** and user-mode Code Integrity **Enforced**; Microsoft distinguishes 3077 enforcement from 3076 audit. | The original failure was a real Windows policy refusal. |
| It was a Defender/SAC trust decision for the specific launch at that time. | Each historical block has companion 3118 fields: `DefenderCalled=true`, `DefenderCloudCallRequested=true`, `TTLValid=false`, and `DefenderTrust=-16777216`. | It was not a malware finding and not a generic unsigned-file rule. |
| It was not caused by Forge topology. | MRE parent/child, inherited pipes, exact Host pipe protocol, Host payload, direct candidate-DLL use, and the current zero-argument Supervisor all ran. | Do not redesign or merge processes to address this policy issue. |
| It was not a static property of the Supervisor bytes, folder, or launch API. | SHA-256 `4BEAB9DC…FFC4DE` was blocked from PowerShell, Explorer, and Program Files on 2026-09-17. On 2026-09-18 the exact same byte-identical file ran from both `dist` and the preserved 2026-09-17 Program Files install, with both shell and direct process creation and no CI event. | A Forge-side payload or invocation bisect cannot reproduce the historical block. |

**Cause:** on 2026-09-17 Smart App Control's Defender trust evaluation rejected the Supervisor
launch. Its trust disposition later changed while SAC remained on and the executable bytes and
original installed file remained unchanged. The logs expose the call and its decision, not the
internal Defender/reputation reason for the later change.

## MRE experiments

| Experiment | Result |
|---|---|
| Minimal MAUI parent → AOT child, launched from Explorer | Passed. |
| Minimal native-AOT MAUI parent → AOT child | Passed. |
| Minimal parent → exact published `ForgeMission.Application.Host.exe` | Passed; emitted `FORGE_CLIENT_RUNTIME_URL`. |
| Minimal parent → exact published legacy `ForgeMission.Desktop.Host.exe`, with its required files and inherited pipes | Passed. |
| Minimal native-AOT parent/child using the exact anonymous-pipe handles and framing | Passed; command/event round-trip and normal child exit. |
| Historical `aot-desktop-host-pipe-child` control | Passed. Its construction excludes `ForgeMission.Desktop.exe`, `ForgeMission.Desktop.dll`, every `ForgeMission.Application.Host*` file, and PDBs. The exact earlier blocked folder was not retained. |
| 2026-09-18 reconstructed full current `dist/forge-desktop` payload | The recorded pipe probe passed with no CI event. A separate, unrecorded launch from the same folder produced 3033/3077 naming `ForgeMission.Desktop.Host.exe` and `Microsoft.Extensions.DependencyInjection.Abstractions.dll`; later direct-load and exact-folder replay did not reproduce it. |

**MRE conclusion:** parent/child creation, AOT, and inherited pipes are not independently proven
policy triggers. The isolated Code Integrity decision for an unsigned dependency does not establish
that it caused the user-visible failure. The historical two-executable shorthand was incomplete.

## Forge experiments

| Experiment | Result |
|---|---|
| Published `ForgeMission.Desktop.exe` | Blocked before `Main`. |
| Debug Supervisor → `dotnet ForgeMission.Desktop.Host.dll` | Blocked loading `ForgeMission.Desktop.Contracts.dll`. |
| Release Supervisor → MAUI Host DLL through `dotnet` | Supervisor, Host, and Application Host stayed alive; loopback UI was healthy in a browser; MAUI window was white and had no WebView2 child. |
| Release Supervisor → generated MAUI Host `.exe` child | Blocked with `Win32Exception` 4551. |
| Fresh Visual Studio Release build | Blocked `ForgeMission.Desktop.dll` before Supervisor startup. |

**Forge conclusion:** policy decisions vary by generated image, dependent DLL, and/or deployment
layout. A successful controlled launch does not establish that the published Desktop route will run.

## Separate MAUI bootstrap experiment

| Experiment | Result |
|---|---|
| MAUI Host DLL through `dotnet`, before managed Windows App SDK bootstrap | Crashed in `Microsoft.UI.Xaml.dll` (`0xc000027b`); WER recorded `0x80040154` (class not registered). |
| Same DLL with `WindowsAppSdkBootstrapInitialize` and `WindowsAppSdkUndockedRegFreeWinRTInitialize` | WinUI crash stopped; window remained white. |

This fixes DLL-hosted WinUI initialization only. It does not prove WebView creation or navigation.

## Policy observations

- Code Integrity reported `VerifiedAndReputableDesktop`; these were reputation/policy blocks, not
  malware findings.
- Windows Security also blocked `MSBuild.exe` loading `Docker.DotNet.X509.dll` from the NuGet
  cache, confirming that policy applies beyond Desktop process creation.

## Current controlled protocol

**Question:** does loading the unsigned
`Microsoft.Extensions.DependencyInjection.Abstractions.dll` produce a Code Integrity enforcement
event that prevents the Desktop Host's required behavior?

| Case | Only variable | Expected observation | Interpretation |
|---|---|---|---|
| A — baseline | AOT MRE parent + AOT pipe child only | Pipe round-trip and child exit `0`; no Forge dependency loaded. | Proves the launcher and pipe measurement. |
| B — unused sibling | A plus the candidate DLL beside the MRE child, never loaded by it. | Event or no event. | Separates payload presence from an actual load. |
| C — full Host | Existing MRE parent starts the exact Desktop Host from a frozen full Forge payload. | Pipe/exit outcome plus CI event XML. | Repeats the observed Host dependency load in one bounded window. |

Every case uses a fresh copied directory and the same launcher. Before launch, record the file
manifest, candidate hash, Authenticode status, and Zone.Identifier state. Record the parent/child
PIDs and start/end timestamps; export every CI event in that window as XML. Do not infer causation
from event presence alone: it must correlate to the measured process and outcome. Cases that remove
the candidate DLL are deferred because they first prove a normal missing-dependency failure, not a
security cause.

### 2026-09-18 results — scientific-20260918-193433

The candidate DLL was SHA-256 `05C941A1F0EF262BB6984C33999E445731A28A1E81EE8604B28F188F9984158A`,
Authenticode `NotSigned`, with no Zone.Identifier stream in every case. Each run preserved its
manifest, result, and CI XML under
`C:\Users\ameer\tmp\maui-child-process-mre\scientific-20260918-193433`.

| Case | Parent / child PID | Outcome | CI XML in exact run window |
|---|---|---|---|
| A — baseline | 10008 / 9948 | Pipe round-trip; child exit `0`. | Empty. |
| B — unused sibling | 11316 / 21820 | Pipe round-trip; child exit `0`. | Empty. |
| C — full Host | 27232 / 4940 | Desktop Host accepted pipes and exited `0`. | Empty. |

Case B rules out a simple directory-presence explanation. Case C did **not** reproduce the earlier
3077 observation, so the unsigned DLL is not a proven cause. The next test must force a known type
from that exact DLL to load, record the loader PID and outcome, and correlate that one load to CI
XML before making any distribution decision.

### Next protocol — direct candidate-DLL load

Use a new managed, framework-dependent MRE child. Native AOT is unsuitable for this question
because it would compile a referenced IL assembly into the executable instead of proving a load of
the on-disk candidate. The child receives the same inherited pipes as the earlier MRE, then creates
`Microsoft.Extensions.DependencyInjection.ServiceDescriptor` from an explicit local reference to
the candidate DLL. It reports its PID, UTC timestamps, resolved assembly location, assembly name,
and hash before returning `RetryRequested`.

Run four fresh folders in `baseline → probe → probe → baseline` order. The baseline child has no
candidate reference; the probe child carries only the frozen exact candidate. Each run saves the
child report, parent outcome, directory manifest, `.deps.json`, signature/MOTW observations, and
CI XML in its exact time window.

| Observation | Meaning |
|---|---|
| Probe emits a correlated 3077 naming the copied candidate; baseline does not | Direct policy enforcement of that DLL is proven. |
| Probe resolves the expected location and hash, succeeds, and has no event | Direct loading is allowed; the earlier event requires another interaction. |
| Resolved location/hash differs | Invalid probe; correct resolution before interpreting it. |
| Baseline also fails or emits the event | Launcher/environment confounded; discard the pair. |
| Probe succeeds but emits a correlated 3077 | The policy blocked a load attempt without preventing this measured action; it remains enforcement evidence, not audit evidence. |

### 2026-09-18 results — direct-dll-load-20260918-194527

The direct-load child was managed (not AOT) so it could map the physical candidate DLL. It used
`AssemblyLoadContext.Default.LoadFromAssemblyPath`, verified the resolved location and SHA-256,
then invoked `ServiceDescriptor.Singleton(Type, Type)` and constructed a `ServiceDescriptor`.

| Order | Case | Child PID | Outcome | CI XML |
|---|---|---:|---|---|
| 1 | Baseline | 28028 | Pipe round-trip; exit `0`. | Empty. |
| 2 | Probe | 19964 | Exact candidate loaded and used; pipe round-trip; exit `0`. | Empty. |
| 3 | Probe | 17520 | Exact candidate loaded and used; pipe round-trip; exit `0`. | Empty. |
| 4 | Baseline | 11684 | Pipe round-trip; exit `0`. | Empty. |

Both probes resolved their own copied
`child\Microsoft.Extensions.DependencyInjection.Abstractions.dll` at SHA-256
`05C941A1F0EF262BB6984C33999E445731A28A1E81EE8604B28F188F9984158A`. Therefore that unsigned
DLL is **not sufficient** to reproduce the earlier CI event or block. It remains an event target
that requires an additional process, payload, or timing condition; do not treat it as the root
cause.

### 2026-09-18 correction — Code Integrity event correlation

The 3033/3077 pair was real, but it was **not** emitted by the recorded `full` reverse-bisect run:

- event time: `19:17:38.63`; process PID `15896`;
- recorded `full` run began `19:17:45.82`; parent PID `20328`, Host PID `27072`;
- the event names `full\\child\\ForgeMission.Desktop.Host.exe` and the candidate DLL, but the
  run's own bounded event query recorded none.

The event establishes that that Host path attempted the load under
`VerifiedAndReputableDesktop`; it does **not** establish that the recorded successful pipe run
produced the event or was blocked by it. The next test runs the exact old `full` folder with a
before/after event window and records every PID.

### 2026-09-18 replay — exact old `full` folder

One replay used the existing `reverse-bisect-20260918-191639\\full` folder and its existing MAUI
parent/piped Host setup. Window: `19:49:17.036`–`19:49:21.123`; parent PID `26088`; Host PID
`21476`. The Host accepted the pipes and exited `0`; the bounded Code Integrity query found no
3033, 3077, or 3118 events. The historical 3077 is therefore not reproduced by the documented
old-folder run either. It is not a basis for the next payload change.

### 2026-09-18 default-payload launch — current `dist/forge-desktop`

One zero-argument launch used the published Supervisor in the current `dist/forge-desktop` folder.
Window: `19:50:23.429`–`19:50:36.021`; Supervisor PID `13220`; Host PID `25360`.
The Host created a `msedgewebview2.exe` child (PID `9996`) and its renderer/GPU/service children.
The bounded Code Integrity query found no 3033, 3077, or 3118 events. The test then stopped only
the Supervisor tree. `ForgeMission.Application.Host.exe` had not started in that 12-second window;
that is startup/runtime-state evidence, not a security block.

This is a direct observation that the present published Supervisor, its Host child, inherited pipes,
and WebView2 startup are allowed on this machine. It does not complete default-path acceptance:
the test deliberately stopped before the app could reach ready state.

### 2026-09-18 historical-hash check — same Supervisor image now allowed

The historical 3077 records for `dist\\forge-desktop\\ForgeMission.Desktop.exe` on 2026-09-17
name SHA-256 `4BEAB9DC605695A2B195580DABAF6BD896C4C9204A21D8103C72F8F756FFC4DE`:

- `16:44:49`: launched by PowerShell; block before application startup;
- `16:46:34`: launched by Explorer; block before application startup;
- `16:59:41`: same image installed under `Program Files`; block before application startup.

The current published `ForgeMission.Desktop.exe` has the **same SHA-256**. It launched successfully
at `19:50` above. A second launch forced `ProcessStartInfo.UseShellExecute = false` (direct
CreateProcess): window `19:51:52.675`–`19:52:01.159`; Supervisor PID `13828`; Host PID `9892`;
WebView2 PID `12308`; no 3033/3077/3118 event. Thus neither the binary contents, folder, inherited
pipes, nor shell-vs-direct process creation explains the old block.

The supported conclusion is narrow: the historical failure was a `VerifiedAndReputableDesktop`
policy decision against this image. Its unsigned state was not sufficient to cause the block; its
disposition later changed outside Forge. The available evidence cannot identify Microsoft's internal
Defender/reputation reason for that change.

### 2026-09-18 historical-install replay — preserved original file

The file under `C:\\Program Files\\Forge Desktop` is the copy created at `2026-09-17 16:43:28`.
It still has the same SHA-256 (`4BEAB9DC…FFC4DE`) and no Zone.Identifier stream. It was the target
of the 16:59 historical 3077. Direct launch on 2026-09-18, window `20:10:00.977`–`20:10:09.640`,
started Supervisor PID `21252` and Host PID `26044`; no 3033, 3077, or 3118 event occurred. This
removes later republishing, a changed file identity, and the working `dist` folder as explanations.

### 2026-09-18 policy-state observation

Read-only system state at the time of the passing replays:

- `Get-MpComputerStatus`: `SmartAppControlState = On`.
- `Win32_DeviceGuard`: kernel and user-mode Code Integrity enforcement status `2` (enforced).
- Active policy includes `{0283AC0F-FFF1-49AE-ADA1-8A933130CAD6}.cip`, the same policy GUID in
  the Forge 3077 events.
- AppLocker logged policy application only, not a Forge block.

The underlying effective-policy listing requires elevation (`CiTool.exe -lp` returned
`0x80070005`), but this is not needed to identify the policy that issued the historical block.

## Current position

- Do not remove or duplicate the Supervisor because of a presumed child-process ban.
- Do not describe inherited pipes as the cause.
- Default-path acceptance remains open. The present published zero-argument Desktop now starts the
  Supervisor, Host, and WebView2; it was stopped before reaching ready state. Historical blocks
  remain unreplicated observations, while the MAUI DLL route's white-window finding remains open.
- The 3077 observation is unreplicated and not currently causal evidence: its path/named process
  match the old `full` folder, but its timestamp/PID match neither the recorded run nor its replay.
- The original Supervisor block has a direct cause: a time-specific SAC Defender-trust rejection
  of the exact image. It is currently allowed with the same hash and original installed file.

No further Forge payload/policy bisect is warranted without a new correlated 3077. Separately,
verify MAUI page mount → WebView handler → WebView2 child → Booting content → loopback navigation.

## Approved next MRE — workload-shape reproduction

**Question:** can an unfamiliar user-launched Native-AOT console Supervisor, which stages the same
kind of local GUI/TUI and loopback workload as Forge, reproduce a correlated SAC block?

This replaces the prior MAUI-parent MRE as the first-image test. It does not alter Forge.

```text
Mre.TransportSupervisor.exe       Native AOT console; sole child-lifetime owner
├─ Mre.GuiEndpoint.exe            managed MAUI, selected GUI surface
│  OR Mre.TuiEndpoint.exe          Native AOT console, selected TUI surface
└─ Mre.LoopbackReadyService.exe    Native AOT loopback service, added at stage 2
```

The Supervisor starts exactly one surface per run. GUI/TUI parity remains at the shared Application
contract below this local control boundary; neither surface gets runtime ownership or secrets.

| Stage | Changed workload | Required evidence |
|---|---|---|
| 0 | Supervisor only | Process starts/exits; bounded CI XML. |
| 1 | Add selected GUI or TUI endpoint | Endpoint starts; Supervisor cleans it up; bounded CI XML. |
| 2 | Add loopback ready service, reporting an ephemeral URL through redirected stdout | Ready marker, all PIDs, cleanup, bounded CI XML. |
| 3 | Add harmless third child only if stage 2 passes | Same evidence; isolates child count. |

Every fresh run records manifest/hash/signature/MOTW, UTC start/end, command lines, process tree,
ready marker, supervisor cleanup, and all 3033/3077/3118 events in that exact window. A security
reproduction requires an event that names a recorded process image/hash/PID; an ordinary launch
failure does not count.

Only after a correlated block will the reproducing workload be frozen and its control transport
compared: explicit inherited handles, redirected stdin/stdout (still OS pipes), then a per-run
temporary-file mailbox (the only no-pipe control). No transport fix is justified before then.

### 2026-09-18 preliminary result — fresh Native-AOT console Supervisor

The new workload matrix produced the first reproducible Forge-shaped block. Five fresh launches
under `C:\\Users\\ameer\\tmp\\maui-child-process-mre\\transport-workload-20260918-202847` were
blocked before `Mre.TransportSupervisor.Main`; no GUI/TUI endpoint or ready service started.
Manual event capture for the first launch at `20:27:52` found 3033/3077/3118 naming that fresh
`Mre.TransportSupervisor.exe` and policy `{0283AC0F-FFF1-49AE-ADA1-8A933130CAD6}`.

The first runner queried too soon and missed the asynchronously written event, so this result is
preliminary until the same matrix is re-run with a padded post-launch event window and per-run
PID/hash correlation. The next binary variable, after that evidence is complete, is PE subsystem
and runtime format; do not change control transport yet.

### 2026-09-18 reproduced — Native-AOT console Supervisor alone

The corrected workload-matrix run is
`C:\\Users\\ameer\\tmp\\maui-child-process-mre\\transport-workload-20260918-203341`.
All five fresh cases were blocked before `Mre.TransportSupervisor.Main`:

```text
00-supervisor-only
01-gui-endpoint
01-tui-endpoint
02-gui-service
02-tui-service
```

Every case has raw 3033, 3077, and 3118 XML in its exact recorded window. The 3077 target is the
case's `Mre.TransportSupervisor.exe`, flat SHA-256
`04DB6EA11DD3E4A77940B0B97DDE0B9BF8E833E9051F08B4F03FCC45A4DCA2D4`, attempted by `pwsh.exe`,
under `VerifiedAndReputableDesktop` / `{0283ac0f-fff1-49ae-ada1-8a933130cad6}`. The 3118 detail
records `DefenderCalled=true`, `DefenderCloudCallRequested=true`, `TTLValid=false`, and
`DefenderTrust=-16777216`.

No Supervisor PID/report, endpoint, or loopback service exists in any case. This proves the
reproducing workload is the fresh Native-AOT **console** Supervisor itself. GUI/TUI selection,
child count, loopback readiness, inherited pipes, stdio, and temporary files are downstream and
cannot cause this pre-`Main` block. The next matrix changes binary shape only: console subsystem,
Windows subsystem, and an empty Native-AOT console control.

### 2026-09-18 binary-shape control — inconclusive

`C:\\Users\\ameer\\tmp\\maui-child-process-mre\\binary-shape-20260918-204039` ran three fresh
single-executable cells with no children or IPC. All passed with empty bounded CI XML:

| Cell | Shape | SHA-256 | Result |
|---|---|---|---|
| C | Same Supervisor source, Native-AOT console / `WindowsCui` | `FA1F3BDD…F7173` | PID `9036`, exit `0`. |
| S | C with only PE checksum/subsystem bytes changed to `WindowsGui` | `28A8E3BB…51EF1` | PID `17560`, exit `0`. |
| W | Empty Native-AOT console | `C4148BAC…650F7` | PID `28220`, exit `0`. |

C/S had equal length and differed only in the PE checksum/subsystem bytes. This does **not** support
console vs GUI subsystem, Native AOT, child IPC, or the compiled Supervisor workload as a sufficient
cause. It also means the earlier statement that the fresh console shape alone reproduced the block
was too broad: the reproducible object is currently the specific blocked hash
`04DB6EA1…A2D4`, not its measured binary-shape class. Retest that preserved hash next.

### 2026-09-18 preserved-hash replay — block is hash-stable

The preserved blocked file was launched in place, without copying or rebuilding:
`transport-workload-20260918-203341\\00-supervisor-only\\Mre.TransportSupervisor.exe`.
It remains 2,884,608 bytes, unsigned, has no Zone.Identifier, and SHA-256
`04DB6EA11DD3E4A77940B0B97DDE0B9BF8E833E9051F08B4F03FCC45A4DCA2D4`.

Direct `System.Diagnostics.Process` (`UseShellExecute=false`) at `20:48:19.097` threw
`Win32Exception 4551` before a PID or `Main`. The raw 3033/3077/3118 records at
`20:48:19.255`/`.273` name the exact path and hash. 3118 again records
`DefenderCalled=true`, `DefenderCloudCallRequested=true`, `TTLValid=false`, and
`DefenderTrust=-16777216`.

This verdict is stable for that hash across at least 14 minutes. The passing C image from the
binary-shape control is a different hash, so the next experiment is a binary diff between those
two same-shape Supervisor builds—not a transport change.

### 2026-09-18 binary diff — linker timestamp changes the SAC identity

The blocked `04DB…A2D4` and passing `FA1F…F7173` Supervisor builds have the same length
(2,884,608), version metadata, source, code, imports, and PE shape. Exactly four bytes differ:

- PE COFF `TimeDateStamp` at offsets `280–281`;
- the same timestamp copy in the PE debug directory at offsets `2,377,844–2,377,845`.

`04DB…` was linked at `18:26:30Z`; `FA1F…` at `18:38:25Z`. Native AOT's linker embedded those
timestamps, so otherwise identical publishes have different full hashes. The blocked hash remains
blocked; the timestamp-only different hash is allowed. This is the first causal discriminator.

**Finding:** SAC is making a hash-specific trust decision. There is no evidence that anonymous
pipes, GUI/TUI child shape, loopback work, PE subsystem, or compiled Supervisor behavior decides
the block. A transport or process-architecture rewrite cannot make this reliable. The next MRE
fix experiment is deterministic artifact identity: canonicalize the AOT timestamp in two clean
publishes, prove equal hashes, then observe whether that one stable artifact has one stable verdict.

### Approved hash-identity matrix

Three otherwise identical 2,884,608-byte images will run in interleaved order
`B1, P1, T1, T2, P2, B2`, each from a fresh case folder with no child, IPC, loopback, or argument
difference:

- **B:** preserved blocked hash `04DB…A2D4`;
- **P:** preserved passing hash `FA1F…F7173`;
- **T:** a copy of B whose two duplicated 32-bit PE timestamp fields change to one fixed third value.

Before launch, the runner fails unless B↔P and B↔T differ only at those four timestamp bytes, and
T's two fields match. Each case records identity, byte-diff proof, pre-`Main` exception/PID,
and a padded (`start − 1s` to `end + 5s`) raw CI XML window. A block is valid only when 3077 names
that case's hash. This determines whether hash identity itself is enough to change present SAC
treatment before a deterministic-build remedy is attempted.

### 2026-09-18 timestamp-identity result — hash changes SAC treatment

`C:\\Users\\ameer\\tmp\\maui-child-process-mre\\supervisor-identity-20260918-205425` ran the
interleaved order `B1, P1, T1, T2, P2, B2`:

| Image | Hash | Result |
|---|---|---|
| B — preserved blocked timestamp | `04DB…A2D4` | Both runs blocked pre-`Main`, each with correlated 3077/3118. |
| P — preserved passing timestamp | `FA1F…F7173` | Both runs exit `0`; no CI event. |
| T — B with only the two duplicated timestamp fields changed to `1789750350` | `0F72BE20B08F04CDDAF6AAAC3C544287FC21D806903C98DF16540A120FB3C957` | Both runs exit `0`; no CI event. |

This establishes, for the current policy state, that a timestamp-only full-hash change changes SAC
treatment. The MRE remedy is therefore deterministic artifact identity: normalize the verified AOT
timestamp fields after each clean publish, fail closed if the expected PE layout changes, prove two
publishes yield the same passing hash, and run both. It preserves the Supervisor and leaves GUI/TUI
and IPC untouched. This investigation does not propose signing as the remedy: unsigned MRE
workloads already pass, and the controlled workaround is reproducible artifact identity.

### 2026-09-18 deterministic unsigned fix — supervisor-only verified

`C:\\Users\\ameer\\tmp\\maui-child-process-mre\\deterministic-supervisor-20260918-205944`
clean-published the unchanged Native-AOT console Supervisor twice. Their raw timestamps/hashes
differed, but normalization changed only offsets `280,281,2377844,2377845` in each image and made
both exactly `0F72BE20B08F04CDDAF6AAAC3C544287FC21D806903C98DF16540A120FB3C957`.

Both normalized images are unsigned, CUI subsystem `3`, no MOTW, and 2,884,608 bytes. N1 (PID
`2984`) and N2 (PID `16404`) launched directly, exited `0`, and each has empty padded CI XML.
This verifies deterministic artifact identity for the Supervisor alone. GUI/TUI plus ready-service
verification remains required before accepting the MRE fix for the full supervision model.

### 2026-09-18 deterministic unsigned fix — GUI and TUI verified

`C:\\Users\\ameer\\tmp\\maui-child-process-mre\\deterministic-supervisor-workload-20260918-211108`
uses that same normalized unsigned Supervisor hash with the unchanged endpoint/service payloads:

| Case | Supervisor | Loopback service | Selected surface | Result |
|---|---:|---:|---:|---|
| GUI-S | `9892` | `24528` | MAUI GUI `28136` | Report `passed`; Supervisor exit `0`. |
| TUI-S | `2776` | `12312` | AOT TUI `10340` | Report `passed`; Supervisor exit `0`. |

Both cases contain 12+ live snapshots with exactly the Supervisor and its two direct children,
then an empty cleanup snapshot. Their padded CI XML is empty. This verifies that the fixed,
unsigned artifact retains separate Supervisor ownership, GUI/TUI alternatives, loopback readiness,
and child cleanup.

### Proposed Forge direction

Keep the separate Supervisor and its existing narrow control contract. Pipes are not part of the
block and should not be changed to address it. The MRE fix is a **deterministic Native-AOT artifact
identity step** in publishing: normalize the two verified linker timestamp fields, fail closed on
any unexpected PE layout/byte difference, and verify repeat publishes have one hash before release.

The MRE scripts are `publish-deterministic-supervisor.ps1`,
`run-deterministic-supervisor-matrix.ps1`, and the GUI/TUI workload verifier. Their fixed timestamp
produces the tested `0F72…C957` hash for this unchanged program body. A Forge implementation must
not hard-code that MRE hash or timestamp: source changes alter the legitimate program body. It needs
the same fail-closed reproducibility check and a fresh policy observation for each changed body.
