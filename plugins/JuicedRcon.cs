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

            JuicedRemoteConsole.SendMessage(new RemoteMessage
            {
                Message = message,
                Identifier = -1,
                Type = "Generic",
                Stacktrace = stackTrace
            });
        }

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
                RemoteMessage message = RemoteMessage.GetMessage(e.Data);

                if (message == null || string.IsNullOrEmpty(message.Message))
                {
                    return;
                }

                var args = new List<string>(message.Message.Split(' '));
                var command = args[0];
                args.RemoveAt(0);

                if (Interface.CallHook("OnRconCommand", context.UserEndPoint, e.Data, args.ToArray()) != null)
                {
                    return;
                }

                var output = ConsoleSystem.Run(ConsoleSystem.Option.Server, command, args.ToArray());
                if (output == null)
                {
                    return;
                }


                SendMessage(context, RemoteMessage.CreateMessage(output, -1));
            }

            private class JuicedWebSocketBehavior : WebSocketBehavior
            {
                private readonly JuicedRemoteConsole Parent;
                private IPAddress _address;

                public JuicedWebSocketBehavior(JuicedRemoteConsole parent)
                {
                    Parent = parent;
                    IgnoreExtensions = true;
                }

                public void SendMessage(RemoteMessage message)
                {
                    var serializedMessage = JsonConvert.SerializeObject(message, Formatting.Indented);
                    Sessions.Broadcast(serializedMessage);
                }

                protected override void OnClose(CloseEventArgs e)
                {
                    Interface.Oxide.LogInfo("rcon connection {0} closed", _address);
                }

                protected override void OnError(ErrorEventArgs e)
                {
                    Interface.Oxide.LogException(string.Format("rcon exception: {0}", e.Message), e.Exception);
                }
                
                protected override void OnMessage(MessageEventArgs e)
                {
                    Parent?.OnMessage(e, Context);
                }

                protected override void OnOpen()
                {
                    _address = Context.UserEndPoint.Address;
                    Interface.Oxide.LogInfo("rcon connection {0} established", _address);
                }
            }
        }
    }
}
