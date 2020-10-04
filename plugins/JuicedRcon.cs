using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.RemoteConsole;
using System;
using System.Net;
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

        private class JuicedConfig
        {
            public bool Enabled { get; set; } = true;
            public Dictionary<string, Profile> Profiles { get; set; } = new Dictionary<string, Profile>();

            public class Profile
            {
                public bool Enabled { get; set; } = false;
                public string Password { get; set; } = "";
                public string[] Whitelist { get; set; } = new string[]{};
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<JuicedConfig>();
            }
            finally
            {
                if (config != null)
                {
                    LoadDefaultConfig();
                }
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new JuicedConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion Configuration

        #region Constants

        private static class CommandType
        {
            public static readonly string CommandEcho = "echo";
            public static readonly string CommandSay = "say";
        }

        private static class RemoteMessageType
        {
            public static readonly string Generic = "Generic";
            public static readonly string Chat = "Chat";

            private static Regex PatternChat = new Regex(@"^\[((chat)|(Better Chat))\]");

            public static bool IsChat(string message)
            {
                return PatternChat.IsMatch(message);
            }
        }

        #endregion Constants

        private class JuicedRemoteConsole
        {
            private readonly JuicedConfig config;

            private readonly string password;
            private readonly int port;

            private static WebSocketServer server;
            private static JuicedWebSocketBehavior behavior;

            public JuicedRemoteConsole(JuicedConfig config)
            {
                this.config = config;
                password = Interface.Oxide.Config.Rcon.Password;
                port = Interface.Oxide.Config.Rcon.Port;
            }

            #region Client

            public void Start()
            {
                if (!config.Enabled)
                {
                    Log(LogType.Log, "rcon server is not enabled: skipping start");
                    return;
                }

                if (server != null && behavior != null)
                {
                    Log(LogType.Log, "rcon server already started");
                    return;
                }

                if (string.IsNullOrEmpty(password))
                {
                    Log(LogType.Error, "rcon server failed to start: it is recommended a password is set");
                    return;
                }

                try
                {
                    server = new WebSocketServer(port) { WaitTime = TimeSpan.FromSeconds(5.0), ReuseAddress = true };
                    server.AddWebSocketService(string.Format("/{0}", password), () => behavior = new JuicedWebSocketBehavior(this));

                    server.Start();
                }
                catch (Exception exception)
                {
                    Log(LogType.Exception, "rcon server failed to start: {0}", exception);
                    return;
                }

                Log(LogType.Log, "rcon server listening on {0}", server.Port);
            }

            public void Stop()
            {
                if (server != null)
                {
                    server.Stop();

                    server = null;
                    behavior = null;

                    JuicedRcon.Log(LogType.Log, "rcon server has stopped");
                }
            }

            #endregion Client

            #region MessageHandlers

            public static void SendMessage(RemoteMessage message)
            {
                if (message != null && server != null && server.IsListening && behavior != null)
                {
                    behavior.SendMessage(message);
                }
            }

            public void SendMessage(string message, int identifier)
            {
                if (!string.IsNullOrEmpty(message) && server != null && server.IsListening && behavior != null)
                {
                    behavior.SendMessage(RemoteMessage.CreateMessage(message, identifier));
                }
            }

            public void SendMessage(WebSocketContext context, RemoteMessage message)
            {
                if (message != null && server != null && server.IsListening && behavior != null)
                {
                    var serializedMessage = JsonConvert.SerializeObject(message, Formatting.Indented);
                    context?.WebSocket?.Send(serializedMessage);
                }
            }

            private void OnMessage(MessageEventArgs e, WebSocketContext context)
            {
                RemoteMessage request = RemoteMessage.GetMessage(e.Data);
                if (request == null || string.IsNullOrEmpty(request.Message))
                {
                    return;
                }

                var data = new List<string>(request.Message.Split(' '));
                var command = data[0];
                data.RemoveAt(0);
                var args = data.ToArray();

                if (Interface.CallHook("OnRconCommand", context.UserEndPoint.Address, command, args) != null)
                {
                    return;
                }

                var output = ConsoleSystem.Run(ConsoleSystem.Option.Server, command, args);
                if (output == null)
                {
                    return;
                }

                if (command == CommandType.CommandSay)
                {
                    Interface.Oxide.LogInfo(string.Format("{0}: {1}", context.QueryString["name"], string.Join(" ", args)));
                    return;
                }

                RemoteMessage response = RemoteMessage.CreateMessage(output, -1, RemoteMessageType.Generic);

                if (command == CommandType.CommandEcho)
                {
                    response.Message = string.Join(" ", args);
                }

                SendMessage(context, response);
            }

            #endregion MessageHandlers


            private class JuicedWebSocketBehavior : WebSocketBehavior
            {
                private readonly JuicedRemoteConsole parent;

                private IPAddress _address;
                private string _name;

                public JuicedWebSocketBehavior(JuicedRemoteConsole parent)
                {
                    this.parent = parent;
                    IgnoreExtensions = true;
                    _name = "SERVER";
                }

                #region MessageHandlers

                public void SendMessage(RemoteMessage message)
                {
                    var serializedMessage = JsonConvert.SerializeObject(message, Formatting.Indented);
                    Sessions.Broadcast(serializedMessage);
                }

                protected override void OnMessage(MessageEventArgs e)
                {
                    parent?.OnMessage(e, Context);
                }

                #endregion MessageHandlers

                #region EventHandlers

                protected override void OnClose(CloseEventArgs e)
                {
                    JuicedRcon.Log(LogType.Log, "rcon connection {0} closed", _address);
                }

                protected override void OnError(ErrorEventArgs e)
                {
                    JuicedRcon.Log(LogType.Log, string.Format("rcon exception: {0}", e.Message), e.Exception);
                }

                protected override void OnOpen()
                {
                    _address = Context.UserEndPoint.Address;
                    Context.QueryString["name"] = _name;
                    JuicedRcon.Log(LogType.Log, "rcon connection {0} established", _address);
                }

                #endregion EventHandlers
            }
        }
    }
}
