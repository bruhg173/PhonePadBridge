@echo off
setlocal EnableExtensions EnableDelayedExpansion
title PhonePad Bridge - Build Single EXE
cd /d "%~dp0"

echo.
echo Building PhonePad Bridge as one .exe...
echo.

call :EnsureDotnet8Sdk
if errorlevel 1 goto fail_dotnet

echo.
echo Using dotnet:
dotnet --info

echo.
echo SDK selected for this project:
dotnet --version

echo.
echo Restoring packages...
dotnet restore
if errorlevel 1 goto fail

echo.
echo Publishing single-file EXE...
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true
if errorlevel 1 goto fail

echo.
echo DONE.
echo Your exe is here:
echo.
echo %cd%\bin\Release\net8.0-windows\win-x64\publish\PhonePadBridge.WebBridge.exe
echo.
echo Note: You still need ViGEmBus installed for the virtual Xbox controller.
echo For Android USB mode, adb.exe must be installed or placed next to the final exe.
echo.
pause
exit /b 0

:EnsureDotnet8Sdk
echo Checking for .NET 8 SDK...

set "LOCAL_DOTNET=%LOCALAPPDATA%\Microsoft\dotnet"
set "PATH=%LOCAL_DOTNET%;%LOCAL_DOTNET%\tools;%PATH%"

call :HasDotnet8Sdk
if not errorlevel 1 (
    echo .NET 8 SDK found.
    exit /b 0
)

echo .NET 8 SDK was not found.
echo Attempting automatic install...
echo.

where winget >nul 2>nul
if not errorlevel 1 (
    echo Trying winget install: Microsoft.DotNet.SDK.8
    winget install --id Microsoft.DotNet.SDK.8 -e --source winget --accept-source-agreements --accept-package-agreements
    call :HasDotnet8Sdk
    if not errorlevel 1 (
        echo .NET 8 SDK installed successfully with winget.
        exit /b 0
    )
    echo winget install did not finish successfully. Trying Microsoft dotnet-install script...
) else (
    echo winget was not found. Trying Microsoft dotnet-install script...
)

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ErrorActionPreference='Stop';" ^
  "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12;" ^
  "$installDir=Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet';" ^
  "New-Item -ItemType Directory -Force -Path $installDir | Out-Null;" ^
  "$script=Join-Path $env:TEMP 'dotnet-install.ps1';" ^
  "Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $script;" ^
  "& $script -Channel 8.0 -InstallDir $installDir;" ^
  "Write-Host 'Installed to' $installDir;"

set "PATH=%LOCAL_DOTNET%;%LOCAL_DOTNET%\tools;%PATH%"
call :HasDotnet8Sdk
if not errorlevel 1 (
    echo .NET 8 SDK installed successfully with dotnet-install.
    exit /b 0
)

echo Automatic .NET 8 SDK install failed.
exit /b 1

:HasDotnet8Sdk
where dotnet >nul 2>nul
if errorlevel 1 exit /b 1

for /f "tokens=1" %%v in ('dotnet --list-sdks 2^>nul') do (
    echo %%v | findstr /R "^8\." >nul
    if not errorlevel 1 exit /b 0
)

exit /b 1

:fail_dotnet
echo.
echo Could not find or install the .NET 8 SDK.
echo You can install it manually from Microsoft's official .NET 8 download page.
echo.
pause
exit /b 1

:fail
echo.
echo Build failed.
echo Send me the red error text and I will patch it.
echo.
pause
exit /b 1
