using System;
using System.CommandLine.Rendering;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
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

        public static bool IsHWIDValid(string hwid)
        {
            if (string.IsNullOrEmpty(hwid))
            {
                return false;
            }

            // Must be all hex characters
            if (!Regex.IsMatch(hwid, @"\A[0-9a-fA-F]+\z"))
            {
                return false;
            }

            // Must be either MD5 (32 chars) or SHA-256 (64 chars)
            return hwid.Length == 32 || hwid.Length == 64;
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

            if (!IsHWIDValid(deviceId) && ClientManager.IsVersionSupported(clientVersion))
            {
                using var packet = MessageWriter.Get(MessageType.Reliable);
                var reason = "Invalid Client Data.\nTry disabling mods or updating the game.";
                Message01JoinGameS2C.SerializeError(packet, false, Api.Innersloth.DisconnectReason.Custom, reason);
                await e.Connection.SendAsync(packet);
                await Task.Delay(TimeSpan.FromMilliseconds(250));
                await e.Connection.Disconnect(reason);
                return;
            }

            if (BanManager.IsBanned(deviceId, e.Connection.EndPoint.Address.ToString()))
            {
                var banEntry = BanManager.GetBanEntry(deviceId);
                banEntry ??= BanManager.GetBanEntry(e.Connection.EndPoint.Address.ToString());

                if (banEntry == null)
                {
                    await e.Connection.Disconnect("error");
                    return;
                }

                using var packet = MessageWriter.Get(MessageType.Reliable);
                var reason = $"You are banned from this server.\nReason: {banEntry.Reason}";
                Message01JoinGameS2C.SerializeError(packet, false, Api.Innersloth.DisconnectReason.Custom, reason);
                await e.Connection.SendAsync(packet);
                await Task.Delay(TimeSpan.FromMilliseconds(250));
                await e.Connection.Disconnect(reason);
                return;
            }

            var connection = new HazelConnection(e.Connection, _connectionLogger);

            await _eventManager.CallAsync(new ClientConnectionEvent(connection, e.HandshakeData));

            // Register client
            await _clientManager.RegisterConnectionAsync(connection, name, clientVersion, deviceId);
        }
    }
}
