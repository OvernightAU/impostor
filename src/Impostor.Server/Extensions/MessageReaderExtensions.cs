using System.Numerics;
using Impostor.Api.Net.Messages;
using Impostor.Api.Unity;
using Impostor.Server.Net.Inner;
using Impostor.Server.Net.State;

namespace Impostor.Server
{
    internal static class MessageReaderExtensions
    {
        public static T ReadNetObject<T>(this IMessageReader reader, Game game)
            where T : InnerNetObject
        {
            return game.FindObjectByNetId<T>(reader.ReadPackedUInt32());
        }

        public static Vector2 ReadVector2(this IMessageReader reader)
        {
            const float range = 50f;

            var x = reader.ReadUInt16() / (float)ushort.MaxValue;
            var y = reader.ReadUInt16() / (float)ushort.MaxValue;

            return new Vector2(Mathf.Lerp(-range, range, x), Mathf.Lerp(-range, range, y));
        }
    }
}
