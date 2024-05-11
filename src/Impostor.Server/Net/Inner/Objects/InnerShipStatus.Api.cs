using System.Threading.Tasks;
using Impostor.Api.Innersloth;
using Impostor.Api.Net.Inner;
using Impostor.Api.Net.Inner.Objects;

namespace Impostor.Server.Net.Inner.Objects
{
    internal partial class InnerShipStatus : IInnerShipStatus
    {
        public async ValueTask EndGameCustomAsync(byte[] winners, string reason, string audioPath, string hex)
        {
            using var writer = _game.StartRpc(NetId, (byte)RpcCalls.EndGameCustom);
            writer.WriteBytesAndSize(winners);
            writer.Write(reason);
            writer.Write(audioPath);
            writer.Write(hex);
            await _game.FinishRpcAsync(writer);
        }
    }
}
