# Bundled Plugin Manifests

`registry/plugins` is release metadata for plugins that ship with this launcher. It is not a public marketplace.

Add or update a manifest only when the plugin DLL is built by this repository or intentionally bundled in `TabgInstaller.Gui/plugins` or `TabgInstaller.Gui/client-plugins`.

## Checklist

1. Keep the manifest `id` equal to the folder name.
2. Set `dllNames` to the exact bundled DLL names.
3. Use `type` to describe where the bundled DLL is installed: `server`, `client`, or `both`.
4. Do not create multiple manifests on the same side that point at the same DLL. Use one combined plugin and expose sub-feature config toggles.
5. Keep `dependencies` and `clientPluginId` values pointed at real registry IDs.
6. Regenerate `registry/registry.json` after changing manifests when release metadata needs to be refreshed.
7. Build the affected plugin and launcher project, then run the registry manifest tests.
