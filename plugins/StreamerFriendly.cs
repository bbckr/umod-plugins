using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Steamworks;

namespace Oxide.Plugins
{
    [Info("StreamerFriendly", "bbckr", "2.0.0")]
    [Description("A plugin that prevents external services from tracking players via Steam Queries.")]
    class StreamerFriendly : CovalencePlugin
    {
        private StreamerFriendlyConfig config;
        private Anonymizer anonymizer;

        #region Hooks

        void Loaded()
        {
            {
                DisablePlugin();
                Puts("Plugin is not enabled: skipping start");
                return;
            }

            EnablePlugin();
        }

        void Unload()
        {
            DisablePlugin();
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

        #region Commands

        [Command(CommandType.EnablePluginCommand), Permission("streamerfriendly.admin")]
        private void EnablePluginCommand(IPlayer player, string command, string[] args)
        {
            if (config.Enabled)
            {
                Puts($"Plugin is already enabled");
            }

            config.Enabled = true;
            EnablePlugin();

            SaveConfig();
            Puts($"Plugin is enabled");
        }

        [Command(CommandType.DisablePluginCommand), Permission(PermissionType.AdminPermission)]
        private void DisablePluginCommand(IPlayer player, string command, string[] args)
        {
            if (!config.Enabled)
            {
                Puts($"Plugin is already disabled");
            }

            config.Enabled = false;
            DisablePlugin();

            SaveConfig();
            Puts($"Plugin is disabled");
        }

        [Command(CommandType.RandomCommand), Permission(PermissionType.AdminPermission)]
        private void RandomCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length < 2)
            {
                Puts($"Invalid use of command");
                return;
            }

            switch (args[0])
            {
                case "add-left":
                    if (config.RandomNameConfiguration.Left.Contains(args[1]))
                    {
                        Puts($"Invalid: already exists");
                        return;
                    }

                    config.RandomNameConfiguration.Left.Add(args[1]);
                    break;

                case "add-right":
                    if (config.RandomNameConfiguration.Right.Contains(args[1]))
                    {
                        Puts($"Invalid: already exists");
                        return;
                    }

                    config.RandomNameConfiguration.Right.Add(args[1]);
                    break;

                case "remove-left":
                    config.RandomNameConfiguration.Left.Remove(args[1]);
                    break;

                case "remove-right":
                    config.RandomNameConfiguration.Right.Remove(args[1]);
                    break;

                default:
                    Puts($"Invalid use of command");
                    return;
            }

            SaveConfig();
        }

        #endregion Commands

        #region Constants

        private static class PermissionType
        {
            public const string AdminPermission = "streamerfriendly.admin";
        }

        private static class CommandType
        {
            public const string EnablePluginCommand = "anonymize.enable";
            public const string DisablePluginCommand = "anonymize.disable";
            public const string RandomCommand = "anonymize.random";
        }

        #endregion Constants

        #region Helpers

        private void EnablePlugin()
        {
            if (anonymizer != null)
            {
                return;
            }

            // Anonymize all active players
            anonymizer = new Anonymizer(BasePlayer.activePlayerList, () => config.RandomNameConfiguration.GenerateRandomName());

            Subscribe("OnUserConnected");
            Subscribe("OnUserDisconnected");
        }

        private void DisablePlugin()
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
            public RandomNameConfig RandomNameConfiguration { get; } = new RandomNameConfig();

            public class RandomNameConfig
            {
                private readonly Random _random = new Random();

                public List<string> Left { get; } = new List<string>()
                {
                    "swole", "juiced", "tryhard", "creeping", "slimy", "sleeping", "scummy", "wholesome", "salty", "enraged", "floppy", "friendly", "raided", "honest", "deceitful", "diplomatic", "sincere", "courageous", "fragile", "cynical", "impulsive", "obnoxious", "rusty", "chippy", "moist", "juicy", "spicy", "flaming", "sweaty", "greasy", "kinetic", "toxic", "silent", "spoiled", "jealous", "gullible", "nauseous", "abusive", "vulgar", "repulsive", "vibing", "reactionary", "sleazy", "sociopathic", "prudent", "nifty", "mouthbreathing", "crafty"
                };
                public List<string> Right { get; } = new List<string>()
                {
                    "newton", "clatter", "trombone", "trudy", "goblin", "jolene", "canuck", "danish", "mega", "stranger", "beancan", "newell", "valve", "freeman", "vance", "kleiner", "breen", "mossman", "calhoun", "sabotage", "grigori", "louis", "francis", "bill", "zoey", "chell", "benjamin", "sinatra", "liger", "yoda", "neapolitan", "neckbeard", "trumpling", "zerg", "hoarder", "squid", "simpleton", "maggot", "polliwog", "manchild", "legacy", "snake", "chinook", "cargo", "vagrant", "footman", "sausage"
                };

                public string GenerateRandomName()
                {
                    return $"{Left[_random.Next(Left.Count)]} {Right[_random.Next(Right.Count)]}";
                }
            }
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
        /// SaveConfig saves the config to a file in `oxide/config/`
        /// </summary>
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion Configuration

        #region Anonymization

        private class Anonymizer: IDisposable
        {
            private IDictionary<string, AnonymizedPlayer> AnonymizedPlayers;
            private Func<string> GenerateRandomName;

            public Anonymizer(ListHashSet<BasePlayer> activePlayers, Func<string> generateRandomName)
            {
                AnonymizedPlayers = new Dictionary<string, AnonymizedPlayer>();
                foreach (BasePlayer activePlayer in activePlayers)
                {
                    Anonymize(activePlayer.IPlayer);
                }

                GenerateRandomName = generateRandomName;
            }

            public void Anonymize(IPlayer player)
            {
                AnonymizedPlayer anonymizedPlayer;
                if (!AnonymizedPlayers.TryGetValue(player.Id, out anonymizedPlayer))
                {
                    anonymizedPlayer = new AnonymizedPlayer(player, GenerateRandomName.Invoke());
                }

                SteamServer.UpdatePlayer(anonymizedPlayer.SteamID, anonymizedPlayer.AnonymizedName, 0);
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
                public string AnonymizedName { get; set; }

                public AnonymizedPlayer(IPlayer player, string anonymizedName)
                {
                    Player = player;

                    SteamID = new SteamId
                    {
                        Value = Convert.ToUInt64(player.Id)
                    };

                    AnonymizedName = anonymizedName;
                }
            }
        }

        #endregion Anonymization
    }
}
