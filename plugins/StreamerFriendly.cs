using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Steamworks;
using Facepunch;
using Network.Visibility;

namespace Oxide.Plugins
{
    [Info("StreamerFriendly", "bbckr", "0.2.0")]
    [Description("A plugin that prevents external services from tracking players via Steam Queries.")]
    class StreamerFriendly : RustPlugin
    {
        private static Anonymizer anonymizer = new Anonymizer();
        private static RconListener rconListener = new RconListener();

        void Loaded()
        {
            // Start listening to rcon
            rconListener.Subscribe();

            // Anonymize player info
            var activeBasePlayers = BasePlayer.activePlayerList;
            for (int i = 0; i < activeBasePlayers.Count; i++) {
                anonymizer.Anonymize(activeBasePlayers[i].IPlayer);
            }
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
            // Must unsubscribe OR ELSE
            rconListener.Unsubscribe();

            // Deanonymize player info
            var activeBasePlayers = BasePlayer.activePlayerList;
            for (int i = 0; i < activeBasePlayers.Count; i++)
            {
                anonymizer.Deanonymize(activeBasePlayers[i].IPlayer);
            }
        }

        private class RconListener
        {
            private Action<string, string, UnityEngine.LogType> CustomOnMessage;

            public RconListener()
            {
                CustomOnMessage = new Action<string, string, UnityEngine.LogType>(OnMessage);
            }

            public void Subscribe()
            {
                Output.OnMessage += CustomOnMessage;
            }

            public void Unsubscribe()
            {
                Output.OnMessage -= CustomOnMessage;
            }

            private void OnMessage(string arg0, string arg1, UnityEngine.LogType arg3)
            {

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
