using System.ComponentModel;
using Dreamy.Debugger.Foundation.Service;
using UnityEngine;
using UnityEngine.Scripting;

public delegate void DebugOptionsPropertyChanged(object sender, string propertyName);

#if !DISABLE_DREAMY_DEBUGGER
[Preserve]
#endif
public partial class DebugOptions : INotifyPropertyChanged
{
    private static DebugOptions _current;

    public static DebugOptions Current
    {
        get { return _current; }
    }

#if !DISABLE_DREAMY_DEBUGGER
    public static void OnStartup()
    {
        _current = new DebugOptions(); // Need to reset options here so if we enter play-mode without a domain reload there will be the default set of options.
        ServiceManager.GetService<Dreamy.Debugger.Presentation.Internal.InternalOptionsRegistry>().AddOptionContainer(Current);
    }
#endif

    public event DebugOptionsPropertyChanged PropertyChanged;
    
#if UNITY_EDITOR
    [JetBrains.Annotations.NotifyPropertyChangedInvocator]
#endif
    public void OnPropertyChanged(string propertyName)
    {
        if (PropertyChanged != null)
        {
            PropertyChanged(this, propertyName);
        }

        if (InterfacePropertyChangedEventHandler != null)
        {
            InterfacePropertyChangedEventHandler(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    private event PropertyChangedEventHandler InterfacePropertyChangedEventHandler;

    event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
    {
        add { InterfacePropertyChangedEventHandler += value; }
        remove { InterfacePropertyChangedEventHandler -= value; }
    }
}
