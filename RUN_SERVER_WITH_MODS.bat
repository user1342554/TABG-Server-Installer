@echo off
title TABG Server with Mods
color 0A
echo ========================================
echo     TABG SERVER WITH BEPINEX MODS
echo ========================================
echo.

:: Check if running as admin
net session >nul 2>&1
if %errorLevel% == 0 (
    echo [OK] Running as Administrator
) else (
    echo [!] Requesting Administrator privileges...
    echo.
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit
)

:: Navigate to server directory
cd /d "C:\Program Files (x86)\Steam\steamapps\common\TotallyAccurateBattlegroundsDedicatedServer"

echo.
echo Current directory: %CD%
echo.
echo Checking BepInEx installation...
if exist "BepInEx\plugins\TabgInstaller.WeaponSpawnConfig.dll" (
    echo [OK] WeaponSpawnConfig mod found!
) else (
    echo [WARN] WeaponSpawnConfig mod not found
)

echo.
echo Starting TABG Server with BepInEx...
echo.
echo If BepInEx loads correctly, you should see:
echo - [Message: BepInEx] messages
echo - [WeaponSpawnConfig] plugin loading messages
echo.
echo ========================================
echo.

:: Set environment variables to ensure BepInEx loads
set DOORSTOP_ENABLE=TRUE
set DOORSTOP_INVOKE_DLL_PATH=BepInEx\core\BepInEx.Preloader.dll

:: Start the server
TABG.exe

pause 