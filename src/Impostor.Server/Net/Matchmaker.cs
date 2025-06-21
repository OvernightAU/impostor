using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Impostor.Api.Events.Managers;
using Impostor.Api.Net;
using Impostor.Api.Net.Messages.S2C;
using Impostor.Hazel;
using Impostor.Hazel.Udp;
using Impostor.Server.Events.Client;
using Impostor.Server.Net.Hazel;
using Impostor.Server.Net.Manager;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Impostor.Server.Net
{
    internal class Matchmaker
    {
        private readonly ClientManager _clientManager;
        private readonly ObjectPool<MessageReader> _readerPool;
        private readonly ILogger<HazelConnection> _connectionLogger;
        private readonly IEventManager _eventManager;
        private UdpConnectionListener _connection;

        public Matchmaker(
            ClientManager clientManager,
            ObjectPool<MessageReader> readerPool,
            ILogger<HazelConnection> connectionLogger,
            IEventManager eventManager)
        {
            _clientManager = clientManager;
            _readerPool = readerPool;
            _connectionLogger = connectionLogger;
            _eventManager = eventManager;
        }

        public async ValueTask StartAsync(IPEndPoint ipEndPoint)
        {
            var mode = ipEndPoint.AddressFamily switch
            {
                AddressFamily.InterNetwork => IPMode.IPv4,
                AddressFamily.InterNetworkV6 => IPMode.IPv6,
                _ => throw new InvalidOperationException(),
            };

            _connection = new UdpConnectionListener(ipEndPoint, _readerPool, mode)
            {
                NewConnection = OnNewConnection,
            };

            await _connection.StartAsync();
        }

        public async ValueTask StopAsync()
        {
            await _connection.DisposeAsync();
        }

        private async ValueTask OnNewConnection(NewConnectionEventArgs e)
        {
            // Handshake.
            var clientVersion = e.HandshakeData.ReadInt32();
            var name = e.HandshakeData.ReadString();
            var deviceId = string.Empty;
            try
            {
                deviceId = e.HandshakeData.ReadString();
            }
            catch
            {
            }

            if (string.IsNullOrEmpty(deviceId) && ClientManager.IsVersionSupported(clientVersion))
            {
                using var packet = MessageWriter.Get(MessageType.Reliable);
                string reason = "Invalid Client Data.\nTry disabling mods or updating the game.";
                Message01JoinGameS2C.SerializeError(packet, false, Api.Innersloth.DisconnectReason.Custom, reason);
                await e.Connection.SendAsync(packet);
                await Task.Delay(TimeSpan.FromMilliseconds(250));
                await (e.Connection as IHazelConnection).DisconnectAsync(reason);
                return;
            }

            var connection = new HazelConnection(e.Connection, _connectionLogger);

            await _eventManager.CallAsync(new ClientConnectionEvent(connection, e.HandshakeData));

            // Register client
            await _clientManager.RegisterConnectionAsync(connection, name, clientVersion, deviceId);
        }
    }
}
