# Contributing a Plugin to the TABG Marketplace

## Prerequisites

- Your plugin is hosted on GitHub with releases containing the DLL files.
- Your plugin works with BepInEx 5.4.22 on a TABG dedicated server.

## Steps

### 1. Fork this repository

Fork `user1342554/TABG-Server-Installer` on GitHub.

### 2. Create your manifest

Copy `registry/TEMPLATE.json` to `registry/plugins/<your-plugin-id>/manifest.json`.

Replace `<your-plugin-id>` with a unique kebab-case identifier (e.g., `my-cool-plugin`).

### 3. Fill in your manifest

Edit the manifest with your plugin's details. See `registry/schema/plugin-manifest.schema.json` for the full field reference.

**Required fields:** `id`, `name`, `version`, `description`, `author`, `downloadUrl`, `dllNames`, `type`, `compatibleTabgVersions`, `minInstallerVersion`, `bepInExVersion`.

**Important:**
- `id` must match your folder name exactly.
- `downloadUrl` must point to your GitHub releases (e.g., `https://github.com/you/plugin/releases/latest`).
- `dllNames` must list every DLL file in your release that should be installed.
- `version` must be valid semver (e.g., `1.0.0`).

### 4. Validate locally (optional)

You can validate your manifest against the schema using any JSON Schema validator:

```bash
npx ajv-cli validate -s registry/schema/plugin-manifest.schema.json -d registry/plugins/your-plugin-id/manifest.json
```

### 5. Submit a Pull Request

Push your branch and open a PR. The CI will automatically validate your manifest and post results as a comment.

### 6. Wait for review

A maintainer will review your submission and merge it. Once merged, your plugin will appear in the app's Browse Plugins tab.

## Updating Your Plugin

To release a new version:

1. Create a new GitHub release with your updated DLLs.
2. Submit a PR updating the `version` field in your manifest.

## Plugin Types

| Type | Install Location |
|------|-----------------|
| `server` | Server's `BepInEx/plugins/community/<id>/` |
| `client` | Client's `BepInEx/plugins/community/<id>/` |
| `both` | Both locations |

## Questions?

Open an issue on this repository.
