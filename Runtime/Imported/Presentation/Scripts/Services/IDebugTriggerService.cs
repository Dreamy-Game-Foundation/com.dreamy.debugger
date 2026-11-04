namespace Dreamy.Debugger.Presentation.Services
{
    public interface IDebugTriggerService
    {
        bool IsEnabled { get; set; }
        bool ShowErrorNotification { get; set; }
        PinAlignment Position { get; set; }
    }
}
