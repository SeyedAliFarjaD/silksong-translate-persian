@echo off
chcp 65001 >nul
echo ====================================================
echo    Silksong Persian Translation Pack Builder
echo ====================================================
echo.

echo [1/2] Building and installing SilksongPersian plugin...
dotnet build "%~dp0silksong-fa\mod\SilksongPersian\SilksongPersian.csproj" -c Release -v q --nologo
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to compile SilksongPersian.dll!
    goto :fail
)

if not exist "C:\GOG Games\Hollow Knight Silksong\BepInEx\plugins\SilksongPersian" mkdir "C:\GOG Games\Hollow Knight Silksong\BepInEx\plugins\SilksongPersian"
copy /y "%~dp0silksong-fa\mod\SilksongPersian\bin\Release\netstandard2.1\SilksongPersian.dll" "C:\GOG Games\Hollow Knight Silksong\BepInEx\plugins\SilksongPersian\SilksongPersian.dll" >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [WARNING] Game is running! SilksongPersian.dll could not be overwritten.
    echo           Close the game and run build.bat again to update the DLL.
) else (
    echo [OK] SilksongPersian.dll updated.
)

echo [2/2] Building translation pack...
dotnet run --project "%~dp0silksong-fa\mod\PackBuilder\PackBuilder.csproj"
if %ERRORLEVEL% EQU 0 (
    echo.
    echo ====================================================
    echo [SUCCESS] Translation pack and RTL patch applied!
    echo [OK] Baste tarjome va patch-e RTL ba movafaghiat nasb shod.
    echo ====================================================
    goto :end
)

:fail
echo.
echo ====================================================
echo [ERROR] Build failed!
echo ====================================================

:end
echo.
pause
