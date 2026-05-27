#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

if ! command -v jq >/dev/null 2>&1; then
  echo "ERROR: jq is required for registry validation." >&2
  exit 1
fi

mapfile -t manifests < <(find registry/plugins -mindepth 2 -maxdepth 2 -name manifest.json | sort)
if [[ ${#manifests[@]} -eq 0 ]]; then
  echo "ERROR: no plugin manifests found." >&2
  exit 1
fi

errors=0
declare -A ids=()
declare -A folders_by_id=()

fail() {
  echo "ERROR: $*" >&2
  errors=1
}

for manifest in "${manifests[@]}"; do
  id="$(jq -r '.id // empty' "$manifest")"
  folder="$(basename "$(dirname "$manifest")")"

  if [[ -z "$id" ]]; then
    fail "$manifest is missing id"
    continue
  fi

  if [[ "$id" != "$folder" ]]; then
    fail "$manifest id '$id' does not match folder '$folder'"
  fi

  if [[ -n "${ids[$id]:-}" ]]; then
    fail "duplicate plugin id '$id' in $manifest and ${ids[$id]}"
  fi

  ids[$id]="$manifest"
  folders_by_id[$id]="$folder"
done

declare -A payload_owner=()

for manifest in "${manifests[@]}"; do
  id="$(jq -r '.id // empty' "$manifest")"
  type="$(jq -r '.type // empty' "$manifest")"

  case "$type" in
    server) sides=("server") ;;
    client) sides=("client") ;;
    both) sides=("server" "client") ;;
    *)
      fail "$id has invalid type '$type'"
      continue
      ;;
  esac

  while IFS= read -r dependency; do
    [[ -z "$dependency" ]] && continue
    if [[ -z "${ids[$dependency]:-}" ]]; then
      fail "$id depends on unknown plugin id '$dependency'"
    fi
  done < <(jq -r '.dependencies[]? // empty' "$manifest")

  client_plugin_id="$(jq -r '.clientPluginId // empty' "$manifest")"
  if [[ -n "$client_plugin_id" && -z "${ids[$client_plugin_id]:-}" ]]; then
    fail "$id references unknown clientPluginId '$client_plugin_id'"
  fi

  while IFS= read -r dll; do
    [[ -z "$dll" ]] && continue
    for side in "${sides[@]}"; do
      key="$side|$dll"
      owner="${payload_owner[$key]:-}"
      if [[ -n "$owner" && "$owner" != "$id" ]]; then
        fail "$id and $owner both declare $dll for $side"
      fi

      payload_owner[$key]="$id"

      if [[ "$side" == "server" ]]; then
        payload_path="TabgInstaller.Gui/plugins/$dll"
      else
        payload_path="TabgInstaller.Gui/client-plugins/$dll"
      fi

      if [[ ! -f "$payload_path" ]]; then
        fail "$id declares missing bundled payload $payload_path"
      fi
    done
  done < <(jq -r '.dllNames[]? // empty' "$manifest")
done

if [[ -f registry/registry.json ]]; then
  manifest_ids="$(mktemp)"
  registry_ids="$(mktemp)"
  printf '%s\n' "${!ids[@]}" | sort > "$manifest_ids"
  jq -r '.plugins[].id' registry/registry.json | sort > "$registry_ids"

  missing_from_registry="$(comm -23 "$manifest_ids" "$registry_ids" || true)"
  extra_in_registry="$(comm -13 "$manifest_ids" "$registry_ids" || true)"
  rm -f "$manifest_ids" "$registry_ids"

  if [[ -n "$missing_from_registry" ]]; then
    fail "registry.json is missing manifest ids: ${missing_from_registry//$'\n'/, }"
  fi

  if [[ -n "$extra_in_registry" ]]; then
    fail "registry.json contains ids without manifests: ${extra_in_registry//$'\n'/, }"
  fi
else
  fail "registry/registry.json is missing"
fi

if [[ "$errors" -ne 0 ]]; then
  exit 1
fi

echo "Validated ${#manifests[@]} plugin manifest(s)."
