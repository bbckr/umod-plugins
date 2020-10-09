# JuicedRcon

A plugin for better, custom RCON experience using websockets.

## How to install

- Drop the `JuicedRcon.cs` file in the `./oxide/plugins/` directory
- Ensure an RCON port + password is set in your `server.cfg`
- Disable `rcon.web` in your `server.cfg` by setting it to 0 (or else your server will fail to start)
- Restart the server after installing for the first time

Once it is running, connect to RCON as you normally would. If you set up custom RCON profiles, connect using the password you set for the profile.

## Features

### RCON profiles
- Create profiles with specific passwords
- Grant profiles access to specific commands
- Set display names for messaging across rcon sessions

### Improved broadcasting
- Using `say` appears realtime across all RCON sessions with display names (in-game, these will still appear as `SERVER`)

### Talk privately across all RCON sessions
- Use `juicedrcon.say` to broadcast a message to all active RCON sessions (does not display in game)

## Upcoming features

### Audit logging
- Commands are recorded according to the RCON profile + IP executing the commands

## Console commands

### Permission: `juicedrcon.admin`
#### Plugin
- `juicedrcon.enable` Enables the plugin and starts the RCON server (by default the plugin is enabled)
- `juicedrcon.disable` Disables the plugin and stops the RCON server

#### Profiles
- `juicedrcon.profile list` Lists all RCON profiles
- `juicedrcon.profile create <profile name>` Creates a new RCON profile
- `juicedrcon.profile <profile name> get` Gets the details of the RCON profile
- `juicedrcon.profile <profile name> delete` Deletes an existing RCON profile
- `juicedrcon.profile <profile name> enable` Enables the profile in the RCON server (by default they are disabled)
- `juicedrcon.profile <profile name> disable` Disables the profile in the RCON server and terminates all existing connections
- `juicedrcon.profile <profile name> set displayname <display name>` Sets a new display name for the profile (default is the profile name)
- `juicedrcon.profile <profile name> set password <password>` Sets a new password for the profile (default is empty, is required for the profile to be enabled by the RCON server)
- `juicedrcon.profile <profile name> add <command>` Grants a profile access to a command, can use wildcards (e.g. oxide.*)
- `juicedrcon.profile <profile name> remove <command>` Revokes access to a command

## Chat commands

None
