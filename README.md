## About TAE Editor DX:
* Edits the TimeAct Editor files of Dark Souls 1. These files control **everything** that happens on a specific frame of an animation, such as:
  * i-frames
  * parry windows
  * applying an "SpEffect" (special temporary statuses such as ring effects, poisoning, buffs, AI triggers, etc)
  * allowing animation cancelling
  * forcing death
  * creating "SFX" (particle effects)
  * playing sound effects such as footsteps, sword swooshes, etc.
  * doing an attack behavior (does damage to opponent, drains stamina from player, etc all in one event)
  * doing a "bullet" (projectile) behavior (fires projectile, drains stamina from player, etc all in one event)
  * doing a "common" behavior (like attack behaviors but for simpler things such as falling on someone's head causing stagger)
  * creating motion blur on weapon swings
  * setting the opacity of a character (used for getting summoned into other worlds, teleportation, etc)
  * setting attack aim tracking speed of a character
  * shaking the camera (e.g. Smough's footsteps)
  * playing additional animation layers (e.g. all of Gwyn's animations have events to play his clothes-blowing-in-wind animation layer)
  * **many more that we haven't even figured out yet!**
* ***Has all events mapped so that every single ingame file is openable without errors!***
* Shows a visual representation of event start/end times.
* Snaps to 30-fps increments just like the vanilla files do.
* Allows you to add new events to animations by right-clicking anywhere.
* Allows you to delete events by highlighting them and pressing the Delete key.
* Allows you to modify the parameters passed to each event (click an event to highlight it, then the parameters appear in the pane on the right side of the window)
* **Has full undo/redo functionality with Ctrl+Z/Ctrl+Y**
* **Has full copy/paste functionality**:
  * **Ctrl+C**: Copy
  * **Ctrl+V**: Paste at Mouse Cursor
  * **Ctrl+Shift+V**: Paste In-Place (keeps original location where you copied them from, useful for copying between animations)
* Edits the .anibnd or .anibnd.dcx files of the games directly. **No need to use BND rebuilders.**

## User Instructions:
  1. Download and extract [the latest version](https://github.com/Meowmaritus/TAE-DX/releases/download/v2.0/TAE.Editor.DX.v2.0.zip)
  1. Run `TAE Editor DX.exe`
  1. Go to File -> Open
  1. Load a .anibnd (PTDE) or .anibnd.dcx (Remastered). [There's a dropdown to change which type of file is shown.](https://lh3.googleusercontent.com/-006qBXK_PwY/W-2OWtSfmrI/AAAAAAAAAmg/9mI1fiRrnFM98Os-skSmn7s3ei8ry2JEQCHMYCw/s0/TAE%2BEditor%2BDX_2018-11-15_09-18-50.png)
  1. Select an animation ID on the left pane
  1. Drag some events around or otherwise mess with things (try the right pane for editing the highlighted event)
  1. Hit Ctrl+S to save. If the `File -> Force Refresh On Save` option is enabled and your game window is open, the character's files will immediately reload ingame and you can focus the game menu and test the TAE event changes. Additionally, you can press F5 or click the `File -> Force Refresh Ingame` option to force the character to reload.
  
## Developer Instructions:
  1. Install [MonoGame for Visual Studio - Development Build](http://teamcity.monogame.net/repository/download/MonoGame_PackagingWindows/latest.lastSuccessful/MonoGameSetup.exe?guest=1)
  1. Clone the TAE-DX repository
  1. Update the MeowDSIO submodule (do `git submodule update --init --recursive` from within TAE-DX directory)
  1. Open the TAEDX.sln file in Visual Studio 2017
  1. Go to Tools -> NuGet Package Manager -> Package Manager Console
  1. Paste `Update-Package -reinstall` into the console and hit enter.
  1. Wait for VS to reinstall all packages
  1. Build Solution should succeed now.

## System Requirements:
* Prepare to Die Edition **Only** (not required for remastered edition): Game unpacked with [UnpackDarkSoulsForModding by HotPocketRemix](https://www.nexusmods.com/darksouls/mods/1304/)
* Windows 7/8/8.1/10 (32-bit and 64-bit both work)
* [Microsoft .NET Framework 4.7.2](https://www.microsoft.com/net/download/thank-you/net472)
* A DirectX 9 Compatible Graphics Device (this is important because this application is **Direct3D-accelerated**)

## Special Thanks
* River Nyxx - General .TAE file structure.
* RavagerChris - Parameters of some of the events.

## Libraries Utilized
* [MeowDSIO](https://github.com/Meowmaritus/MeowDSIO) by myself
* [Newtonsoft Json.NET](https://www.newtonsoft.com/json)
* [MonoGame Framework](http://www.monogame.net/)
