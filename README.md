# Portal Gun for Quest

The Quest version of caleb's Portal Gun and Portal Doorways for A Township Tale. It lets a Quest player use the portal gun and the linked doorways on a server that runs Portal Gun, side by side with PC players.

Ported with caleb's permission. The original mod, its art and its design are his: https://github.com/calebv2/Portal-Gun

## What you need

- A Quest with LemonLoader (the Township Tale Quest kit).
- A server that runs Portal Gun 0.2.5 with SyncLib. Nothing changes on the server: it keeps caleb's `PortalGun.dll` and `SyncLib.dll`.
- PC players on the same server keep caleb's PC client and SyncLib, as before.

## Install

Close the game. Put `PortalGunQuest.dll` in the headset's Mods folder:

```
/sdcard/Android/data/com.Alta.ATownshipTale/files/MelonLoader/com.Alta.ATownshipTale/Mods/
```

Start the game and join a server that runs Portal Gun. On any other server the mod does nothing.

## Use

- `PortalGun`: hold it and pull the trigger. The first shot opens a blue portal, the second an orange one. Walk or drop things through.
- `PortalDoorA` and `PortalDoorB`: place both upright, at least 2.5 metres apart. They link on their own.

Spawning works as on PC, for example `spawn "PlayerName" PortalGun`.

## Differences from the PC version

- The view through a portal is drawn once for both eyes and at a lower resolution, to keep the Quest at full frame rate. It is correct from your head's position but has no depth between the eyes.
- A portal view is drawn only when it is in front of you and within 30 metres.
- Your arm is not drawn on the far side when you reach into a portal.
- Everything else follows the server, which is caleb's.

## Building

`build.ps1` bakes the models from caleb's Unity project (`tools/bake.py`), then builds the Quest DLL against the LemonLoader proxies of the game. Paths at the top of the script.
