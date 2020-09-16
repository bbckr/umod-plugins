using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Steamworks;

namespace Oxide.Plugins
{
    [Info("StreamerFriendly", "bbckr", "0.1.0")]
    [Description("A plugin that prevents external services from tracking players via Steam Queries.")]
    class StreamerFriendly : RustPlugin
    {
        private const String ANONYMIZED_NAME = "StreamerFriendly";

        void Loaded()
        {
            // Anonymize player info
            var activeBasePlayers = BasePlayer.activePlayerList;
            for (int i = 0; i < activeBasePlayers.Count; i++) {
                Anonymize(activeBasePlayers[i].IPlayer.Id);
            }
        }

        void OnUserConnected(IPlayer player)
        {
            // Anonymize player info
            Anonymize(player.Id);
        }

        private void Anonymize(string id) {
            var steamId = new SteamId();
            steamId.Value = Convert.ToUInt64(id);
            SteamServer.UpdatePlayer(steamId, ANONYMIZED_NAME, 0);
        }
    }
}
