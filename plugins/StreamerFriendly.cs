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
        private Anonymizer anonymizer;

        #region Hooks

        void Loaded()
        {
            if (!config.Enabled)
            {
                Disable();
                Puts("Plugin is not enabled: skipping start");
                return;
            }

            Enable();
        }

        void Unload()
        {
            Disable();
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

        #endregion Hooks

        #region Helpers

        private void Enable()
        {
            if (anonymizer != null)
            {
                return;
            }

            // Anonymize all active players
            anonymizer = new Anonymizer(BasePlayer.activePlayerList);

            Subscribe("OnUserConnected");
            Subscribe("OnUserDisconnected");
        }

        private void Disable()
        {
            if (anonymizer == null)
            {
                return;
            }

            Unsubscribe("OnUserConnected");
            Unsubscribe("OnUserDisconnected");

            // Deanonymize all active players
            anonymizer.Dispose();
            anonymizer = null;
        }

        #endregion Helpers

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

        private class Anonymizer: IDisposable
        {
            private const string DEFAULT_ANONYMIZED_NAME = "StreamerFriendly";
            private IDictionary<string, AnonymizedPlayer> AnonymizedPlayers;

            public Anonymizer(ListHashSet<BasePlayer> activePlayers)
            {
                AnonymizedPlayers = new Dictionary<string, AnonymizedPlayer>();
                foreach (BasePlayer activePlayer in activePlayers)
                {
                    Anonymize(activePlayer.IPlayer);
                }
            }

            public void Anonymize(IPlayer player)
            {
                AnonymizedPlayer anonymizedPlayer;
                if (!AnonymizedPlayers.TryGetValue(player.Id, out anonymizedPlayer))
                {
                    anonymizedPlayer = new AnonymizedPlayer(player);
                }

                SteamServer.UpdatePlayer(anonymizedPlayer.SteamID, DEFAULT_ANONYMIZED_NAME, 0);
                AnonymizedPlayers.Add(player.Id, anonymizedPlayer);
            }

            public void Deanonymize(IPlayer player)
            {
                var anonymizedPlayer = AnonymizedPlayers[player.Id];
                SteamServer.UpdatePlayer(anonymizedPlayer.SteamID, anonymizedPlayer.Player.Name, 0);
            }

            public void Deanonymize(string playerID)
            {
                var anonymizedPlayer = AnonymizedPlayers[playerID];
                SteamServer.UpdatePlayer(anonymizedPlayer.SteamID, anonymizedPlayer.Player.Name, 0);
            }

            public void Dispose()
            {
                foreach(string playerID in AnonymizedPlayers.Keys)
                {
                    Deanonymize(playerID);
                }

                AnonymizedPlayers = null;
            }

            public void Remove(IPlayer player)
            {
                AnonymizedPlayers.Remove(player.Id);
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
