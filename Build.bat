@echo off
setlocal
cd /d "%~dp0"
title Building AltDeck

echo.
echo  ==============================================
echo    AltDeck - one-click build
echo  ==============================================
echo.

dotnet --list-sdks 2>nul | findstr /r "^1[0-9]\." >nul
if errorlevel 1 goto :nosdk

echo  Building... the first build downloads a few packages and can take a minute.
echo.
dotnet publish "src\AltDeck.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o "build"
if errorlevel 1 goto :failed

copy /y "build\AltDeck.exe" "AltDeck.exe" >nul
if errorlevel 1 goto :failed

echo.
echo  Done! AltDeck.exe is ready in this folder.
echo  You can move it anywhere, or right-click it and create a desktop shortcut.
echo.
choice /c YN /m " Start it now"
if errorlevel 2 goto :end
start "" "%~dp0AltDeck.exe"
goto :end

:nosdk
echo  The .NET 10 SDK (or newer) is needed to build the app. This is a one-time install.
echo  Opening the download page now - pick the "SDK x64" installer for Windows.
echo  When it's installed, double-click Build.bat again.
start "" "https://dotnet.microsoft.com/download/dotnet/10.0"
echo.
pause
exit /b 1

:failed
echo.
echo  Build failed - see the messages above.
echo.
pause
exit /b 1

:end
endlocal
