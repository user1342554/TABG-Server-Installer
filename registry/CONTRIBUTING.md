# Bundled Plugin Manifests

`registry/plugins` is release metadata for plugins that ship with this launcher. It is not a public marketplace.

Add or update a manifest only when the plugin DLL is built by this repository or intentionally bundled in `TabgInstaller.Gui/plugins` or `TabgInstaller.Gui/client-plugins`.

## Checklist

1. Keep the manifest `id` equal to the folder name.
2. Set `dllNames` to the exact bundled DLL names.
3. Use `type` to describe where the bundled DLL is installed: `server`, `client`, or `both`.
4. Regenerate `registry/registry.json` after changing manifests.
5. Build the affected plugin and launcher project.
