# AnyPortal v1.0.0.0
## A BepInEx Valheim mod
## By sweetgiorni
### https://github.com/sweetgiorni/AnyPortal

### Replaces the default portal-pairing mechanism with a dropdown to select your destination portal.

This mod overhauls the portal pairing system by allowing you to select and travel to any existing portal, regardless of tag.

Destination selection
When you interact with a portal, you'll see a new dropdown under the tag input text box. Click on the dropdown and select your desired destination. The list elements include the tag name of the destination portals and their distances from the player.

Show destination on map
Don't recognize a portal listed in the dropdown? Just select it and click the "Map" button to be shown where the portal is on the map.


Installation (Read carefully!!!)

1. Install [BepInEx](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/)
2. Copy the folder "AnyPortal" to the BepInEx plugins directory. Copy the entire folder, not just the dll. plugin expects the AsetBundle file to be present at Valheim/BepInEx/plugins/AnyPortal/anyportal. See sample directory structure below

Valheim/
├─ BepInEx/
│  ├─ plugins/
│  │  ├─ AnyPortal/
│  │  │  ├─ anyportal
│  │  │  ├─ AnyPortal.dll



Multiplayer
The mod works in multiplayer, but there is a caveat: it must be installed on the server and on the game client of every player connected to the server. This is because every client runs a background thread that matches up portal tags every 5 seconds (Game::ConnectPortals()). All it takes is one person without the mod installed to connect to the server and the portals will stop working. If you notice that you're able to select a portal from the dropdown and the portal connects and lights up, only to disconnect a few seconds later, that means either the server or one of the players connected to the server is missing the mod.


## Build
Build the solution with Visual Studio 2019. You will need to add references to the Valheim, BepInEx, and unstripped Unity assemblies.

### Asset Bundle
The plugin also relies an a Unity AssetBundle file named "anyportal". This file must be located in BepInEx/plugins/AnyPortal/. The AssetBundle contains the prefab for the UI elements the mod uses. At some point I will post the Unity project so as to complete this repository.