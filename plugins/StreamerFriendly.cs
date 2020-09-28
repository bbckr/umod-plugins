// Requires: BetterRcon

using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Core.RemoteConsole;
using Steamworks;
using System;
using System.Collections.Generic;

/*
 * TODO
 * 
 * - Run RCON service on additional port
 * - Pass custom anonymize handler when sending message
 * 
 */

namespace Oxide.Plugins
{
    [Info("StreamerFriendly", "bbckr", "0.2.0")]
    [Description("A plugin that prevents external services from tracking players via Steam Queries and RCON.")]
    class StreamerFriendly : RustPlugin
    {
        [PluginReference]
        private Plugin BetterRcon;

        private Anonymizer anonymizer = new Anonymizer();
        private BetterRcon.BetterRemoteConsole rcon;

        void Loaded()
        {
            BetterRcon = (BetterRcon)Manager.GetPlugin("BetterRcon");

            // Anonymize rcon messages
            rcon = new BetterRcon.BetterRemoteConsole();
            rcon.Start();

            // Anonymize player info
            var activeBasePlayers = BasePlayer.activePlayerList;
            for (int i = 0; i < activeBasePlayers.Count; i++) {
                anonymizer.Anonymize(activeBasePlayers[i].IPlayer);
            }
        }

        object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            // TODO: (bckr) imitate console message
            rcon.SendMessage(new RemoteMessage { 
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
            if (BetterRcon != null)
            {
                // Stop rcon console
                rcon.Stop();
            }

            // Deanonymize player info
            var activeBasePlayers = BasePlayer.activePlayerList;
            for (int i = 0; i < activeBasePlayers.Count; i++)
            {
                anonymizer.Deanonymize(activeBasePlayers[i].IPlayer);
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
