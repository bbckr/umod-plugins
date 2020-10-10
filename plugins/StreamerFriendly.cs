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
        private StreamerFriendlyConfig config;
        private Anonymizer anonymizer = new Anonymizer();

        #region Hooks

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

        #endregion Hooks

        #region Configuration

        /// <summary>
        /// StreamerFriendlyConfig is the plugin configuration
        /// </summary>
        private class StreamerFriendlyConfig
        {
            public bool Enabled { get; set; } = true;

        }

        /// <summary>
        /// LoadConfig loads the config from file, if missing it loads and saves the default config
        /// </summary>
        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<StreamerFriendlyConfig>();
            }
            finally
            {
                if (config == null)
                {
                    LoadDefaultConfig();
                    SaveConfig();
                }
            }
        }

        /// <summary>
        /// LoadDefaultConfig initializes the default config for the plugin
        /// </summary>
        protected override void LoadDefaultConfig()
        {
            config = new StreamerFriendlyConfig();
        }

        /// <summary>
        /// SaveConfig saves the config to a file in 'oxide/config/'
        /// </summary>
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion Configuration

        #region Anonymization

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

        #endregion Anonymization
    }
}
