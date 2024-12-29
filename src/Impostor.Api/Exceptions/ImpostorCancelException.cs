using System;
using System.Runtime.Serialization;

namespace Impostor.Api
{
    public class ImpostorCancelException : ImpostorException
    {
        public ImpostorCancelException()
        {
        }

        protected ImpostorCancelException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public ImpostorCancelException(string? message) : base(message)
        {
        }

        public ImpostorCancelException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}