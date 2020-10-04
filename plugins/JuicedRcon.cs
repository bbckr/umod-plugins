using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.RemoteConsole;
using System;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Collections.Generic;
using WebSocketSharp.Net.WebSockets;
using UnityEngine;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("JuicedRcon", "bbckr", "1.0.0")]
    [Description("A plugin for better, custom RCON experience.")]
    class JuicedRcon : CovalencePlugin
    {
        private JuicedConfig config;
        private static JuicedRemoteConsole rcon;

        private static InfoAttribute info;

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

            // handler for all log messages received in Oxide
            Application.logMessageReceived += HandleLog;

            rcon = new JuicedRemoteConsole(config);
            rcon.Start();
        }

        void Unload()
        {
            Application.logMessageReceived -= HandleLog;

            rcon.Stop();
        }

        #region Helpers

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

            JuicedRemoteConsole.SendMessage(new RemoteMessage
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
        /// JuicedConfig is the plugin configuration
        /// </summary>
        private class JuicedConfig
        {
            public bool Enabled { get; set; } = true;
            public Dictionary<string, Profile> Profiles { get; set; } = new Dictionary<string, Profile>();

            public JuicedConfig()
            {
                // default profile for moderator
                Profiles.Add("Moderator", new Profile
                {
                    DisplayName = "Moderator",
                    Permissions = new string[]
                    {
                        "say"
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
                /// FullPermissions is whether the profile has access to all RCON commands
                /// </summary>
                public bool FullPermissions { get; set; } = false;

                /// <summary>
                /// Permissions are the allowed permissions for the profile, ignored if FullPermissions is set
                /// </summary>
                public string[] Permissions { get; set; } = new string[]{};

                /// <summary>
                /// HasPermissions checks if a profile has access to the given command
                /// </summary>
                /// <param name="command"></param>
                /// <returns></returns>
                public bool HasPermission(string command)
                {
                    if (FullPermissions)
                    {
                        return true;
                    }

                    return Array.Exists(Permissions, v => {
                        if (v.EndsWith("*"))
                        {
                            return command.StartsWith(v.Trim('*'));
                        }

                        return command.Equals(v);
                    });
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
                        FullPermissions = true
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
                config = Config.ReadObject<JuicedConfig>();
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
            config = new JuicedConfig();
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
        private static class CommandType
        {
            public static readonly string CommandEcho = "echo";
            public static readonly string CommandSay = "say";
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

        /// <summary>
        /// JuicedRemoteConsole is the custom websocket RCON server
        /// </summary>
        private class JuicedRemoteConsole
        {
            private readonly JuicedConfig config;
            private readonly JuicedConfig.Profile rootProfile;

            private readonly int port;

            private static WebSocketServer server;
            private static JuicedWebSocketBehavior behavior;

            public JuicedRemoteConsole(JuicedConfig config)
            {
                this.config = config;
                rootProfile = JuicedConfig.Profile.CreateRootProfile();

                port = Interface.Oxide.Config.Rcon.Port;
            }

            #region Server

            /// <summary>
            /// Start starts the RCON server and all services
            /// </summary>
            public void Start()
            {
                if (server != null && behavior != null)
                {
                    Log(LogType.Log, "rcon server already started");
                    return;
                }

                try
                {
                    server = new WebSocketServer(port) { WaitTime = TimeSpan.FromSeconds(5.0), ReuseAddress = true };

                    // setup root profile
                    AddService(rootProfile);

                    // setup custom profiles
                    foreach (KeyValuePair<string, JuicedConfig.Profile> profile in config.Profiles)
                    {
                        AddService(profile.Value);
                    }

                    server.Start();
                }
                catch (Exception exception)
                {
                    Log(LogType.Exception, $"rcon server failed to start: {exception}");
                    return;
                }

                Log(LogType.Log, $"rcon server listening on {server.Port}");
            }

            /// <summary>
            /// AddService adds a websocket service to the RCON server based on a profile
            /// </summary>
            /// <param name="profile"></param>
            public void AddService(JuicedConfig.Profile profile)
            {
                if (!profile.Enabled)
                {
                    Log(LogType.Log, $"rcon profile {profile.DisplayName} is not enabled: skipping start");
                    return;
                }

                if (string.IsNullOrEmpty(profile.Password))
                {
                    Log(LogType.Error, $"rcon profile {profile.DisplayName} failed to start: it is recommended a password is set");
                    return;
                }

                server.AddWebSocketService($"/{profile.Password}", () => behavior = new JuicedWebSocketBehavior(this, profile));
                Log(LogType.Log, $"rcon profile {profile.DisplayName} is enabled");
            }

            /// <summary>
            /// Stop stops the RCON server
            /// </summary>
            public void Stop()
            {
                if (server != null)
                {
                    server.Stop();

                    server = null;
                    behavior = null;

                    Log(LogType.Log, "rcon server has stopped");
                }
            }

            #endregion Server

            #region MessageHandlers

            /// <summary>
            /// OnMessage handles all executed RCON requests from connected sessions
            /// </summary>
            /// <param name="e"></param>
            /// <param name="context"></param>
            /// <param name="profile"></param>
            private void OnMessage(MessageEventArgs e, WebSocketContext context, JuicedConfig.Profile profile)
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

                if (!profile.HasPermission(command))
                {
                    SendMessage(context, "You do not have permission to run the command", -1);
                    return;
                }

                if (Interface.CallHook("OnRconCommand", context.UserEndPoint.Address, command, args) != null)
                {
                    return;
                }

                // run command and capture output
                var output = ConsoleSystem.Run(ConsoleSystem.Option.Server, command, args);
                if (output == null)
                {
                    return;
                }

                // handle "say"
                if (command == CommandType.CommandSay)
                {
                    // broadcast to all sessions
                    Interface.Oxide.LogInfo(string.Format($"{profile.DisplayName}: {string.Join(" ", args)}"));
                    return;
                }

                RemoteMessage response = RemoteMessage.CreateMessage(output, -1, RemoteMessageType.Generic);

                // handle "echo"
                if (command == CommandType.CommandEcho)
                {
                    response.Message = string.Join(" ", args);
                }

                // broadcast to session
                SendMessage(context, response);
            }

            /// <summary>
            /// SendMessage broadcasts to all active RCON sessions
            /// </summary>
            /// <param name="message"></param>
            public static void SendMessage(RemoteMessage message)
            {
                if (message != null && server != null && server.IsListening && behavior != null)
                {
                    behavior.SendMessage(message);
                }
            }

            /// <summary>
            /// SendMessage broadcasts to all active RCON sessions
            /// </summary>
            /// <param name="message"></param>
            /// <param name="identifier"></param>
            public void SendMessage(string message, int identifier)
            {
                if (!string.IsNullOrEmpty(message) && server != null && server.IsListening && behavior != null)
                {
                    behavior.SendMessage(RemoteMessage.CreateMessage(message, identifier));
                }
            }

            /// <summary>
            /// SendMessage broadcasts to a specific RCON session
            /// </summary>
            /// <param name="context"></param>
            /// <param name="message"></param>
            /// <param name="identifier"></param>
            public void SendMessage(WebSocketContext context, string message, int identifier)
            {
                if (!string.IsNullOrEmpty(message) && server != null && server.IsListening && behavior != null)
                {
                    var serializedMessage = JsonConvert.SerializeObject(RemoteMessage.CreateMessage(message, identifier), Formatting.Indented);
                    context?.WebSocket?.Send(serializedMessage);
                }
            }

            /// <summary>
            /// SendMessage broadcasts to a specific RCON session
            /// </summary>
            /// <param name="context"></param>
            /// <param name="message"></param>
            public void SendMessage(WebSocketContext context, RemoteMessage message)
            {
                if (message != null && server != null && server.IsListening && behavior != null)
                {
                    var serializedMessage = JsonConvert.SerializeObject(message, Formatting.Indented);
                    context?.WebSocket?.Send(serializedMessage);
                }
            }

            #endregion MessageHandlers

            /// <summary>
            /// JuicedWebSocketBehavior is the behavior for the websocket service
            /// </summary>
            private class JuicedWebSocketBehavior : WebSocketBehavior
            {
                private readonly JuicedRemoteConsole parent;
                private readonly JuicedConfig.Profile profile;

                public JuicedWebSocketBehavior(JuicedRemoteConsole parent, JuicedConfig.Profile profile)
                {
                    this.parent = parent;
                    this.profile = profile;

                    IgnoreExtensions = true;
                }

                #region MessageHandlers

                /// <summary>
                /// SendMessage broadcasts to all active RCON sessions
                /// </summary>
                /// <param name="message"></param>
                public void SendMessage(RemoteMessage message)
                {
                    var serializedMessage = JsonConvert.SerializeObject(message, Formatting.Indented);
                    Sessions.Broadcast(serializedMessage);
                }

                /// <summary>
                /// OnMessage triggers the RCON server message handler when a session sends a request
                /// </summary>
                /// <param name="e"></param>
                protected override void OnMessage(MessageEventArgs e)
                {
                    parent?.OnMessage(e, Context, profile);
                }

                #endregion MessageHandlers

                #region EventHandlers


                /// <summary>
                /// OnClose triggers when an RCON session is closed
                /// </summary>
                /// <param name="e"></param>
                protected override void OnClose(CloseEventArgs e)
                {
                    JuicedRcon.Log(LogType.Log, "rcon connection {0}[{1}] closed", profile.DisplayName, Context.UserEndPoint.Address);
                }

                /// <summary>
                /// OnError triggers when an RCON session has an error
                /// </summary>
                /// <param name="e"></param>
                protected override void OnError(ErrorEventArgs e)
                {
                    JuicedRcon.Log(LogType.Log, string.Format("rcon exception: {0}", e.Message), e.Exception);
                }


                /// <summary>
                /// OnOpen triggers when a new RCON session is established
                /// </summary>
                protected override void OnOpen()
                {
                    JuicedRcon.Log(LogType.Log, "rcon connection {0}[{1}] established", profile.DisplayName, Context.UserEndPoint.Address);
                }

                #endregion EventHandlers
            }
        }
    }
}
