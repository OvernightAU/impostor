namespace Impostor.Api.Net.Messages.RPC
{
    public static class RpcCustomRoleRpc
    {
        public static void Serialize(IMessageWriter writer, string name)
        {
            writer.Write(name);
        }

        public static void Deserialize(IMessageReader reader, out string name)
        {
            name = reader.ReadString();
        }
    }
}
