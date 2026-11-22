# TibiaSnap

Press your hotkey (default **End**) in Tibia → Tibia takes its own screenshot → TibiaSnap opens it fullscreen and starts the Windows snip tool so you can crop it.

TibiaSnap only checks whether your hotkey is pressed. It never blocks or sends keys and never touches the Tibia client.

## For friends
Download **TibiaSnap.exe** from the [latest release](https://github.com/AlexFrr/TibiaSnap/releases/latest), put it in a normal folder (not Program Files) and start it.
In Tibia, bind **Take Screenshot** to the same key as TibiaSnap (tray icon → *Change hotkey*).
It updates itself.

## Releasing a new version
1. Change `TibiaSnap.cs` and push it.
2. Tag it: `git tag v1.2.0` then `git push origin v1.2.0`
3. GitHub builds the exe and publishes the release. Everyone's TibiaSnap updates within a few hours (or right away via tray → *Check for updates*).

Local build without GitHub: run `BUILD.bat`.
