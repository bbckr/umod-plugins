using System;
using System.Collections.Generic;
using WebSocketSharp.Net.WebSockets;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.RemoteConsole;
using Steamworks;
using WebSocketSharp.Server;
using Newtonsoft.Json;
using WebSocketSharp;
using System.Net;

/*
 * TODO
 * 
 * - Check if RCON service already running (either abort or run on another port)
 * - Add more hooks
 * - Language variations, dictionary for hook messages
 * - Break out into seperate plugin and have streamer friendly use
 * 
 */

namespace Oxide.Plugins
{
    [Info("StreamerFriendly", "bbckr", "0.2.0")]
    [Description("A plugin that prevents external services from tracking players via Steam Queries and RCON.")]
    class StreamerFriendly : RustPlugin
    {
        private Anonymizer anonymizer = new Anonymizer();
        private AnonymizedRemoteConsole anonymizedRemoteConsole = new AnonymizedRemoteConsole();

        void Loaded()
        {
            // Anonymize rcon messages
            anonymizedRemoteConsole.Start();

            // Anonymize player info
            var activeBasePlayers = BasePlayer.activePlayerList;
            for (int i = 0; i < activeBasePlayers.Count; i++) {
                anonymizer.Anonymize(activeBasePlayers[i].IPlayer);
            }
        }

        object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            // TODO: (bckr) imitate console message
            anonymizedRemoteConsole.SendMessage(new RemoteMessage { 
                Message = "they died",
                Type = "Generic"
            });
            return null;
        }

        void OnUserConnected(IPlayer player)
        {
            // Anonymize player info
            anonymizer.Anonymize(player);
        }

        void OnUserDisconnected(IPlayer player)
        {
            // Stop tracking player in memory
            anonymizer.Remove(player);
        }

        void Unload()
        {
            // Stop rcon console
            anonymizedRemoteConsole.Stop();

            // Deanonymize player info
            var activeBasePlayers = BasePlayer.activePlayerList;
            for (int i = 0; i < activeBasePlayers.Count; i++)
            {
                anonymizer.Deanonymize(activeBasePlayers[i].IPlayer);
            }
        }

        private class AnonymizedRemoteConsole
        {
            private WebSocketServer server;
            private AnonymizedWebSocketBehavior behavior;

            private readonly int port;
            private readonly string password;

            public AnonymizedRemoteConsole()
            {
                port = Interface.Oxide.Config.Rcon.Port;
                password = Interface.Oxide.Config.Rcon.Password;
            }

            public void Start()
            {
                if (server != null && behavior != null)
                {
                    Interface.Oxide.LogWarning("rcon server already started");
                    return;
                }

                if (string.IsNullOrEmpty(password))
                {
                    Interface.Oxide.LogWarning("rcon server is unprotected: it is recommended a password is set");
                }

                try
                {
                    server = new WebSocketServer(port) { WaitTime = TimeSpan.FromSeconds(5.0), ReuseAddress = true };
                    server.AddWebSocketService(string.Format("/{0}", password), () => behavior = new AnonymizedWebSocketBehavior(this));

                    server.Start();
                }
                catch (Exception exception)
                {
                    Interface.Oxide.LogException($"rcon server failed to initialize", exception);
                    return;
                }

                Interface.Oxide.LogInfo("rcon server listening on {0}", server.Port);
            }

            public void Stop()
            {
                if (server != null && !server.IsListening)
                {
                    server.Stop();
                    server = null;
                    behavior = null;
                    Interface.Oxide.LogInfo($"rcon server has stopped");
                }
            }

            public void SendMessage(RemoteMessage message)
            {
                // TODO: (bckr) anonymize message here
                var serializedMessage = JsonConvert.SerializeObject(message, Formatting.Indented);
                server.WebSocketServices.Broadcast(serializedMessage);
            }

            private void OnMessage(MessageEventArgs e)
            {
                // TODO: (bckr) make MessageEventArgs to serialized remotemessage
                server.WebSocketServices.Broadcast(e.Data);
            }

            private class AnonymizedWebSocketBehavior : WebSocketBehavior
            {
                private readonly AnonymizedRemoteConsole Parent;
                private IPAddress _address;

                public AnonymizedWebSocketBehavior(AnonymizedRemoteConsole parent)
                {
                    Parent = parent;
                    IgnoreExtensions = true;
                }

                protected override void OnClose(CloseEventArgs e)
                {
                    Interface.Oxide.LogInfo("rcon connection {0} closed", _address);
                }

                protected override void OnError(ErrorEventArgs e)
                {
                    Interface.Oxide.LogException(string.Format("rcon exception: {0}", e.Message), e.Exception);
                }

                protected override void OnMessage(MessageEventArgs e)
                {
                    Parent?.OnMessage(e);
                }

                protected override void OnOpen()
                {
                    _address = Context.UserEndPoint.Address;
                    Interface.Oxide.LogInfo("rcon connection {0} established", _address);
                }
            }
        }

        private class Anonymizer
        {
            private const string DEFAULT_ANONYMIZED_NAME = "StreamerFriendly";
            private IDictionary<string, ServerPlayer> anonymizedPlayers = new Dictionary<string, ServerPlayer>();

            public void Anonymize(IPlayer player)
            {
                ServerPlayer serverPlayer;
                if (!anonymizedPlayers.TryGetValue(player.Id, out serverPlayer))
                {
                    serverPlayer = new ServerPlayer(player);
                }

                SteamServer.UpdatePlayer(serverPlayer.steamId, DEFAULT_ANONYMIZED_NAME, 0);
                anonymizedPlayers.Add(player.Id, serverPlayer);
            }

            public void Deanonymize(IPlayer player)
            {
                var serverPlayer = anonymizedPlayers[player.Id];
                SteamServer.UpdatePlayer(serverPlayer.steamId, serverPlayer.player.Name, 0);
            }

            public void Remove(IPlayer player)
            {
                anonymizedPlayers.Remove(player.Id);
            }
        }

        private class ServerPlayer
        {
            public SteamId steamId { get; }
            public IPlayer player { get; }

            public ServerPlayer(IPlayer player)
            {
                this.player = player;

                var steamId = new SteamId();
                steamId.Value = Convert.ToUInt64(player.Id);
                this.steamId = steamId;
            }
        }
    }
}
