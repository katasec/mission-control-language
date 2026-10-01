// Read-only: all layer-0 windows of one process (any Space): id, onscreen, bounds.
import CoreGraphics
let pid = Int32(CommandLine.arguments[1])!
let list = CGWindowListCopyWindowInfo([.optionAll], kCGNullWindowID) as! [[String: Any]]
for w in list where (w[kCGWindowOwnerPID as String] as? Int32) == pid && (w[kCGWindowLayer as String] as? Int) == 0 {
  let b = w[kCGWindowBounds as String] as! [String: Any]
  print(w[kCGWindowNumber as String]!, w[kCGWindowIsOnscreen as String] ?? false, b["X"]!, b["Y"]!, b["Width"]!, b["Height"]!)
}
