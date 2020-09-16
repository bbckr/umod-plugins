# uMod Plugins for Rust

A collection of [uMod](https://umod.org/) plugins for running on a modded [PlayRust](https://rust.facepunch.com/) dedicated server.

# Running locally

## Prerequistes

- Docker + docker-compose (for running a local instance of a PlayRust dedicated server to test the plugins)
- Go 1.15 (for running the e2e tests against a local or remote server with the plugin installed)

## Running the server locally

You can test the plugins against a local server by running:

``` bash
# spinning up the server
docker-compose up

# tear down the server
docker-compose down

# tear down the server and volumes
docker-compose down -v
```

Make sure your Docker daemon is allocated enough RAM to properly start up the server (~6gbs).

## Running tests locally against a server

Run the go tests included to see if the plugin is properly loaded and functional:

``` bash
# set the connect ip of the server before running
export CONNECT_IP=127.0.0.1:28015

go test ./...
```
