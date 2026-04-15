# Contributing a Plugin to the TABG Marketplace

Want your plugin in the marketplace? Follow the steps below — or see [`registry/CONTRIBUTING.md`](./registry/CONTRIBUTING.md) for the full detailed guide.

## Requirements

- Your plugin is a BepInEx 5.4.22 plugin that works with TABG dedicated servers
- Your DLLs are hosted as GitHub releases

## Steps

### 1. Fork this repository

Fork [`user1342554/TABG-Server-Installer`](https://github.com/user1342554/TABG-Server-Installer) on GitHub.

### 2. Create your manifest

Copy [`registry/TEMPLATE.json`](./registry/TEMPLATE.json) to `registry/plugins/<your-plugin-id>/manifest.json`.

### 3. Fill in your plugin details

```json
{
  "id": "my-cool-plugin",
  "name": "My Cool Plugin",
  "version": "1.0.0",
  "description": "Does something cool on your TABG server.",
  "author": "YourGitHubUsername",
  "downloadUrl": "https://github.com/You/my-cool-plugin/releases/latest",
  "dllNames": ["MyCoolPlugin.dll"],
  "type": "server",
  "compatibleTabgVersions": ["*"],
  "minInstallerVersion": "5.0.0",
  "bepInExVersion": "5.4.22"
}
```

### 4. Open a pull request

CI validates your manifest automatically (schema, download URL, duplicate check).

### 5. Done

Once merged, your plugin appears in the **Browse Plugins** tab for everyone.

## Updating Your Plugin

1. Push a new GitHub release with your updated DLLs
2. Open a PR bumping the `version` field in your manifest

## Plugin Types

| Type | Where it gets installed |
|------|------------------------|
| `server` | Server's `BepInEx/plugins/community/<id>/` |
| `client` | Client's `BepInEx/plugins/community/<id>/` |
| `both` | Both server and client |

## Registry Details

The marketplace is powered by a GitHub-based registry. Manifests are validated by CI on every pull request and compiled into a single `registry.json` that the app fetches at runtime.

- **Schema**: [`registry/schema/plugin-manifest.schema.json`](./registry/schema/plugin-manifest.schema.json)
- **Template**: [`registry/TEMPLATE.json`](./registry/TEMPLATE.json)
- **Full guide**: [`registry/CONTRIBUTING.md`](./registry/CONTRIBUTING.md)

## Questions?

- **Discord:** `anonymer__hase_22156`
- **GitHub Issues:** [Open an issue](https://github.com/user1342554/TABG-Server-Installer/issues)
