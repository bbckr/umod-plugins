#### Like what you see? Buy me a Beer!
[![buy_me_a_beer](https://raw.githubusercontent.com/bbckr/assets/master/buymeabear.PNG)](https://www.buymeacoffee.com/bckr)

# uMod Plugins for Rust

The **Tower of Swole** collection of [uMod](https://umod.org/) plugins for running on a modded [PlayRust](https://rust.facepunch.com/) dedicated server.

A guide is also included on how to run and test the plugins locally.

## Included plugins

For more detailed documentation on how to install and use the plugins, check out the `docs` folder or click the plugin name below:

- **[StreamerFriendly](docs/StreamerFriendly.md)** (2.0.1) A plugin that prevents third-party services from tracking players via Steam server queries *(Official umod plugin page: [Streamer Friendly](https://umod.org/plugins/OeK1mVbKG4))*
- **[JuicedRcon](docs/JuicedRcon.md)** (1.1.0) A plugin for better, custom RCON experience *(Official umod plugin page: [Juiced Rcon](https://umod.org/plugins/MGKbWeegDa))*

## Need plugin support?

Head over to the **Tower of Swole** discord `#plugins` channel using [this invite link](https://discord.gg/a3hJBZG) and @ masq#5845.

# Local Development

## Prerequistes

- Visual Studio IDE
- Docker + docker-compose (for running a local instance of a PlayRust dedicated server to test the plugins)
- (Optional) Go 1.15 (for running the e2e tests against a local or remote server with the plugin installed)

## Running the server locally

You can test the plugins against a local server by running it in a container via docker-compose.

``` bash
# spinning up the server
docker-compose up

# tear down the server
docker-compose down

# tear down the server and volumes
docker-compose down -v
```

Any change you make to the files in `plugins/` should be picked up by server running in the container.

Make sure your Docker daemon is allocated enough RAM to properly start up the server (~6gbs).

## Enabling code completion

This requires you to open the project solution in Visual Studio.

``` bash
# if you haven't already ran docker-compose up, run the container
docker-compose up -d

# copy the dlls into your project directory
docker cp local-rust-server:/steamcmd/rust/RustDedicated_Data/Managed/ ./bin/

# if you are missing references to dlls you would like to use, go to
# the .csproj file and update what is missing from the bin/
```
