using System;

public partial class DebugOptions
{
    // For compatibility with older versions of Dreamy.Debugger.Presentation, this simply inherits from the component model version.

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class DisplayNameAttribute : System.ComponentModel.DisplayNameAttribute
    {
        public DisplayNameAttribute(string displayName) : base(displayName)
        {
        }
    }

    // These attributes are used when using DebugOptions. Options added via DreamyDebug.Instance.AddOptionsContainer can use the attribute defined in Dreamy.Debugger.Presentation namespace.

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class IncrementAttribute :
#if DISABLE_DREAMY_DEBUGGER
        Attribute
#else
        Dreamy.Debugger.Presentation.IncrementAttribute
#endif
    {
        public IncrementAttribute(double increment)
#if !DISABLE_DREAMY_DEBUGGER
            : base(increment)
#endif
        {
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class NumberRangeAttribute :
#if DISABLE_DREAMY_DEBUGGER
        Attribute
#else
        Dreamy.Debugger.Presentation.NumberRangeAttribute
#endif
    {
        public NumberRangeAttribute(double min, double max)
#if !DISABLE_DREAMY_DEBUGGER
            : base(min, max)
#endif
        {
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class SortAttribute :
#if DISABLE_DREAMY_DEBUGGER
        Attribute
#else
        Dreamy.Debugger.Presentation.SortAttribute
#endif
    {
        public SortAttribute(int priority)
#if !DISABLE_DREAMY_DEBUGGER
            : base(priority)
#endif
        {
        }
    }
}