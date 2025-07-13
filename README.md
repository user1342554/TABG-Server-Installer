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

*An installer, mod‑loader, and AI-powered configuration assistant for **Totally Accurate Battlegrounds** dedicated servers.*

---


## 🚀 Quick Start

### Option 1: Standard Installation

1. Download the latest **`TabgInstaller.zip`** from the [releases page](../../releases)
2. Extract and run **`TabgInstaller.exe`**
3. The installer will auto-detect your Steam library or you can browse manually
4. Enter your server details and select optional plugins
5. Click **Install** and wait for the process to complete
6. Configure your server using the visual editors or with the Ai 
7. Start your server from the console window


## AI Configuration Assistant

The installer includes an AI assistant that understands TABG configuration syntax and can modify your server settings through conversation.

### Supported Providers
- **OpenAI** (GPT-4, o3, ...)
- **Anthropic** (Claude 4, ...)
- **Google** (Gemini)
- **xAI** (Grok)
- **Local AI** (Free via Ollama with DeepSeek-R1, Qwen 2.5, Llama 3.2, etc.)

## Project Structure

| Project                             | Type            | Description                                                              |
| ----------------------------------- | --------------- | ------------------------------------------------------------------------ |
| **TabgInstaller.Core**              | Library         | Core installation logic, GitHub API, configuration management            |
| **TabgInstaller.Gui**               | WPF App         | Main installer UI with tabs for settings, presets, and AI chat          |
| **TabgInstaller.AntiCheatBypass**   | BepInEx Plugin  | Harmony patches to bypass EAC/EOS for dedicated servers                 |
| **TabgInstaller.WeaponSpawnConfig** | BepInEx Plugin  | Runtime weapon spawn rate configuration                                  |
| **TabgInstaller.StarterPack**       | BepInEx Plugin  | Essential server modifications (respawning, lobbies, etc.)              |
| **TabgInstaller.TestMod**           | BepInEx Plugin  | Example mod for developers                                               |
| **ConfigSanitizer**                 | Console App     | Fixes malformed JSON in configuration files                             |

---

## 📊 Installation Flow

```mermaid
flowchart TD
    A[Launch Installer] --> B{Detect Steam Library}
    B -->|Found| C[Select Server Directory]
    B -->|Not Found| D[Browse Manually]
    D --> C
    C --> E[Enter Server Details]
    E --> F[Validate Input with Word List]
    F --> G[Select Optional Plugins]
    G --> H[Begin Installation]
    
    H --> I[Clean Existing Files]
    I --> J[Install BepInEx 5.4.22]
    J --> K[Download & Install StarterPack]
    K --> L{Optional Plugins Selected?}
    
    L -->|CitrusLib| M1[Download CitrusLib]
    L -->|Weapon Config| M2[Install Weapon Config Mod]
    L -->|Anti-Cheat Bypass| M3[Install EAC/EOS Bypass]
    L -->|None| N
    
    M1 --> N
    M2 --> N
    M3 --> N[Configure StarterPack]
    
    N --> O[Generate game_settings.txt]
    O --> P[Open Configuration Window]
    P --> Q{Use AI Assistant?}
    
    Q -->|Yes| R[Setup AI Provider]
    R --> S[Configure via Chat]
    Q -->|No| T[Manual Configuration]
    
    S --> U[Start Server]
    T --> U
    U --> V[Server Running!]
```




## 🔧 Requirements

- Windows 10/11 (64-bit)
- .NET Framework 4.7.2 or higher
- Steam with TABG Dedicated Server installed
- (Optional) API key for AI features or ~4GB disk space for local AI


---

## 🙏 Credits

- **Landfall Games** - For creating TABG
- **BepInEx Team** - For the modding framework
- **CyrusTheLesser** - For CitrusLib
- **ContagiouslyStupid** - For the StarterPack


## 📄 License

Released under the **MIT License** – see [LICENSE](LICENSE) for the full text.
