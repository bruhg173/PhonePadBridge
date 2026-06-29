# Setup Guide

## Build or run

From:

```text
src\PhonePadBridge.WebBridge
```

Run from source:

```bat
run_from_source.bat
```

Build one EXE:

```bat
build_single_exe.bat
```

## Android setup

1. On Android, open:
   ```text
   Settings → About phone → Software information
   ```
2. Tap **Build number** 7 times.
3. Open:
   ```text
   Settings → Developer options
   ```
4. Enable **USB debugging**.
5. Plug the phone into the PC.
6. Accept the **Allow USB debugging** popup.

## Controller setup

Pair your controller to the Android phone over Bluetooth.

For a DualShock 4:

```text
Hold PS + Share until the light flashes
```

Then pair it in Android Bluetooth settings.

## Start bridge

1. Start the Windows app.
2. Click **Start Bridge**.
3. Click **Android USB Setup**.
4. Open this URL on Android:
   ```text
   http://127.0.0.1:49494
   ```
5. Tap **Start Bridge** on the phone page.
6. Press a controller button.

## Test

Open on Windows:

```text
Win + R
joy.cpl
```

Check for a virtual Xbox 360 controller.
