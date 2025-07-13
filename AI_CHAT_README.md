# TABG Server Installer - AI Chat Feature

## Overview

The TABG Server Installer now includes an AI-powered configuration assistant that helps you set up and manage your TABG server through natural language conversations.

## Features

- **Multi-Provider Support**: Works with OpenAI (GPT-4, o3), Anthropic (Claude 4), Google (Gemini), xAI (Grok), or local AI via Ollama
- **Secure Key Storage**: API keys are encrypted using Windows DPAPI
- **Automatic Local AI Setup**: If you don't have an API key, the installer automatically sets up Ollama with DeepSeek-R1 8B (with tool calling support) for free local AI
- **Configuration Assistance**: The AI understands TABG configuration syntax and can modify game_settings.txt and datapack.txt
- **Tool Calling**: The AI can directly modify your server configuration files

## Getting Started

### Option 1: Using Cloud AI (Requires API Key)

1. Get an API key from one of the supported providers:
   - OpenAI: https://platform.openai.com
   - Anthropic: https://console.anthropic.com
   - Google: https://makersuite.google.com
   - xAI: https://x.ai/api

2. Run the bootstrap script:
   ```powershell
   .\bootstrap.ps1
   ```

3. Click the "AI Chat" button in the Configuration window

4. Select your provider and enter your API key when prompted

### Option 2: Using Local AI (Free, No API Key Required)

1. Run the bootstrap script:
   ```powershell
   .\bootstrap.ps1
   ```

2. Click the "AI Chat" button in the Configuration window

3. Select "Local (Ollama)" as the provider

4. The installer will automatically:
   - Download and install Ollama
   - Pull the DeepSeek-R1 8B with tools model (or choose from other models like Qwen 2.5, Llama 3.2, etc.)
   - Start the local AI server

## Usage Examples

Ask the AI assistant questions like:

- "Set the server name to 'My TABG Server' and max players to 50"
- "What are the available team modes?"
- "Enable respawn minigame and set countdown to 30 seconds"
- "Show me the current server configuration"
- "What weapons are available in the game?"

## Technical Details

### Architecture

The AI Chat feature consists of several components:

1. **Secure Key Storage**: Uses Windows DPAPI to encrypt API keys
2. **Unified Backend**: Abstracts different AI providers behind a common interface
3. **Prompt Builder**: Injects Knowledge folder content into system prompts
4. **Tool Executor**: Handles function calls to modify configuration files
5. **Config Patcher**: Applies changes to game_settings.txt and datapack.txt

### Available Functions

The AI has access to these functions:

- `modify_game_settings`: Change any setting in game_settings.txt
- `modify_datapack`: Modify datapack.txt configuration
- `get_game_setting`: Retrieve current value of a setting

### Knowledge Base

The AI is pre-loaded with knowledge about:

- Game settings and their valid values
- Starter pack configuration options
- Complete weapon list with IDs

## Troubleshooting

### Ollama Installation Issues

If Ollama installation fails:

1. Run PowerShell as Administrator
2. Manually download from https://ollama.com/download/OllamaSetup.exe
3. Install Ollama
4. Run `ollama pull Tr3cks/deepseek-r1-tool-calling:8b` in terminal (best for TABG) or choose another model

### API Key Validation Issues

If your API key is not validating:

1. Check your internet connection
2. Verify the key is correct (no extra spaces)
3. Ensure your API account has credits/access
4. Try using a different provider

### Performance

- Cloud AI providers typically respond in 1-3 seconds
- Local AI (Ollama) may take 5-30 seconds depending on your hardware
- GPU acceleration is automatically enabled for Ollama on supported hardware

## Security

- API keys are stored encrypted using Windows DPAPI
- Keys are only accessible to the current Windows user
- No keys are transmitted except to their respective AI providers
- Local AI (Ollama) runs entirely on your machine

## Contributing

To add support for additional AI providers:

1. Implement `IModelBackend` interface
2. Add provider configuration to models.json
3. Update `UnifiedBackend` to handle the new provider
4. Add validation logic in `ApiKeyDialog`

## License

This feature is part of the TABG Server Installer and follows the same license terms. 