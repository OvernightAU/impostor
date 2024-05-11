using System.Numerics;
using Impostor.Api.Games;
using Impostor.Api.Net.Messages;
using Impostor.Api.Unity;

namespace Impostor.Server
{
    internal static class MessageWriterExtensions
    {
        public static void Serialize(this GameCode gameCode, IMessageWriter writer)
        {
            writer.Write(gameCode.Value);
        }

        public static void Write(this IMessageWriter writer, Vector2 vector)
        {
            writer.Write((ushort)(Mathf.ReverseLerp(vector.X) * (double)ushort.MaxValue));
            writer.Write((ushort)(Mathf.ReverseLerp(vector.Y) * (double)ushort.MaxValue));
        }
    }
}
