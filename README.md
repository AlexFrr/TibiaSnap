# TibiaSnap

Press your hotkey (default **End**) in Tibia → Tibia takes its own screenshot → TibiaSnap opens it fullscreen and starts the Windows snip tool so you can crop it.

TibiaSnap only checks whether your hotkey is pressed. It never blocks or sends keys and never touches the Tibia client.

## For friends
Download **TibiaSnap.exe** from the [latest release](https://github.com/AlexFrr/TibiaSnap/releases/latest), put it in a normal folder (not Program Files) and start it.
In Tibia, bind **Take Screenshot** to the same key as TibiaSnap (tray icon → *Change hotkey*).
It updates itself.

## Releasing a new version
1. In `TibiaSnap.cs`, raise `AssemblyVersion` (e.g. `1.3.0.0`) and add an entry at the top of `ChangeLog` – that's what people see in the *What's new* window.
2. Push to `main`.
3. GitHub builds the exe and publishes release `v1.3.0` by itself. Everyone's TibiaSnap updates within a few hours (or right away via tray → *Check for updates*).

Local build without GitHub: run `BUILD.bat`.
