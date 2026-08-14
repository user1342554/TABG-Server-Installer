# TABG Performance Server

This BepInEx plugin targets custom/private TABG dedicated servers. Version 2.0 restores dirty-field entity snapshots with periodic full keyframes, fixes the car-only queue starvation and stale-car queue bugs, bounds queued update packets, writes chunk-entry packets at their exact used length, parses hot player packets without streams, uses direct player/chunk lookups, and removes production IMGUI work.

Install the matching `TabgInstaller.PerformanceClient.dll` on players connecting to a server with delta snapshots enabled. The server can fall back to vanilla full snapshots by setting `EnableDeltaSnapshots = false` in `BepInEx/config/tabginstaller.performanceserver.cfg`.

The queue payload target defaults to 1200 bytes. Individual messages larger than that limit are sent intact because TABG's message format cannot split a single entity record without a protocol extension.
