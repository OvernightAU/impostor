using System.Threading.Tasks;
using Impostor.Api;
using Impostor.Api.Innersloth;
using Impostor.Api.Innersloth.Customization;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Api.Net.Inner.Objects.Components;
using Impostor.Server.Events.Player;

namespace Impostor.Server.Net.Inner.Objects
{
    internal partial class InnerPlayerControl : IInnerPlayerControl
    {
        IInnerPlayerPhysics IInnerPlayerControl.Physics => Physics;

        IInnerCustomNetworkTransform IInnerPlayerControl.NetworkTransform => NetworkTransform;

        IInnerPlayerInfo IInnerPlayerControl.PlayerInfo => PlayerInfo;

        public async ValueTask SetNameAsync(string name)
        {
            PlayerInfo.PlayerName = name;

            using var writer = _game.StartRpc(NetId, (byte)RpcCalls.SetName);
            writer.Write(name);
            await _game.FinishRpcAsync(writer);
        }

        public async ValueTask SetColorAsync(byte colorId)
        {
            PlayerInfo.ColorId = colorId;

            using var writer = _game.StartRpc(NetId, (byte)RpcCalls.SetColor);
            writer.Write(colorId);
            await _game.FinishRpcAsync(writer);
        }

        public ValueTask SetColorAsync(ColorType colorType)
        {
            return SetColorAsync((byte)colorType);
        }

        public async ValueTask SetHatAsync(string hatId)
        {
            PlayerInfo.HatId = hatId;

            using var writer = _game.StartRpc(NetId, (byte)RpcCalls.SetHat);
            writer.Write(hatId);
            await _game.FinishRpcAsync(writer);
        }

        public async ValueTask SetPetAsync(string petId)
        {
            PlayerInfo.PetId = petId;

            using var writer = _game.StartRpc(NetId, (byte)RpcCalls.SetPet);
            writer.Write(petId);
            await _game.FinishRpcAsync(writer);
        }

        public async ValueTask SetSkinAsync(string skinId)
        {
            PlayerInfo.SkinId = skinId;

            using var writer = _game.StartRpc(NetId, (byte)RpcCalls.SetSkin);
            writer.Write(skinId);
            await _game.FinishRpcAsync(writer);
        }

        public async ValueTask SendChatAsync(string text)
        {
            using var writer = _game.StartRpc(NetId, (byte)RpcCalls.SendChat);
            writer.Write(text);
            await _game.FinishRpcAsync(writer);
        }

        public async ValueTask SendChatToPlayerAsync(string text, IInnerPlayerControl? player = null)
        {
            if (player == null)
            {
                player = this;
            }

            using var writer = _game.StartRpc(NetId, (byte)RpcCalls.SendChat);
            writer.Write(text);
            await _game.FinishRpcAsync(writer, player.OwnerId);
        }

        public async ValueTask SetMurderedByAsync(IClientPlayer impostor)
        {
            if (impostor.Character == null)
            {
                throw new ImpostorException("Character is null.");
            }

            if (impostor.Character.PlayerInfo.IsDead)
            {
                throw new ImpostorProtocolException("Plugin tried to murder a player while the impostor specified was dead.");
            }

            if (PlayerInfo.IsDead)
            {
                return;
            }

            // Update player.
            Die(DeathReason.Kill);

            // Send RPC.
            using var writer = _game.StartRpc(impostor.Character.NetId, (byte)RpcCalls.MurderPlayer);
            writer.WritePacked(NetId);
            await _game.FinishRpcAsync(writer);

            // Notify plugins.
            await _eventManager.CallAsync(new PlayerMurderEvent(_game, impostor, impostor.Character, this));
        }
    }
}
