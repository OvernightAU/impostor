using System;
using System.Numerics;
using System.Threading.Tasks;
using Impostor.Api;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Api.Net.Messages;
using Impostor.Server.Net.Manager;
using Impostor.Server.Net.State;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Net.Inner.Objects.Components
{
    internal partial class InnerCustomNetworkTransform : InnerNetObject
    {
        private static readonly FloatRange XRange = new FloatRange(-40f, 40f);
        private static readonly FloatRange YRange = new FloatRange(-40f, 40f);

        private readonly ILogger<InnerCustomNetworkTransform> _logger;
        private readonly InnerPlayerControl _playerControl;
        private readonly Game _game;

        private ushort _lastSequenceId;

        public Vector2 Position { get; private set; }

        private enum RpcCalls
        {
            SnapTo = 21,
        }

        public InnerCustomNetworkTransform(ILogger<InnerCustomNetworkTransform> logger, InnerPlayerControl playerControl, Game game)
        {
            _logger = logger;
            _playerControl = playerControl;
            _game = game;
        }

        private static bool SidGreaterThan(ushort newSid, ushort prevSid)
        {
            var num = (ushort)(prevSid + (uint)short.MaxValue);

            return (int)prevSid < (int)num
                ? newSid > prevSid && newSid <= num
                : newSid > prevSid || newSid <= num;
        }


        public override ValueTask HandleRpc(ClientPlayer sender, ClientPlayer? target, byte call, IMessageReader reader)
        {
            if (call == (byte)RpcCalls.SnapTo)
            {
                if (!ClientManager.SupportedVersions.Contains(_game.Host.Client.GameVersion))
                {
                    return default;
                }

                if (!sender.IsOwnerOrHost(this))
                {
                    throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SnapTo)} to an unowned {nameof(InnerPlayerControl)}");
                }

                if (target != null)
                {
                    throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SnapTo)} to a specific player instead of broadcast");
                }

                SnapTo(reader.ReadVector2(), reader.ReadUInt16());
            }
            else
            {
                _logger.LogWarning("{0}: Unknown rpc call {1}", nameof(InnerCustomNetworkTransform), call);
            }

            return default;
        }

        public override bool Serialize(IMessageWriter writer, bool initialState)
        {
            if (initialState)
            {
                writer.Write(_lastSequenceId);
                writer.Write(Position);
                return true;
            }

            writer.Write(_lastSequenceId);

            writer.WritePacked(1);
            writer.Write(Position);
            return true;
        }

        public override void Deserialize(IClientPlayer sender, IClientPlayer? target, IMessageReader reader, bool initialState)
        {
            if (!ClientManager.SupportedVersions.Contains(_game.Host.Client.GameVersion))
            {
                return;
            }

            var sequenceId = reader.ReadUInt16();

            if (initialState)
            {
                _lastSequenceId = sequenceId;
                Position = reader.ReadVector2();
            }
            else
            {
                if (!sender.IsOwner(this))
                {
                    throw new ImpostorCheatException($"Client attempted to send unowned {nameof(InnerCustomNetworkTransform)} data");
                }

                var positions = reader.ReadPackedInt32();

                for (var i = 0; i < positions; i++)
                {
                    var position = reader.ReadVector2();
                    var newSid = (ushort)(sequenceId + i);
                    if (SidGreaterThan(newSid, _lastSequenceId))
                    {
                        _lastSequenceId = newSid;
                        Position = position;
                    }
                }
            }
        }

        private void SnapTo(Vector2 position, ushort minSid)
        {
            if (!SidGreaterThan(minSid, _lastSequenceId))
            {
                return;
            }

            _lastSequenceId = minSid;
            Position = position;
        }
    }
}
