# Contributing

This launcher ships a curated set of bundled plugins. It no longer has a runtime plugin marketplace, so plugin changes should be source changes in this repository instead of remote manifest-only additions.

## Adding A Bundled Plugin

1. Add the plugin source as a normal project in the solution.
2. Target BepInEx 5 / `netstandard2.0` unless the surrounding plugin stack changes.
3. Copy the release DLL into `TabgInstaller.Gui/plugins` or `TabgInstaller.Gui/client-plugins` from the project build target.
4. Register it in `TabgInstaller.Core/PluginRegistry.cs` and the matching launcher plugin list.
5. Add a `registry/plugins/<id>/manifest.json` only for release metadata and documentation.
6. Build the plugin plus the launcher projects before opening a PR.

## Compatibility

`TabgInstaller.MatchCore` intentionally keeps reading `TheStarterPack.txt` so existing presets, rings, loadouts, and match settings continue to work while the implementation is owned by this repository.

## Questions

- **Discord:** `anonymer__hase_22156`
- **GitHub Issues:** [Open an issue](https://github.com/user1342554/TABG-Server-Installer/issues)
