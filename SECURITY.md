# Security

## Local server

PhonePad Bridge opens a local HTTP/WebSocket server on the selected port.

Recommended use:

- Use Android USB mode with `adb reverse`.
- Only use LAN/Wi-Fi mode on a trusted network.
- Close the app when you are done.

## ADB

Android USB mode uses `adb reverse` to forward a port from the Android phone to the PC.

Only enable USB debugging on phones you control, and only authorize computers you trust.

## Reporting issues

If this project is published on GitHub, use GitHub Issues for bugs. Do not post private logs or screenshots unless you have removed information you consider sensitive.
