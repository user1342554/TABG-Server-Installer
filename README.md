<div align="center">

<pre>
 _____                                                               _____ 
( ___ )-------------------------------------------------------------( ___ )
 |   |                                                               |   | 
 |   |  _____  _    ____   ____      ____                            |   | 
 |   | |_   _|/ \  | __ ) / ___|    / ___|  ___ _ ____   _____ _ __  |   | 
 |   |   | | / _ \ |  _ \| |  _     \___ \ / _ \ '__\ \ / / _ \ '__| |   | 
 |   |   | |/ ___ \| |_) | |_| |     ___) |  __/ |   \ V /  __/ |    |   | 
 |   |  _|_/_/   \_\____/ \____| _  |____/ \___|_|    \_/ \___|_|    |   | 
 |   |                                                               |   | 
  |   |           T A B G   S E R V E R   I N S T A L L E R           |   |  
 |___|                                                               |___| 
(_____)-------------------------------------------------------------(_____) 
</pre>

</div>




## Installation Flow

```mermaid
flowchart TD
    A[Launch App] --> B{Detect TABG Server via Steam}
    B -->|Found| C[Prefill Server Directory]
    B -->|Not Found| D[Paste Server Directory]
    C --> E[Click INSTALL]
    D --> E

    E --> F[Confirm: modifies files and will create backup]
    F -->|No| X1[Cancel]
    F -->|Yes| G{Path exists?}
    G -->|No| D
    G -->|Yes| H{Required fields present?}
    H -->|No| E
    H -->|Yes| I[Validate Name/Password/Description with word list]
    I -->|Invalid| E
    I -->|Valid| J{Server directory empty?}
    J -->|Yes| L[Skip backup]
    J -->|No| K[Create backup]
    K --> K1{Backup succeeded?}
    K1 -->|No| K2{Continue anyway?}
    K2 -->|No| X1
    K2 -->|Yes| M[Begin installation]
    K1 -->|Yes| M
    L --> M

    subgraph Installer core
        M --> N[Kill running TABG processes]
        N --> O[Ensure VanillaFiles whitelist - preserve Presets and backup]
        O --> P[Write game_settings.txt defaults]
        P --> Q[Hard reset files not in whitelist]
        Q --> R[Install BepInEx 5.4.22]
        R --> S[Enable doorstop_config.ini]
        S --> T[Copy winhttp.dll and version.dll]
        T --> U{Unity 2021.3+?}
        U -->|Yes| U1[Install automatic BepInEx loader]
        U -->|No| V
        U1 --> V
        V --> W[Download StarterPack.dll to plugins]
        W --> W1[Run TABG.exe headless until heartbeat or timeout]
        W1 --> W2[Download StarterPackSetup.exe to server root]
        W2 --> W3[Launch setup and wait for exit]
        W3 --> W4[Sanitize TheStarterPack.txt]
    end

    W4 --> Y1[Enable Config/AI/Backups tabs and switch to Config]
    Y1 --> Z[Installation complete]
```




## Requirements

- **Windows 10/11** (64-bit)
- **.NET 8.0 Desktop Runtime** (only if using the framework-dependent build in `publish/`)
- **Steam** with TABG Dedicated Server installed
- **(Optional)** API key or access token for AI features (OpenAI, Anthropic, xAI, Google Vertex)

---



## 🙏 Credits

- **Landfall Games** - For creating TABG
- **BepInEx Team** - For the modding framework
- **CyrusTheLesser** - For CitrusLib
- **ContagiouslyStupid** - For the StarterPack


## 📄 License

Released under the **MIT License** – see [LICENSE](LICENSE) for the full text.

---


## Key Storage and Knowledge Files

- Keys are stored per provider at `%LOCALAPPDATA%\TABGInstaller\keys`, encrypted via Windows DPAPI (Current User)
- The AI uses the `Knowledge` folder next to `TabgInstaller.Gui.exe` (`Game settings explanation.json`, `The starter pack explained.json`, `Weaponlist.json`)

## 🧰 Tool Calling (advanced)

The assistant can request config edits by emitting a line starting with `TOOL_CALL` followed by JSON:

```json
TOOL_CALL {"tool":"edit_tabg_config","target":"game_settings|starter_pack","ops":[{"type":"set","key":"KeyName","value":"NewValue"}]}
```

- `game_settings` -> `game_settings.txt`
- `starter_pack` -> `TheStarterPack.txt`
- Keys use `Key=Value` format; unknown keys are appended

Example:

```json
TOOL_CALL {"tool":"edit_tabg_config","target":"game_settings","ops":[{"type":"set","key":"MaxPlayers","value":"70"}]}
```


