// Read-only: is the login session's screen locked, and is the main display asleep?
import CoreGraphics
let d = CGSessionCopyCurrentDictionary() as? [String: Any] ?? [:]
print("screenLocked=\(d["CGSSessionScreenIsLocked"] ?? false) onConsole=\(d["kCGSSessionOnConsoleKey"] ?? "?") displayAsleep=\(CGDisplayIsAsleep(CGMainDisplayID()))")
