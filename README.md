![](http://i.imgur.com/UgN4uSs.png)
![](http://i.imgur.com/i0hzUwu.png)

This is the final open source release of the [D2Moddin](http://paral.in/the-d2moddin-project/) client addon manager and launcher, and server network code.

## The Manager
The manager is responsible for connecting to D2Moddin, checking the SteamIDs of the active users on the machine, detecting the versions of downloaded addons and, when requested, downloading and decompressing new addons. It runs without admin privileges of any kind and is as small and ephemeral as possible.

Addons are downloaded to the dota directory / D2Moddin folder. When the user joins a lobby the "selected" mod files are copied into dota directory / addons / d2moddin. The process is non-invasive and easily uninstalled (delete both D2Moddin directories)

The manager is automatically told to shut down a minute after all open browser tabs with the user logged in are closed, or 30 seconds after connection is lost. 

The taskbar icon allows the user to see all downloaded addons, uninstall the manager and all installed addons, or restart/exit the manager.

## The Launcher (Installer)
The launcher simply downloads the latest version of the D2Moddin manager. It is a very light executable that targets all versions of Windows. 

The executable is as simple as possible, and follows this task list:

1. Compare the existing D2Moddin manager version with the latest version (found at the clientver page).
2. If the latest version is already downloaded, simply launch it.
3. If an update is required (or it has never been run before), download it and extract the latest version to `%AppData%/D2MP/`, a temporary directory.

Finally before exiting the executable attempts to delete itself.

## Touched Directories
Here is a list of the only directories both executables touch through their entire lifetimes. 

- The manager directory (temporary directory): `%AppData%/D2MP`
- The Dota 2 d2moddin directory: `%DotaDir%/dota/d2moddin%`
- The Dota 2 addon directory: `%DotaDir%/addons/d2moddin%`

Please note that files are never deleted or modified in your regular Dota 2 installation. The Dota 2 client loads addons as if they were Frostivus or Nian style game modes.

## Network Layout

The system uses XSockets 3.0. We were working on an XSockets 4 implementation when the service was shut down.

Full Writeup
============

I have a writeup on everything you could possibly want to know about the project on [my blog](http://paral.in/the-d2moddin-project/).
