# TABG Modding And Installer Integration

This repo already contains the working pattern for TABG mods:

- BepInEx 5.4.22 plugin DLLs are loaded by the dedicated server or by a copied client install.
- Mods compile against TABG's Unity/Mono assemblies from `TABG_Data/Managed`, especially `Assembly-CSharp.dll`.
- Server mods go to `BepInEx/plugins`.
- Client mods go to a modded TABG copy's `BepInEx/plugins`.
- Bundled install choices are owned by `PluginRegistry` and documented by `registry/plugins/<id>/manifest.json`.

The decoded reference project at:

`/run/media/jonasn/Daten und Programme/_Organized/Projects/TABG/GigaSchmigaTABG`

is useful for finding TABG class names, fields, and method signatures. The most useful folders are:

- `DecompiledServer/` - dedicated server classes such as `ServerClient`, `BattleRoyaleGameMode`, `TABGPlayerServer`, `GameRoom`.
- `DecompiledClient/` - client classes such as `ServerConnector`, `PhotonServerHandler`, `Player`, `TABGPlayerClient`.
- `Citruslib-master/` - examples for server-side command, player, loot, and settings helpers.

## Project Shape

Use `netstandard2.0` for BepInEx plugin projects. The existing projects use local `Libs/` DLL references and mark them as non-private so game/framework DLLs are not copied into the plugin output:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>disable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="0Harmony">
      <HintPath>Libs\0Harmony.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="BepInEx">
      <HintPath>Libs\BepInEx.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="Assembly-CSharp">
      <HintPath>Libs\Assembly-CSharp.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>Libs\UnityEngine.CoreModule.dll</HintPath>
      <Private>False</Private>
    </Reference>
  </ItemGroup>
</Project>
```

Use these existing projects as templates:

- Server only: `TabgInstaller.UnusedVehicles`, `TabgInstaller.SoloTesting`.
- Client only: `TabgInstaller.CoordsDisplay`, `TabgInstaller.FlyingControls`.
- Client/server pair: `TabgInstaller.ProximityChat.Server` plus `TabgInstaller.ProximityChat.Client`.
- Client/server pair with shared constants: `TabgInstaller.HuntMode`, `TabgInstaller.HuntMode.Client`, `TabgInstaller.HuntMode.Shared`.

## Basic Plugin Skeleton

```csharp
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace MyTabgMod;

[BepInPlugin("your.name.mytabgmod", "My TABG Mod", "1.0.0")]
public sealed class MyTabgModPlugin : BaseUnityPlugin
{
    private Harmony _harmony;
    private ConfigEntry<bool> _enabled;

    private void Awake()
    {
        _enabled = Config.Bind("General", "Enabled", true, "Enable this mod.");

        _harmony = new Harmony("your.name.mytabgmod");
        _harmony.PatchAll();

        Logger.LogInfo("My TABG Mod loaded.");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }
}
```

## Server-Side Mods

Server mods run in the dedicated server process. Good entry points from the decoded server:

- `Landfall.Network.ServerClient` - main network/server object.
- `ServerClient.GameRoomReference` - access to `GameRoom`, players, settings, and game state.
- `GameRoom.Players` - list of `TABGPlayerServer`.
- `TABGPlayerServer.PlayerIndex`, `PlayerName`, `PlayerPosition`, `GroupIndex`, `Health`.
- `Landfall.Network.GameModes.BattleRoyaleGameMode.Run` - useful per-tick game mode hook.
- `ServerClient.HandleNetorkEvent` - intercept custom client-to-server packets. The misspelling is real in TABG.

Example server network hook:

```csharp
using BepInEx;
using HarmonyLib;
using Landfall.Network;

[BepInPlugin("your.name.servermod", "Server Mod", "1.0.0")]
public sealed class ServerModPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        new Harmony("your.name.servermod").PatchAll();
    }

    [HarmonyPatch(typeof(ServerClient), "HandleNetorkEvent")]
    private static class CustomEventPatch
    {
        private const byte CustomEventCode = 242;

        private static bool Prefix(ServerPackage networkEvent, ServerClient __instance)
        {
            if ((byte)networkEvent.Code != CustomEventCode)
                return true;

            var sender = networkEvent.SenderPlayerID;
            var payload = networkEvent.Buffer;

            // Handle payload, then optionally relay to clients.
            __instance.SendMessageToClients(
                (EventCode)CustomEventCode,
                payload,
                byte.MaxValue,
                reliable: true);

            return false;
        }
    }
}
```

Use Citruslib when you need high-level server helpers:

- Chat commands via `Citrus.AddCommand`.
- Player lookup and admin permission helpers.
- Teleport, team changes, custom loot tables, and settings files.

## Client-Side Mods

Client mods run in a copied TABG install created by the installer. Good entry points from the decoded client:

- `Landfall.Network.ServerConnector.Instance` - sends messages to the server.
- `ServerConnector.SendMessageToServer(EventCode code, byte[] buffer, bool reliable)` - client-to-server.
- `ServerConnector.OnEvent(ClientPackage clientPackage)` - server-to-client receive hook.
- `Player.localPlayer` - local player object.
- `PhotonServerHandler.instance.LocalPlayer` - local network player data in many gameplay contexts.
- IMGUI hooks via `OnGUI()` for simple overlays.

Example client send/receive hook:

```csharp
using BepInEx;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;

[BepInPlugin("your.name.clientmod", "Client Mod", "1.0.0")]
public sealed class ClientModPlugin : BaseUnityPlugin
{
    private const byte CustomEventCode = 242;

    private void Awake()
    {
        new Harmony("your.name.clientmod").PatchAll();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F7) && ServerConnector.Instance != null)
        {
            ServerConnector.Instance.SendMessageToServer(
                (EventCode)CustomEventCode,
                new byte[] { 1 },
                reliable: true);
        }
    }

    [HarmonyPatch(typeof(ServerConnector), "OnEvent")]
    private static class ReceivePatch
    {
        private static bool Prefix(ClientPackage clientPackage)
        {
            if ((byte)clientPackage.Code != CustomEventCode)
                return true;

            var payload = clientPackage.Buffer;
            // Handle server payload.
            return false;
        }
    }
}
```

## Client/Server Networking

TABG's stock `EventCode` is a byte enum. The decoded enum uses many values up to `230` and reserves Photon-style values `248-255`. Existing custom installer mods currently use:

- `240` - Proximity Chat.
- `241` - Admin Radar.

Use a different unused byte, document it in your shared project or plugin constants, and keep the binary payload format identical on both sides.

For client/server pairs:

1. Put event codes and payload constants in a shared `netstandard2.0` project if both DLLs need them.
2. Server receives client packets by patching `ServerClient.HandleNetorkEvent`.
3. Client sends packets with `ServerConnector.Instance.SendMessageToServer`.
4. Server sends packets with `ServerClient.SendMessageToClients`.
5. Client receives packets by patching `ServerConnector.OnEvent`.

For low-rate important messages, use `reliable: true`. For high-rate streams like voice or frequent position data, use `reliable: false`.

## Installer Integration

There are three installer paths.

### Built-In Server Plugins

Built-in server DLLs live in:

`TabgInstaller.Gui/plugins/`

The installer copies selected built-in DLLs into:

`<server>/BepInEx/plugins/`

The server plugin list is surfaced by `ServerModsViewModel.KnownServerPlugins`, and initial setup copies selected bundled plugins through `Installer.RunAsync(..., bundledPlugins: ...)`.

To add a new built-in server mod:

1. Add the plugin project to `TabgInstaller.sln`.
2. Build the DLL into or copy it to `TabgInstaller.Gui/plugins/`.
3. Add a manifest under `registry/plugins/<PluginId>/manifest.json` with `"type": "server"` and `"kind": "bundled"`.
4. Add its DLL to `ServerModsViewModel.KnownServerPlugins` if you want it in the direct server mods panel.
5. Add dependencies such as `"Citruslib"` if required.

### Built-In Client Plugins

Built-in client DLLs live in:

`TabgInstaller.Gui/client-plugins/`

The client installer creates a separate modded TABG copy, installs BepInEx, removes EasyAntiCheat from that copy, writes `steam_appid.txt`, and copies selected client DLLs into:

`<modded-client>/BepInEx/plugins/`

To add a new built-in client mod:

1. Add the plugin project to `TabgInstaller.sln`.
2. Build the DLL into or copy it to `TabgInstaller.Gui/client-plugins/`.
3. Add a manifest under `registry/plugins/<PluginId>/manifest.json` with `"type": "client"` and `"kind": "bundled"`.
4. Add its DLL to `ClientPanelViewModel.KnownClientMods` if you want it in the direct client mods panel.

### Bundled Plugin Manifests

The launcher no longer has a runtime plugin marketplace. A manifest under `registry/plugins/<id>/manifest.json` is release metadata for a plugin that is built or bundled in this repository.

Minimal server manifest:

```json
{
  "id": "MyPlugin",
  "name": "My Plugin",
  "version": "1.0.0",
  "description": "Does one focused thing.",
  "author": "YourName",
  "downloadUrl": "",
  "dllNames": ["MyPlugin.dll"],
  "type": "server",
  "kind": "bundled",
  "defaultChecked": false,
  "compatibleTabgVersions": ["*"],
  "minInstallerVersion": "5.0.0",
  "bepInExVersion": "5.4.22",
  "dependencies": []
}
```

For server mods that require a client component, use:

```json
"requiresClientMod": true,
"clientPluginId": "MyPluginClient"
```

and create a separate bundled client manifest with `"type": "client"`.

## Current Examples In This Repo

- `TabgInstaller.ProximityChat.Server` patches `ServerClient.HandleNetorkEvent`, consumes event `240`, and relays voice only to nearby players.
- `TabgInstaller.ProximityChat.Client` sends event `240` through `ServerConnector.Instance.SendMessageToServer` and patches `ServerConnector.OnEvent` to play relayed voice.
- `TabgInstaller.AdminRadar.Server` patches `BattleRoyaleGameMode.Run`, reads player positions from the server room, and broadcasts event `241`.
- `TabgInstaller.AdminRadar.Client` listens for event `241` and draws an IMGUI radar overlay.
- `TabgInstaller.HuntMode.Shared` shows the cleanest pattern for shared constants between server and client DLLs.

## Practical Workflow

1. Search decoded code with `rg` for the gameplay class or method you need.
2. Create a small BepInEx plugin project with only the required DLL references.
3. Prefer Harmony postfixes for observation and prefixes only when you intentionally replace or consume behavior.
4. Keep client and server logic separate unless the code is pure shared constants or serializers.
5. Build and copy the DLL into `TabgInstaller.Gui/plugins/` or `TabgInstaller.Gui/client-plugins/`.
6. Add or update the registry manifest.
7. Run the installer and verify BepInEx logs in `<game>/BepInEx/LogOutput.log`.

## Common Failure Points

- Wrong target framework: use `netstandard2.0` for plugins.
- Accidentally copying Unity, BepInEx, Harmony, or `Assembly-CSharp` dependency DLLs beside your plugin. Keep those references `Private=False`.
- Patching a client class in a server plugin or a server class in a client plugin.
- Reusing an event byte already used by TABG or another mod.
- Assuming private/public names from memory. Check the decoded source first because TABG has misspellings like `HandleNetorkEvent`.
- Installing a client-required server mod without also installing the client mod.
