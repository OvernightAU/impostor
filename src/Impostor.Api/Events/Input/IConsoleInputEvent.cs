// TODO: Add plugins support.
namespace Impostor.Api.Events.Input
{
    public interface IConsoleInputEvent : IEvent
    {
        string Input { get; }
    }
}
