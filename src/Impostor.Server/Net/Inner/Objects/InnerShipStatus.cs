using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Impostor.Api;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Api.Net.Messages;
using Impostor.Server.Net.Inner.Objects.Systems;
using Impostor.Server.Net.Inner.Objects.Systems.ShipStatus;
using Impostor.Server.Net.State;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Net.Inner.Objects
{
    internal partial class InnerShipStatus : InnerNetObject
    {
        private readonly ILogger<InnerShipStatus> _logger;
        private readonly Game _game;
        private readonly Dictionary<SystemTypes, ISystemType> _systems;

        public enum RpcCalls
        {
            CloseDoorsOfType = 0,
            RepairSystem = 1,
            SetWinners = 2,
        }

        public InnerShipStatus(ILogger<InnerShipStatus> logger, Game game)
        {
            _logger = logger;
            _game = game;

            _systems = new Dictionary<SystemTypes, ISystemType>
            {
                [SystemTypes.Electrical] = new SwitchSystem(),
                [SystemTypes.MedBay] = new MedScanSystem(),
                [SystemTypes.Reactor] = new ReactorSystemType(),
                [SystemTypes.LifeSupp] = new LifeSuppSystemType(),
                [SystemTypes.Security] = new SecurityCameraSystemType(),
                [SystemTypes.Comms] = new HudOverrideSystemType(),
                [SystemTypes.Doors] = new DoorsSystemType(_game),
            };

            _systems.Add(SystemTypes.Sabotage, new SabotageSystemType(new[]
            {
                (IActivatable)_systems[SystemTypes.Comms],
                (IActivatable)_systems[SystemTypes.Reactor],
                (IActivatable)_systems[SystemTypes.LifeSupp],
                (IActivatable)_systems[SystemTypes.Electrical],
            }));

            Components.Add(this);
        }

        public override ValueTask HandleRpc(ClientPlayer sender, ClientPlayer? target, byte call,
            IMessageReader reader)
        {
            _logger.LogWarning($"Handling RPC {(RpcCalls)call} ({(int)call}) in {sender.Client.Name}");

            switch ((RpcCalls)call)
            {
                case RpcCalls.CloseDoorsOfType:
                {
                    if (target == null || !target.IsHost)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.CloseDoorsOfType)} to wrong destinition, must be host");
                    }

                    var systemType = (SystemTypes)reader.ReadByte();

                    break;
                }

                case RpcCalls.RepairSystem:
                {
                    if (target == null || !target.IsHost)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.RepairSystem)} to wrong destinition, must be host");
                    }

                    var systemType = (SystemTypes)reader.ReadByte();
                    /*
                    if (systemType == SystemTypes.Sabotage)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.RepairSystem)} for {systemType} as crewmate");
                    }
                    */

                    var player = reader.ReadNetObject<InnerPlayerControl>(_game);
                    var amount = reader.ReadByte();

                    // TODO: Modify data (?)
                    break;
                }

                case RpcCalls.SetWinners:
                {
                    if (!sender.IsHost)
                    {
                        throw new ImpostorCheatException("Only host can end game");
                    }

                    var playerList = reader.ReadBytesAndSize().ToArray();

                    _logger.LogInformation($"Host sent game over with winners {string.Join(", ", playerList.Select(b => b.ToString()))}");
                    break;
                }

                default:
                {
                    //_logger.LogWarning("{0}: Unknown rpc call {1}", nameof(InnerShipStatus), call);
                    break;
                }
            }

            return default;
        }

        public override bool Serialize(IMessageWriter writer, bool initialState)
        {
            throw new NotImplementedException();
        }

        public override void Deserialize(IClientPlayer sender, IClientPlayer? target, IMessageReader reader, bool initialState)
        {
            if (!sender.IsHost)
            {
                throw new ImpostorCheatException($"Client attempted to send data for {nameof(InnerShipStatus)} as non-host");
            }

            if (target != null)
            {
                throw new ImpostorCheatException($"Client attempted to send {nameof(InnerShipStatus)} data to a specific player, must be broadcast");
            }

            if (initialState)
            {
                // TODO: (_systems[SystemTypes.Doors] as DoorsSystemType).SetDoors();
                foreach (var systemType in SystemTypeHelpers.AllTypes)
                {
                    if (_systems.TryGetValue(systemType, out var system))
                    {
                        system.Deserialize(reader, true);
                    }
                }
            }
            else
            {
                var count = reader.ReadPackedUInt32();

                foreach (var systemType in SystemTypeHelpers.AllTypes)
                {
                    // TODO: Not sure what is going on here, check.
                    if ((count & 1 << (int)(systemType & (SystemTypes.ShipTasks | SystemTypes.Doors))) != 0L)
                    {
                        if (_systems.TryGetValue(systemType, out var system))
                        {
                            system.Deserialize(reader, false);
                        }
                    }
                }
            }
        }
    }
}
