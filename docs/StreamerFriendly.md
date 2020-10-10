# StreamerFriendly

A plugin that prevents external services from tracking players via Steam Server Queries.

## How it works

Server information is publically expose by Steam through their [Server Queries](https://developer.valvesoftware.com/wiki/Server_queries).

This plugin works by anonymizing the player names to the same name so that external services, such as BattleMetrics, are not able to track the players on your servers.

This is particularly useful for preventing players from abusing those services to stalk, harass, or stream-snipe other players on the server.

## Features

- Player names are randomized in Steam Server Queries
- Randomized names can be configured through commands
- Enable and disable the plugin through commands

## Console commands

### Permission `streamerfriendly.admin`

#### Plugin
- `anonymize.enable` Enables the plugin
- `anonymize.disable` Disables the plugin

#### Random Name Generator
- `anonymize.random add-left <word>` Adds a word to the list that is randomly choosen for the left side of the name
- `anonymize.random add-right <word>` Adds a word to the list that is randomly choosen for the right side of the name
- `anonymize.random remove-left <word>` Removes a word from the list that is randomly choosen for the left side of the name
- `anonymize.random remove-right <word>` Removes a word from the list that is randomly choosen for the right side of the name

## Chat commands

None
