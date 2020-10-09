using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.RemoteConsole;
using System;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;
using Oxide.Core.Libraries.Covalence;
using System.Net;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("JuicedRcon", "bbckr", "1.0.0")]
    [Description("A plugin for better, custom RCON experience.")]
    class JuicedRcon : CovalencePlugin
    {
        private JuicedRconConfig config;
        private static JuicedRemoteConsole rcon;

        private static InfoAttribute info;

        #region Hooks

        void Init()
        {
            info = (InfoAttribute)Attribute.GetCustomAttribute(GetType(), typeof(InfoAttribute));
        }

        void Loaded()
        {
            if (!config.Enabled)
            {
                Log(LogType.Log, "rcon server is not enabled: skipping start");
                return;
            }

            Enable();
        }

        void Unload()
        {
            Disable();
        }

        #endregion Hooks

        #region Commands

        /// <summary>
        /// EnableCommand enables the plugin and starts the RCON server
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private string EnableCommand(string[] args)
        {
            if (config.Enabled)
            {
                return $"plugin {info.Title} is already enabled";
            }

            config.Enabled = true;
            Enable();

            SaveConfig();
            return $"plugin {info.Title} is enabled";
        }

        /// <summary>
        /// DisableCommand disables the plugin and stops the RCON server
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private string DisableCommand(string[] args)
        {
            if (!config.Enabled)
            {
                return $"plugin {info.Title} is already disabled";
            }

            config.Enabled = false;
            Disable();

            SaveConfig();
            return $"plugin {info.Title} is disabled";
        }

        /// <summary>
        /// ProfileCommand manages existing RCON server profiles
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private string ProfileCommand(string[] args)
        {
            if (args.Length < 1)
            {
                return "Invalid use of command";
            }

            switch (args[0])
            {
                case "list":
                    return $"{string.Join(", ", config.Profiles.Keys)}";
            }

            if (args.Length < 2)
            {
                return "Invalid use of command";
            }

            switch (args[0])
            {
                case "create":
                    if (config.Profiles.ContainsKey(args[1]))
                    {
                        return $"Invalid: rcon profile {args[1]} already exists";
                    }

                    config.Profiles.Add(args[1], new JuicedRconConfig.Profile
                    {
                        DisplayName = args[1]
                    });
                    SaveConfig();
                    return $"Created rcon profile \"{args[1]}\"";
            }

            JuicedRconConfig.Profile profile;
            config.Profiles.TryGetValue(args[0], out profile);
            if (profile == null)
            {
                return $"Invalid: \"{args[0]}\" does not exist";
            }

            switch (args[1])
            {
                case "get":
                    return profile.ToString();

                case "delete":
                    rcon.TryRemoveWebSocketService(profile);
                    config.Profiles.Remove(args[0]);
                    SaveConfig();
                    return $"Deleted rcon profile {args[1]}";

                case "enable":
                    if (profile.Enabled)
                    {
                        return $"Invalid: rcon profile {args[0]} is already enabled";
                    }

                    profile.Enabled = true;
                    rcon.TryAddWebSocketService(profile);
                    SaveConfig();
                    return $"Enabled rcon profile {args[0]}";

                case "disable":
                    if (!profile.Enabled || string.IsNullOrEmpty(profile.Password))
                    {
                        return $"Invalid: rcon profile {args[0]} is already disabled";
                    }

                    rcon.TryRemoveWebSocketService(profile);
                    profile.Enabled = false;
                    SaveConfig();
                    return $"Disabled rcon profile {args[0]}";

                case "set":
                    if (args.Length < 4)
                    {
                        return "Invalid use of command";
                    }

                    switch (args[2])
                    {
                        case "password":
                            if (args[3] != "" && args[3] != Interface.Oxide.Config.Rcon.Password && config.Profiles.Values.FirstOrDefault(p => p.Password == args[3]) != null)
                            {
                                return "Invalid password";
                            }

                            rcon.TryRemoveWebSocketService(profile);
                            profile.Password = args[3];
                            rcon.TryAddWebSocketService(profile);
                            SaveConfig();
                            return "Successfully updated password";

                        case "displayname":
                            profile.DisplayName = args[3];
                            SaveConfig();
                            return "Successfully updated display name";

                        default:
                            return "Invalid use of command";
                    }

                case "add":
                    if (args.Length < 3)
                    {
                        return "Invalid use of command";
                    }

                    if (profile.HasAccess(args[2]))
                    {
                        return $"Invalid: rcon profile {args[0]} already has access to {args[2]}";
                    }

                    profile.AllowedCommands.Add(args[2]);
                    SaveConfig();
                    return "Successfully added command";

                case "remove":
                    if (args.Length < 3)
                    {
                        return "Invalid use of command";
                    }

                    profile.AllowedCommands.Remove(args[2]);
                    SaveConfig();
                    return "Successfully removed command";

                default:
                    return "Invalid use of command";
            }
        }

        #endregion Commands

        #region Helpers


        /// <summary>
        /// Enable is the logic for enabling the plugin
        /// </summary>
        private void Enable()
        {
            // handler for all log messages received in Oxide
            Application.logMessageReceived += HandleLog;

            rcon = new JuicedRemoteConsole(this);   
            rcon.Start();
        }

        /// <summary>
        /// Disable is the logic for disabling the plugin
        /// </summary>
        private void Disable()
        {
            Application.logMessageReceived -= HandleLog;

            if (rcon == null)
            {
                return;
            }

            rcon.Stop();
            rcon = null;
        }

        /// <summary>
        /// HandleLog is the handler for all received log messages
        /// </summary>
        /// <param name="message"></param>
        /// <param name="stackTrace"></param>
        /// <param name="type"></param>
        private static void HandleLog(string message, string stackTrace, LogType type)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }
            
            var remoteType = RemoteMessageType.Generic;

            if (RemoteMessageType.IsChat(message))
            {
                remoteType = RemoteMessageType.Chat;
            }

            JuicedRemoteConsole.Broadcast(new RemoteMessage
            {
                Message = message,
                Identifier = -1,
                Type = remoteType,
                Stacktrace = stackTrace
            });
        }

        private static void Log(LogType type, string format, params object[] args)
        {
            var message = string.Format("[{0}] {1}", info.Title, format);

            switch(type)
            {
                case LogType.Warning:
                    Interface.Oxide.LogWarning(message, args);
                    break;
                case LogType.Error:
                    Interface.Oxide.LogError(message, args);
                    break;
                case LogType.Exception:
                    Interface.Oxide.LogError(message, args);
                    break;
                default:
                    Interface.Oxide.LogInfo(message, args);
                    break;
            }
        }

        #endregion Helpers

        #region Configuration

        /// <summary>
        /// JuicedRconConfig is the plugin configuration
        /// </summary>
        private class JuicedRconConfig
        {
            public bool Enabled { get; set; } = true;
            public Dictionary<string, Profile> Profiles { get; set; } = new Dictionary<string, Profile>();
            public bool AnonymizedProfileEnabled { get; set; } = false;

            public JuicedRconConfig()
            {
                // default profile for moderator
                Profiles.Add("Moderator", new Profile
                {
                    DisplayName = "Moderator",
                    AllowedCommands = new List<string>
                    {
                        "say",
                        "kick",
                        "ban",
                        "unban",
                        "playerlist",
                        "skipqueue",
                        "teleport",
                        "status",
                        "juicedrcon.say"
                    }
                });
            }

            public class Profile
            {
                /// <summary>
                /// DisplayName is used when broadcasting messages to chat
                /// </summary>
                public string DisplayName { get; set; } = "Unnamed";

                /// <summary>
                /// Enabled is whether the RCON server should enable a service for the profile
                /// </summary>
                public bool Enabled { get; set; } = false;

                /// <summary>
                /// Password is the password used to connect to the RCON server as the profile
                /// </summary>
                public string Password { get; set; } = "";

                /// <summary>
                /// FullAccess is whether the profile has access to all RCON commands
                /// </summary>
                public bool FullAccess { get; set; } = false;

                /// <summary>
                /// AllowedCommands are the whitelisted commands for the profile, ignored if FullAccess is set
                /// </summary>
                public List<string> AllowedCommands { get; set; } = new List<string>();

                /// <summary>
                /// HasPermissions checks if a profile has access to the given command
                /// </summary>
                /// <param name="command"></param>
                /// <returns></returns>
                public bool HasAccess(string command)
                {
                    if (FullAccess)
                    {
                        return true;
                    }

                    return AllowedCommands.Exists(c =>
                    {
                        if (c.EndsWith("*"))
                        {
                            return command.StartsWith(c.Trim('*'));
                        }

                        return command.Equals(c);
                    });
                }

                public override string ToString()
                {
                    return $"DisplayName: {DisplayName}, enabled: {Enabled}, commands: [{string.Join(", ", AllowedCommands)}]";
                }

                /// <summary>
                /// CreateRootProfile is the profile for the root service with all permissions
                /// </summary>
                /// <returns></returns>
                public static Profile CreateRootProfile()
                {
                    return new Profile()
                    {
                        DisplayName = "Root",
                        Enabled = true,
                        Password = Interface.Oxide.Config.Rcon.Password,
                        FullAccess = true
                    };
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
                config = Config.ReadObject<JuicedRconConfig>();
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
            config = new JuicedRconConfig();
        }

        /// <summary>
        /// SaveConfig saves the config to a file in 'oxide/config/'
        /// </summary>
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion Configuration

        #region Constants

        /// <summary>
        /// CommandType contains the various command types for RCON commands
        /// </summary>
        private class CommandType
        {
            public static readonly string CommandEcho = "echo";
            public static readonly string CommandSay = "say";

            public static readonly string CommandJuicedRconEnable = "juicedrcon.enable";
            public static readonly string CommandJuicedRconDisable = "juicedrcon.disable";
            public static readonly string CommandJuicedRconProfile = "juicedrcon.profile";
            public static readonly string CommandJuicedRconSay = "juicedrcon.say";
        }

        /// <summary>
        /// RemoteMessageType contains the various message types for RCON messages
        /// </summary>
        private static class RemoteMessageType
        {
            public static readonly string Generic = "Generic";
            public static readonly string Chat = "Chat";

            /// <summary>
            /// PatternChat matches generic chat messages or Better Chat prefixed chat messages
            /// </summary>
            private static Regex PatternChat = new Regex(@"^\[((chat)|(Better Chat))\]");

            /// <summary>
            /// IsChat checks if the message is a chat message
            /// </summary>
            /// <param name="message"></param>
            /// <returns></returns>
            public static bool IsChat(string message)
            {
                return PatternChat.IsMatch(message);
            }
        }

        #endregion Constants

        #region WebSocket

        /// <summary>
        /// JuicedRemoteConsole is the custom websocket RCON server
        /// </summary>
        private class JuicedRemoteConsole : WebSocketServer
        {
            private readonly JuicedRcon parent;
            private readonly JuicedRconConfig.Profile rootProfile;
            private Dictionary<string, Func<string[], string>> commands;

            private readonly int port;

            private static WebSocketServer server;

            public JuicedRemoteConsole(JuicedRcon parent)
            {
                this.parent = parent;
                rootProfile = JuicedRconConfig.Profile.CreateRootProfile();

                port = Interface.Oxide.Config.Rcon.Port;
                commands = new Dictionary<string, Func<string[], string>>
                {
                    { CommandType.CommandJuicedRconEnable, (args) => parent.EnableCommand(args) },
                    { CommandType.CommandJuicedRconDisable, (args) => parent.DisableCommand(args) },
                    { CommandType.CommandJuicedRconProfile, (args) => parent.ProfileCommand(args) }
                };
            }

            #region Server

            /// <summary>
            /// Start starts the RCON server and all services
            /// </summary>
            public new void Start()
            {
                if (server != null)
                {
                    Log(LogType.Log, "Rcon server is already listening");
                    return;
                }

                try
                {
                    server = new WebSocketServer(port) { WaitTime = TimeSpan.FromSeconds(5.0), ReuseAddress = true };

                    // setup root profile
                    TryAddWebSocketService(rootProfile);

                    // setup custom profiles
                    foreach (KeyValuePair<string, JuicedRconConfig.Profile> profile in parent.config.Profiles)
                    {
                        TryAddWebSocketService(profile.Value);
                    }

                    server.Start();
                }
                catch (Exception exception)
                {
                    Log(LogType.Exception, $"Unable to start rcon server: {exception}");
                    return;
                }

                Log(LogType.Log, $"Listening rcon server on {server.Port}");
            }

            /// <summary>
            /// TryAddWebSocketService tries to add a service to the RCON server based on a profile
            /// </summary>
            /// <param name="profile"></param>
            /// <returns></returns>
            public bool TryAddWebSocketService(JuicedRconConfig.Profile profile)
            {
                if (!profile.Enabled)
                {
                    return false;
                }

                if (string.IsNullOrEmpty(profile.Password))
                {
                    Log(LogType.Error, $"Unable to start rcon profile {profile.DisplayName}: it is recommended a password is set");
                    return false;
                }

                server.AddWebSocketService($"/{profile.Password}", () => new JuicedWebSocketBehavior(this, profile));
                Log(LogType.Log, $"Enabled rcon profile {profile.DisplayName}");

                return true;
            }

            /// <summary>
            /// TryRemoveWebSocketService tries to remove a service from the RCON server based on a profile
            /// </summary>
            /// <param name="profile"></param>
            /// <returns></returns>
            public bool TryRemoveWebSocketService(JuicedRconConfig.Profile profile)
            {
                if(!profile.Enabled || string.IsNullOrEmpty(profile.Password))
                {
                    return false;
                }

                server.RemoveWebSocketService($"/{profile.Password}");
                Log(LogType.Log, $"Disabled rcon profile {profile.DisplayName}");

                return true;
            }

            /// <summary>
            /// Stop stops the RCON server
            /// </summary>
            public new void Stop()
            {
                if (server != null)
                {
                    server.Stop();

                    server = null;

                    Log(LogType.Log, "Rcon server stopped listening");
                }
            }

            #endregion Server

            #region MessageHandlers

            /// <summary>
            /// OnMessage handles all executed RCON requests from connected sessions
            /// </summary>
            /// <param name="e"></param>
            /// <param name="behavior"></param>
            private void OnMessage(MessageEventArgs e, JuicedWebSocketBehavior behavior)
            {
                RemoteMessage request = RemoteMessage.GetMessage(e.Data);

                // ignore empty requests
                if (request == null || string.IsNullOrEmpty(request.Message))
                {
                    return;
                }

                var data = new List<string>(request.Message.Split(' '));
                var command = data[0];
                data.RemoveAt(0);
                var args = data.ToArray();

                if (!behavior.Profile.HasAccess(command))
                {
                    Broadcast(behavior, "Permission denied", -1);
                    return;
                }

                if (Interface.CallHook("OnRconCommand", behavior?.Context?.UserEndPoint.Address, command, args) != null)
                {
                    return;
                }

                string output;
                // run command and capture output
                Func<string[], string> cmd;
                if(commands.TryGetValue(command, out cmd)){
                    output = cmd.Invoke(args);
                } else
                {
                    output = ConsoleSystem.Run(ConsoleSystem.Option.Server, command, args);
                }

                // do not broadcast if nothing to broadcast
                if (output == null)
                {
                    return;
                }

                // handle "say"
                if (command == CommandType.CommandSay)
                {
                    // broadcast to all sessions
                    Interface.Oxide.LogInfo(string.Format($"SERVER {string.Join(" ", args)}"));
                    return;
                }

                RemoteMessage response = RemoteMessage.CreateMessage(output, -1, RemoteMessageType.Generic);

                // handle "echo"
                if (command == CommandType.CommandEcho)
                {
                    response.Message = string.Join(" ", args);
                }

                // broadcast to session
                Broadcast(behavior, response);
            }

            /// <summary>
            /// Broadcast broadcasts to all active RCON sessions
            /// </summary>
            /// <param name="message"></param>
            public static void Broadcast(RemoteMessage message)
            {
                Broadcast(null, message);
            }

            /// <summary>
            /// Broadcast broadcasts to a specific RCON session
            /// </summary>
            /// <param name="behavior"></param>
            /// <param name="message"></param>
            /// <param name="identifier"></param>
            public static void Broadcast(JuicedWebSocketBehavior behavior, string message, int identifier)
            {
                Broadcast(behavior, RemoteMessage.CreateMessage(message, identifier));
            }

            /// <summary>
            /// Broadcast broadcasts to a specific RCON session
            /// </summary>
            /// <param name="behavior"></param>
            /// <param name="message"></param>
            public static void Broadcast(JuicedWebSocketBehavior behavior, RemoteMessage message)
            {
                if (server == null || !server.IsListening)
                {
                    return;
                }

                var serializedMessage = JsonConvert.SerializeObject(message, Formatting.Indented);

                if (behavior != null)
                {
                    behavior?.Context?.WebSocket.SendAsync(serializedMessage, null);
                    return;
                }

                server.WebSocketServices.BroadcastAsync(serializedMessage, null);
            }
            
            #endregion MessageHandlers

            #region Service

            /// <summary>
            /// JuicedWebSocketBehavior is the behavior for the websocket service
            /// </summary>
            public class JuicedWebSocketBehavior : WebSocketBehavior
            {
                private readonly JuicedRemoteConsole parent;
                private IPAddress _address;

                public readonly JuicedRconConfig.Profile Profile;

                public JuicedWebSocketBehavior(JuicedRemoteConsole parent, JuicedRconConfig.Profile profile)
                {
                    this.parent = parent;
                    Profile = profile;

                    IgnoreExtensions = true;
                }

                #region EventHandlers

                /// <summary>
                /// OnMessage triggers the RCON server message handler when a session sends a request
                /// </summary>
                /// <param name="e"></param>
                protected override void OnMessage(MessageEventArgs e)
                {
                    parent?.OnMessage(e, this);
                }

                /// <summary>
                /// OnClose triggers when an RCON session is closed
                /// </summary>
                /// <param name="e"></param>
                protected override void OnClose(CloseEventArgs e)
                {
                    JuicedRcon.Log(LogType.Log, $"Closed rcon connection {Profile?.DisplayName}[{_address}]");
                }

                /// <summary>
                /// OnError triggers when an RCON session has an error
                /// </summary>
                /// <param name="e"></param>
                protected override void OnError(ErrorEventArgs e)
                {
                    JuicedRcon.Log(LogType.Exception, $"An exception has occurred: {e.Message}");
                }


                /// <summary>
                /// OnOpen triggers when a new RCON session is established
                /// </summary>
                protected override void OnOpen()
                {
                    _address = Context.UserEndPoint.Address;
                    JuicedRcon.Log(LogType.Log, $"Established rcon connection {Profile?.DisplayName}[{_address}]");
                }

                #endregion EventHandlers
            }
        }
        #endregion Service
    }
    #endregion WebSocket
}
