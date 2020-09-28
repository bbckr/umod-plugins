using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.RemoteConsole;
using System;
using System.Net;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Oxide.Plugins
{
    [Info("BetterRcon", "bbckr", "0.1.0")]
    [Description("A plugin for better, custom RCON experience.")]
    class BetterRcon : CovalencePlugin
    {
        public class BetterRemoteConsole
        {
            private WebSocketServer server;
            private BetterWebSocketBehavior behavior;

            private readonly int port;
            private readonly string password;

            public BetterRemoteConsole()
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
                    server.AddWebSocketService(string.Format("/{0}", password), () => behavior = new BetterWebSocketBehavior(this));

                    server.Start();
                }
                catch (Exception exception)
                {
                    Interface.Oxide.LogException($"rcon server failed to initialize", exception);
                    return;
                }

                Interface.Oxide.LogInfo("rcon server listening on {0}", server.Port);
            }

            public void Stop()
            {
                if (server != null && !server.IsListening)
                {
                    server.Stop();
                    server = null;
                    behavior = null;
                    Interface.Oxide.LogInfo($"rcon server has stopped");
                }
            }

            public void SendMessage(RemoteMessage message)
            {
                var serializedMessage = JsonConvert.SerializeObject(message, Formatting.Indented);
                server.WebSocketServices.Broadcast(serializedMessage);
            }

            private void OnMessage(MessageEventArgs e)
            {
                // TODO: (bckr) make MessageEventArgs to serialized remotemessage
                server.WebSocketServices.Broadcast(e.Data);
            }

            private class BetterWebSocketBehavior : WebSocketBehavior
            {
                private readonly BetterRemoteConsole Parent;
                private IPAddress _address;

                public BetterWebSocketBehavior(BetterRemoteConsole parent)
                {
                    Parent = parent;
                    IgnoreExtensions = true;
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
                    Parent?.OnMessage(e);
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
