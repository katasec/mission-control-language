#!/usr/bin/env python3
"""Supervisor acceptance relay: run a command in a pty inside the real terminal (Ghostty), pass all
bytes both ways (so the terminal answers probes and shows images), record the command's output, and
type scripted keys at fixed times. Usage: relay.py <typescript> <keys.json> <cmd> [args...]
keys.json: [[seconds_after_start, "text with \\r, \\u0003 (Ctrl-C), \\u0004 (Ctrl-D), \\u001b[5~ (PgUp)"], ...]"""
import fcntl, json, os, pty, select, signal, struct, sys, termios, time, tty

def main():
    typescript, keyfile, cmd = sys.argv[1], sys.argv[2], sys.argv[3:]
    keys = sorted(json.load(open(keyfile)))
    pid, master = pty.fork()
    if pid == 0:
        os.execvp(cmd[0], cmd)
    copy_window_size(master)
    signal.signal(signal.SIGWINCH, lambda *_: (copy_window_size(master), os.kill(pid, signal.SIGWINCH)))
    old = termios.tcgetattr(0)
    tty.setraw(0)
    try:
        relay(master, pid, keys, open(typescript, "wb"))
    finally:
        termios.tcsetattr(0, termios.TCSAFLUSH, old)
    _, status = os.waitpid(pid, 0)
    with open(typescript + ".rc", "w") as f:
        f.write(str(os.waitstatus_to_exitcode(status)))

def copy_window_size(master):
    size = fcntl.ioctl(0, termios.TIOCGWINSZ, b"\0" * 8)
    fcntl.ioctl(master, termios.TIOCSWINSZ, size)

def relay(master, pid, keys, log):
    start = time.monotonic()
    while True:
        due = [k for k in keys if k[0] <= time.monotonic() - start]
        for k in due:
            os.write(master, k[1].encode())
            keys.remove(k)
        try:
            ready, _, _ = select.select([0, master], [], [], 0.05)
        except InterruptedError:
            continue
        if master in ready:
            try:
                data = os.read(master, 65536)
            except OSError:
                return
            if not data:
                return
            os.write(1, data)
            log.write(data); log.flush()
        if 0 in ready:
            os.write(master, os.read(0, 4096))

if __name__ == "__main__":
    main()
