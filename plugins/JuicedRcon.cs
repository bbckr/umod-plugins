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
    [Info("JuicedRcon", "bbckr", "0.1.0")]
    [Description("A plugin for better, custom RCON experience.")]
    class JuicedRcon : CovalencePlugin
    {
        private static JuicedRemoteConsole rcon;

        void Loaded()
        {
            Application.logMessageReceived += HandleLog;

            rcon = new JuicedRemoteConsole();
            rcon.Start();
        }

        void Unload()
        {
            Application.logMessageReceived -= HandleLog;

            rcon.Stop();
        }

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

        #region Constants

        private static class CommandType
        {
            internal static readonly string CommandEcho = "echo";
            internal static readonly string CommandSay = "say";
        }

        private static class RemoteMessageType
        {
            internal static readonly string Generic = "Generic";
            internal static readonly string Chat = "Chat";

            #region Helpers

            private static Regex PatternChat = new Regex(@"^\[((chat)|(Better Chat))\]");

            internal static bool IsChat(string message)
            {
                return PatternChat.IsMatch(message);
            }

            #endregion Helpers
        }

        #endregion Constants

        private class JuicedRemoteConsole
        {
            private static WebSocketServer server;
            private static JuicedWebSocketBehavior behavior;

            private readonly int port;
            private readonly string password;

            public JuicedRemoteConsole()
            {
                port = Interface.Oxide.Config.Rcon.Port;
                password = Interface.Oxide.Config.Rcon.Password;
            }

            #region Client

            public void Start()
            {
                if (server != null && behavior != null)
                {
                    Interface.Oxide.LogWarning("rcon server already started");
                    return;
                }

                if (string.IsNullOrEmpty(password))
                {
                    Interface.Oxide.LogWarning("rcon server is unprotected: it is recommended a password is set");
                }

                try
                {
                    server = new WebSocketServer(port) { WaitTime = TimeSpan.FromSeconds(5.0), ReuseAddress = true };
                    server.AddWebSocketService(string.Format("/{0}", password), () => behavior = new JuicedWebSocketBehavior(this));

                    server.Start();
                }
                catch (Exception exception)
                {
                    Interface.Oxide.LogException("rcon server failed to initialize {0}", exception);
                    return;
                }

                Interface.Oxide.LogInfo("rcon server listening on {0}", server.Port);
            }

            public void Stop()
            {
                if (server != null)
                {
                    server.Stop();

                    server = null;
                    behavior = null;

                    Interface.Oxide.LogInfo("rcon server has stopped");
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
                private readonly JuicedRemoteConsole Parent;
                private IPAddress _address;
                private string _name;

                public JuicedWebSocketBehavior(JuicedRemoteConsole parent)
                {
                    Parent = parent;
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
                    Parent?.OnMessage(e, Context);
                }

                #endregion MessageHandlers

                #region EventHandlers

                protected override void OnClose(CloseEventArgs e)
                {
                    Interface.Oxide.LogInfo("rcon connection {0} closed", _address);
                }

                protected override void OnError(ErrorEventArgs e)
                {
                    Interface.Oxide.LogException(string.Format("rcon exception: {0}", e.Message), e.Exception);
                }

                protected override void OnOpen()
                {
                    _address = Context.UserEndPoint.Address;
                    Context.QueryString["name"] = _name;
                    Interface.Oxide.LogInfo("rcon connection {0} established", _address);
                }

                #endregion EventHandlers
            }
        }
    }
}
