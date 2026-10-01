// Read-only: every display's frame (global points, Cocoa bottom-left origin), visible frame and backing scale.
import AppKit
for s in NSScreen.screens {
  let f = s.frame, v = s.visibleFrame
  print(s.localizedName, "frame", Int(f.minX), Int(f.minY), Int(f.width), Int(f.height), "visible", Int(v.minX), Int(v.minY), Int(v.width), Int(v.height), "scale", s.backingScaleFactor)
}
