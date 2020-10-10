using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Steamworks;

namespace Oxide.Plugins
{
    [Info("StreamerFriendly", "bbckr", "1.0.0")]
    [Description("A plugin that prevents external services from tracking players via Steam Queries.")]
    class StreamerFriendly : RustPlugin
    {
        private Anonymizer anonymizer = new Anonymizer();

        void Loaded()
        {
            // Anonymize player info
            var activeBasePlayers = BasePlayer.activePlayerList;
            for (int i = 0; i < activeBasePlayers.Count; i++)
            {
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
            anonymizer.Remove(player);
        }

        void Unload()
        {
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
            private IDictionary<string, AnonymizedPlayer> anonymizedPlayers = new Dictionary<string, AnonymizedPlayer>();

            public void Anonymize(IPlayer player)
            {
                AnonymizedPlayer anonymizedPlayer;
                if (!anonymizedPlayers.TryGetValue(player.Id, out anonymizedPlayer))
                {
                    anonymizedPlayer = new AnonymizedPlayer(player);
                }

                SteamServer.UpdatePlayer(anonymizedPlayer.SteamID, DEFAULT_ANONYMIZED_NAME, 0);
                anonymizedPlayers.Add(player.Id, anonymizedPlayer);
            }

            public void Deanonymize(IPlayer player)
            {
                var anonymizedPlayer = anonymizedPlayers[player.Id];
                SteamServer.UpdatePlayer(anonymizedPlayer.SteamID, anonymizedPlayer.Player.Name, 0);
            }

            public void Remove(IPlayer player)
            {
                anonymizedPlayers.Remove(player.Id);
            }

            private class AnonymizedPlayer
            {
                public SteamId SteamID { get; }
                public IPlayer Player { get; }

                public AnonymizedPlayer(IPlayer player)
                {
                    Player = player;

                    SteamID = new SteamId
                    {
                        Value = Convert.ToUInt64(player.Id)
                    };
                }
            }
        }
    }
}
