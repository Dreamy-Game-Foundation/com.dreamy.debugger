namespace Dreamy.Debugger.Presentation.Services
{
    public interface IDockConsoleService
    {
        bool IsVisible { get; set; }
        bool IsExpanded { get; set; }
        ConsoleAlignment Alignment { get; set; }
    }
}
