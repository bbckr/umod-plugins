using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Steamworks;

namespace Oxide.Plugins
{
    [Info("StreamerFriendly", "bbckr", "0.1.0")]
    [Description("A plugin that prevents external services from tracking players via Steam Queries.")]
    class StreamerFriendly : RustPlugin
    {
        private Anonymizer anonymizer = new Anonymizer();

        void Loaded()
        {
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

        }

        void Unload()
        {

        }

        private class Anonymizer
        {
            private const string DEFAULT_ANONYMIZED_NAME = "StreamerFriendly";
            private IDictionary<string, ServerPlayer> anonymizedPlayers = new Dictionary<string, ServerPlayer>();

            public void Anonymize(IPlayer player)
            {
                var serverPlayer = new ServerPlayer(player);
                SteamServer.UpdatePlayer(serverPlayer.steamId, DEFAULT_ANONYMIZED_NAME, 0);
                anonymizedPlayers.Add(player.Id, serverPlayer);
            }

            public void Deanonymize(IPlayer player)
            {

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
