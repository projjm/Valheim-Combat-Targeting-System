# Valheim Combat Targeting System Mod

### About
This mod implements a soft-lock targeting system during combat, similar to games such as Witcher 3.
For those of you unaware, soft-locking is an automatic mechanic where the camera will attempt to focus towards the current target, the character is free to move in any direction but any attacks or actions will be directed towards the current target. This mod will intelligently select the current target depending on a number of factors, the current target will change dynamically during combat, or alternatively a target can be locked using a configurable key.

Valheim does not have any native targeting system, this was a design choice and means the player is responsible for camera control and attack direction. This is not bad by any means, and if you're happy with the current style of combat then this mod probably isn't for you. This mod aims to make combat less rigid, allowing you to focus more on the style of your combat rather than accuracy.

### Features
* Current target is dynamically updated using configurable factors.
* Character attacks will always be towards the current target.
* Current target can be locked to stop it from changing (Default key: Z)
* Camera focussing is dynamic and reacts to certain events (Dodging, Running etc).
* Camera focussing is temporarily disabled whilst running/sprinting.
* Camera focussing will only be in effect at certain ranges.
* Hides the crosshair during combat (unless a tool is equipped).
* All factors and weights can be configured to your preference

### Installation
You must have BepinEx installed before attempting to install this mod.
Move the .dll file into your Valheim\BepInEx\plugins folder.

### Config
In order to configure this mod, navigate to the Valheim\BepInEx\config folder and edit the values stored in projjm.combattargetingsystem.cfg. There are lots of configurations but the most influential are the Targeting Weights. These weights determine how much a given factor will influence the target selection process.

There are currently 6 factors:
* The enemy's distance from the player
* The enemy's angular distance from the camera
* The enemy's angular distance from the player
* The enemy's forward facing angular distance from the local player (is the enemy facing the player)
* Has the enemy been recently attacked by the player
* Has the enemy been damaged

If you find that the targeting feels a little off for your playstyle, I encourage you to experiment with these values.

### Feedback
This mod is still in the very early stages and I appreciate any feedback that you might have. If you encounter a bug please report it whenever you can so that I can fix it in the following update. Also, if you've discovered a configuration that works really well for you then I encourage you to share it with others.
In terms of mod compatibility, I have not tested many mods other than the ones I use regularly. If you discover an incompatibility then depending on the difficulty I will attempt to make it compatible. Thanks!

# Changelog
        Version 1.1.4
            Fixed Key Hints HUD flickering during combat and mouse flickering in inventory
            Fixed character's head not rotating towards target
            Fixed sprint issue with gamepad and camera focus
        Version 1.1.3
            Fixed major bug - When using a gamepad, attacks would not rotate towards target when standing still.
        Version 1.1.2
            Fixed bug where certain boss enemies (i.e. roots from The Elder) could not be targeted.
            Fixed bug where other player's nameplate HUDs would be transparent.
        Version 1.1.1
            Small bug prevention fixes - some patches continued to run when disabled.
            Critical bug fix - Removed an attack rotation bug that caused the player to be unable to hit characters.
        Version 1.1.0
            Added new camera focus variables for when the player is inside a dungeon (configurable)
            Fixed main patches running even when the mod is disabled.
            Added extra configs (Disable camera focus via keybind, synced with config)
            Added gamepad support for locking target (double tap the Interact button by default, configurable)
        Version 1.0.0
            Combat Targeting System initial release
            
Nexus Link: https://www.nexusmods.com/valheim/mods/1197
