@echo off
echo ========================================
echo Checking for BepInEx Installation
echo ========================================
echo.

echo Checking common TABG server locations...
echo.

set FOUND=0

REM Check current directory
if exist "BepInEx\core\BepInEx.dll" (
    echo [FOUND] BepInEx in current directory: %CD%
    set FOUND=1
    dir BepInEx\plugins\*.dll 2>nul
)

REM Check Steam dedicated server location
if exist "C:\steamcmd\steamapps\common\TotallyAccurateBattlegroundsServer\BepInEx\core\BepInEx.dll" (
    echo [FOUND] BepInEx in Steam server directory
    set FOUND=1
    dir "C:\steamcmd\steamapps\common\TotallyAccurateBattlegroundsServer\BepInEx\plugins\*.dll" 2>nul
)

REM Check Program Files
if exist "C:\Program Files (x86)\Steam\steamapps\common\TotallyAccurateBattlegroundsServer\BepInEx\core\BepInEx.dll" (
    echo [FOUND] BepInEx in Program Files Steam directory
    set FOUND=1
    dir "C:\Program Files (x86)\Steam\steamapps\common\TotallyAccurateBattlegroundsServer\BepInEx\plugins\*.dll" 2>nul
)

REM Check user's Downloads
if exist "%USERPROFILE%\Downloads\TotallyAccurateBattlegroundsServer\BepInEx\core\BepInEx.dll" (
    echo [FOUND] BepInEx in Downloads directory
    set FOUND=1
    dir "%USERPROFILE%\Downloads\TotallyAccurateBattlegroundsServer\BepInEx\plugins\*.dll" 2>nul
)

if %FOUND%==0 (
    echo.
    echo [NOT FOUND] BepInEx is not installed in any common location!
    echo.
    echo To use the Weapon Spawn Config mod, you need:
    echo 1. BepInEx installed in your TABG server folder
    echo 2. The server must be started with BepInEx
    echo.
    echo Your server is running WITHOUT BepInEx, which is why the mod doesn't work.
)

echo.
echo ========================================
echo Checking for doorstop_config.ini (BepInEx loader)
echo ========================================

if exist "doorstop_config.ini" (
    echo [FOUND] doorstop_config.ini in current directory
    type doorstop_config.ini
) else (
    echo [NOT FOUND] doorstop_config.ini - This file is needed for BepInEx to load!
)

echo.
echo ========================================
echo To fix this issue:
echo ========================================
echo 1. Make sure BepInEx is installed in your TABG server folder
echo 2. You should have these files in your server root:
echo    - doorstop_config.ini
echo    - winhttp.dll (for Windows servers)
echo 3. The BepInEx folder structure should be:
echo    - BepInEx\
echo      - config\
echo      - plugins\
echo      - core\
echo.
pause 