using System;

namespace Dreamy.Debugger.Options
{
    /// <summary>Typed option with stable ID; authorization is enforced again at execution.</summary>
    public sealed class CheatOption
    {
        private readonly Func<object> getter;
        private readonly Action<object> setter;
        private readonly Action action;
        public int Order {get;private set;}
        public double? Increment {get;private set;}
        public CheatOption WithPresentation(int order=0,double? increment=null){if(increment.HasValue && (increment<=0||double.IsNaN(increment.Value)||double.IsInfinity(increment.Value)))throw new ArgumentException("Invalid step");Order=order;Increment=increment;return this;}
        public static CheatOption BoundValue(string id,string category,string name,Type type,Func<object> getter,Action<object> setter=null,double? minimum=null,double? maximum=null){
            if(!(type.IsEnum || type==typeof(bool)||type==typeof(string)||type==typeof(byte)||type==typeof(sbyte)||type==typeof(short)||type==typeof(ushort)||type==typeof(uint)||type==typeof(int)||type==typeof(long)||type==typeof(float)||type==typeof(double)))throw new NotSupportedException(type.FullName);
            if ((minimum.HasValue || maximum.HasValue) && (type==typeof(bool) || type==typeof(string) || type.IsEnum)) throw new ArgumentException("Only numeric options support ranges.");
            if(getter==null)throw new ArgumentNullException(nameof(getter));return new CheatOption(id,category,name,type,getter,setter,null,false,minimum,maximum);
        }
        public string Id { get; }
        public string Category { get; }
        public string DisplayName { get; }
        public Type ValueType { get; }
        public bool IsAction => action != null;
        public bool IsReadOnly => setter == null && !IsAction;
        public bool RequiresConfirmation { get; }
        public double? Minimum { get; }
        public double? Maximum { get; }
        internal CheatOption(string id, string category, string name, Type type, Func<object> getter,
            Action<object> setter, Action action, bool confirmation, double? minimum, double? maximum)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Options need stable ID, category and name.");
            if (minimum.HasValue && (double.IsNaN(minimum.Value) || double.IsInfinity(minimum.Value)) ||
                maximum.HasValue && (double.IsNaN(maximum.Value) || double.IsInfinity(maximum.Value)) ||
                minimum > maximum) throw new ArgumentException("Invalid option range.");
            Id = id; Category = category; DisplayName = name; ValueType = type;
            this.getter = getter; this.setter = setter; this.action = action;
            RequiresConfirmation = confirmation; Minimum = minimum; Maximum = maximum;
        }
        public static CheatOption Value<T>(string id, string category, string name, Func<T> getter,
            Action<T> setter = null, double? minimum = null, double? maximum = null)
        {
            var t = typeof(T);
            if (!(t == typeof(bool) || t == typeof(string) || t == typeof(int) || t == typeof(long) ||
                t == typeof(float) || t == typeof(double) || t.IsEnum)) throw new NotSupportedException(t.FullName);
            if (getter == null) throw new ArgumentNullException(nameof(getter));
            if ((minimum.HasValue || maximum.HasValue) && (t == typeof(bool) || t == typeof(string) || t.IsEnum))
                throw new ArgumentException("Only numeric options support ranges.");
            return new CheatOption(id, category, name, t, () => getter(),
                setter == null ? (Action<object>)null : v => setter((T)v), null, false, minimum, maximum);
        }
        public static CheatOption Button(string id, string category, string name, Action action, bool requiresConfirmation = false)
        {
            return new CheatOption(id, category, name, typeof(void), null, null,
                action ?? throw new ArgumentNullException(nameof(action)), requiresConfirmation, null, null);
        }
        internal object Read() { if (IsAction) throw new InvalidOperationException("Action has no value."); return getter(); }
        internal void Write(object value)
        {
            if (setter == null) throw new InvalidOperationException("Option is read-only.");
            if (value == null ? ValueType != typeof(string) : value.GetType() != ValueType)
                throw new ArgumentException("Value type does not match option.");
            if (ValueType.IsEnum && !Enum.IsDefined(ValueType, value)) throw new ArgumentException("Unknown enum value.");
            if (ValueType == typeof(byte) || ValueType == typeof(sbyte) || ValueType == typeof(short) || ValueType == typeof(ushort) || ValueType == typeof(uint) || ValueType == typeof(int) || ValueType == typeof(long) || ValueType == typeof(float) || ValueType == typeof(double))
            {
                var number = Convert.ToDouble(value);
                if (double.IsNaN(number) || double.IsInfinity(number) || Minimum.HasValue && number < Minimum ||
                    Maximum.HasValue && number > Maximum) throw new ArgumentOutOfRangeException(nameof(value));
            }
            setter(value);
        }
        internal void Invoke(bool confirmed)
        {
            if (!IsAction) throw new InvalidOperationException("Not an action.");
            if (RequiresConfirmation && !confirmed) throw new InvalidOperationException("Confirmation required.");
            action();
        }
    }
}
