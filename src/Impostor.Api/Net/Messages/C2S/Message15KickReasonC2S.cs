namespace Impostor.Api.Net.Messages.C2S
{
    public class Message15KickReasonC2S
    {
        public static void Serialize(IMessageWriter writer)
        {
            throw new System.NotImplementedException();
        }

        public static void Deserialize(IMessageReader reader, out int playerId, out string reason)
        {
            playerId = reader.ReadPackedInt32();
            reason = reader.ReadString();
        }
    }
}
