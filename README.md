#### Like what you see? Buy me a Beer!
[![buy_me_a_beer](https://raw.githubusercontent.com/bbckr/assets/master/buymeabear.PNG)](https://www.buymeacoffee.com/bckr)

# uMod Plugins for Rust

A collection of [uMod](https://umod.org/) plugins for running on a modded [PlayRust](https://rust.facepunch.com/) dedicated server.

A guide is also included on how to run and test the plugins locally.

## Included plugins

- **StreamerFriendly** (1.0.0) A plugin that prevents third-party services from tracking players via Steam server queries
- **JuicedRcon** (1.0.0) A plugin that allows you to customize your RCON experience

# How to install

You must have a server with uMod installed to be able to run these plugins.

Simply drag and drop the files in the `./oxide/plugins/` folder in your server and they will be loaded automatically.

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

## Running tests locally against a server

Run the go tests included to see if the plugin is properly loaded and functional:

``` bash
# set the connect ip of the server before running
export CONNECT_IP=127.0.0.1:28015

go test ./...
```
