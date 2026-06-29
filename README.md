# PhonePad Bridge

PhonePad Bridge lets an Android phone act as a USB bridge between a Bluetooth controller and a Windows PC.

It was built for situations where a PC has **no Bluetooth** and **no Wi-Fi**, but you still want to use a wireless controller in PC games.

## How it works

```text
Controller
   ↓ Bluetooth
Android phone browser
   ↓ USB cable using ADB reverse
Windows app
   ↓ Virtual Xbox 360 controller
PC game
```

The Windows app hosts a local webpage. The phone opens the webpage, reads the controller through the browser Gamepad API, and sends controller state to the Windows app over WebSocket. The Windows app then updates a virtual Xbox 360 controller.

## Features

- No Xcode or iOS app required
- Android USB mode using ADB reverse
- Local web controller bridge
- Virtual Xbox 360 output
- Low-latency update path
- One-click Windows single-file EXE build script
- No accounts, no cloud service, no telemetry
- This works for basically any bluetooth controller that can connect to your phone

## Requirements

### Windows

- Windows 10/11
- .NET 8 SDK
- ViGEmBus installed for virtual Xbox controller output
- ViGEmBus probably wont need to be installed but if for some reason it doesnt work try downloading it and then if that doesnt fix it lmk
- Android Platform Tools / `adb.exe` for Android USB mode

### Android

- Bluetooth controller paired to the phone
- USB debugging enabled
- Chrome or another browser with Gamepad API support

## Quick start from source

Open:

```text
src\PhonePadBridge.WebBridge
```

Run:

```bat
run_from_source.bat
```

Or build one EXE:

```bat
build_single_exe.bat
```

The single-file EXE appears in:

```text
src\PhonePadBridge.WebBridge\bin\Release\net8.0-windows\win-x64\publish\
```

## Android USB mode

1. Enable Developer Options on Android.
2. Turn on USB debugging.
3. Plug the phone into the PC.
4. Start PhonePad Bridge.
5. Click **Start Bridge**.
6. Click **Android USB Setup**.
7. On Android, open:

```text
http://127.0.0.1:49494
```

8. Tap **Start Bridge** on the phone page.
9. Press a button on the controller.

## Test the virtual controller

On Windows:

```text
Win + R
joy.cpl
```

You should see a virtual Xbox 360 controller if ViGEmBus is installed and working.

## Privacy

PhonePad Bridge is designed to run locally.

- No analytics
- No accounts
- No cloud server
- No personal data collection
- No hardcoded personal paths or usernames
- No bundled logs
- No API keys or tokens

The app may display your local network IP, ADB device status, or phone connection events inside the local GUI while running. Do not upload screenshots/logs publicly if they show information you consider private.

## Security note

This app opens a local HTTP/WebSocket server on the selected port. Only run it on trusted networks. Android USB mode with `adb reverse` is recommended when you do not want to expose the webpage over Wi-Fi/LAN.

## License

MIT License. See [LICENSE](LICENSE).
