# CoJ2 Controller

> **Modified fork.** This is a fork of [victormvy/CoJ2-Controller](https://github.com/victormvy/CoJ2-Controller),
> modified by Roozbeh Ghazavi (2026). See [Fork changes](#fork-changes) below for the list of modifications.
> Distributed under the same GNU GPL v3 license as the original — see [LICENSE.md](LICENSE.md).

## What is CoJ2 Controller?
CoJ2 Controller is a software that was created in 2013 for the game Call of Juarez: Bound in Blood Multiplayer and includes some functionalities to improve dedicated servers management.

## Features
- Modded classes creation, including weapons configuration for each of the classes.
- Dedicated server configuration through an easy GUI instead of the ordinary INI files configuration.
- Dedicated server real-time management, including:
    - Automated server messages.
    - Real-time players list and ability to kill, kick or BAN any of them with two clicks.
    - List of banned players for easy unban.
    - List of players grouped by teams.
    - TEAMS AUTO-BALANCE. When teams are unbalanced, the last player who joined the team with more players will be automatically killed / kicked.
    - Custom admin functionalities (apart from standard CoJ2 server admins). Includes new commands to manage the different options described above.
    - Old logs viewing. Logs are stored and can be shown within the dedicated server management window. Logs can be filtered by words.
    - Map rotation viewing and ability to remember last map played when server is restarted.
    - Ability to add a list of forbidden words. Players who say these words will be punished with kill / kick / ban.
- Automatic custom maps download (deprecated feature as cojmodding.com is dead).

## How does CoJ2 Controller work?
As, CoJ2 did not include any dedicated server communication interface, the communication from the server to CoJ2 Controller is made through the log files that the dedicated server generates. Every line of these logs is processed by CoJ2 Controller and, in this way, a state of the game is manually generated (including current gamemode, map, list of players, team of players...).
On the other hand, the communication between CoJ2 Controller and the server is made by directly sending commands to the server console.

## Installation
CoJ2 Controller was developed under Windows with Visual Basic .Net using Visual Studio 2012.
### Dependencies
- .Net Framework 4.0.
- CoJ2 Dedicated Server.
- CoJ2 Patch v1.1, required for dedicated servers to run.

### Development environment
- Visual Studio 2012 (it works with Visual Studio 2017 but some project settings are bugged).

## Binaries download
You can directly download the installer from [Dropbox](https://www.dropbox.com/s/t6jsyuupg6ls0iw/setup_ModsCoJ.zip?dl=1).
Dependencies are automatically installed along with CoJ2 Controller.

## Demo videos
Some videos were uploaded where most of the functionalities are shown.
- [Classes modding](https://youtu.be/do5j__FL060).
- [Server config and console](https://youtu.be/-q1qUkwV0EE).

## Current support
CoJ2 Controller was abandoned when CoJ2 official master servers stopped working and multiplayer officially died. Its source code has been released now as some people are still playing the game through virtual networks and one these players requested me to release our software source code publicly. **However, no longer support will be given**.

## Disclaimer
Most of the source code and comments are in Spanish. Software design and structure is not the best as it was developed in 2013 (at an early age) when I had small knowledge about software design and development.

## Fork changes
This fork modifies the original CoJ2 Controller. Changes by Roozbeh Ghazavi (2026):
- Added the `!mapgo <map> <mode>` admin command, which jumps the rotation directly to a chosen
  map/mode. It resolves the target index from `DedicatedServerSettings.ini`, writes it to the
  running dedicated server's current-map-index counter via `WriteProcessMemory`, verifies the
  write, then issues `/mapnext` to load it. See [`CoJ/MapGo.vb`](CoJ/MapGo.vb).

## License
CoJ2 Controller source code was released in 2020 under the GNU GPL v3 license, and this fork
remains under the same license. Check [LICENSE.md](LICENSE.md) for a full copy of the license.

Modifications in this fork are Copyright (C) 2026 Roozbeh Ghazavi; the original work is
Copyright (C) 2013-2020 S!lv3r and Tr!tu. This is a modified version — problems introduced here
should not be attributed to the original authors.

## Contributors
- S!lv3r
- Víctor Manuel "Tr!tu" Vargas ([@victormvy](https://github.com/victormvy)) — original author
- Roozbeh Ghazavi — fork maintainer (`!mapgo` command and related changes)
