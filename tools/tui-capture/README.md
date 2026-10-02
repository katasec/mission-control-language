# tui-capture

Supervisor tools for live `forge chat` checks in Ghostty: real terminal, real images, scripted keys,
window captures. macOS only. Used for Phase 56 and 57 acceptance.

| File | Does |
|---|---|
| `relay.py <typescript> <keys.json> <cmd...>` | Runs `cmd` in a pty inside the terminal, passes bytes both ways (so Ghostty answers probes and shows images), records output to `<typescript>`, forwards window resizes, types scripted keys. Exit code goes to `<typescript>.rc`. |
| `cap.sh <name> "<delays>" <cmd...>` | Opens a new Ghostty running `cmd`, captures its window at each delay, quits only that Ghostty. |
| `win.swift <pid>` / `winall.swift <pid>` | Window ids of a process: on-screen only / all Spaces. |
| `screens.swift` | Displays, frames, backing scale (Retina = 2.0). |
| `session.swift` | `screenLocked=…`. A locked screen means no capture. |

**Example: two-turn chat, capture at 64 s**

```bash
cat > keys.json <<'EOF'
[[30, "Reply with just: one\r"], [46, "Reply with just: two\r"], [70, "\u0004"]]
EOF
bash tools/tui-capture/cap.sh run "64" /usr/bin/python3 tools/tui-capture/relay.py ts keys.json ~/.local/bin/forge chat
LC_ALL=C grep -ao $'\x1b_Ga=T' ts | wc -l   # images sent; 8 per session since Phase 56 Task 2
```

**Gotchas**

| | |
|---|---|
| Absolute paths | Pass absolute paths for the typescript, the keys file and the binary. The Ghostty window starts in `$HOME`, so relative paths fail with `FileNotFoundError`. |
| Keys file | Write it with a quoted heredoc. `echo` turns `\u0004` into a raw control byte and the JSON breaks. |
| Cold start | The backend can take 10–20 s. Send the first message at 25 s or later, or it lands in the composer and merges with the next message. |
| Window placement | Ghostty may ignore the position flags. Check the capture size against the window size: equal means 1×, double means Retina. |
| Capture after exit | Ctrl-D closes the window. Capture before the Ctrl-D time. |
| Exit codes | Read `<typescript>.rc`. pwsh's `$?` is true/false, not the exit code. |
| tmux | Use `tmux -f /dev/null` so the user's `~/.tmux.conf` doesn't interfere. |
| Messages are real | Every message sent is a real turn in the user's `chat` conversation. Keep them short. |
| Your Ghostty | Never kill a Ghostty that `cap.sh` didn't launch. |
