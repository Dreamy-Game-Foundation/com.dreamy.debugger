using System;
using System.Collections.Generic;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Presentation;
namespace Dreamy.Debugger {
    internal sealed class OptionsPresentationBridge : IDisposable {
        private readonly CheatRegistry registry;private readonly DebuggerRuntime owner;
        private readonly List<OptionDefinition> definitions=new List<OptionDefinition>();
        public OptionsPresentationBridge(DebuggerRuntime owner,CheatRegistry registry){this.owner=owner;this.registry=registry;registry.Changed+=Refresh;Refresh();}
        public void Refresh(){
            foreach(var definition in definitions)DreamyDebug.Instance.RemoveOption(definition);definitions.Clear();
            foreach(var option in registry.Snapshot()){
                OptionDefinition definition;
                if(option.IsAction)definition=OptionDefinition.FromMethod(option.DisplayName,()=>{
                    if(option.RequiresConfirmation)owner.Confirm(option.DisplayName,()=>registry.Execute(option.Id,true));else registry.Execute(option.Id);
                },option.Category);
                else if(option.ValueType==typeof(bool))definition=Value<bool>(option);
                else if(option.ValueType==typeof(int))definition=Value<int>(option);
                else if(option.ValueType==typeof(long))definition=Value<long>(option);
                else if(option.ValueType==typeof(float))definition=Value<float>(option);
                else if(option.ValueType==typeof(double))definition=Value<double>(option);
                else if(option.ValueType==typeof(string))definition=Value<string>(option);
                else { // Enum conversion remains typed at the registry boundary; the imported enum UI uses its real runtime Type.
                    var property=new Dreamy.Debugger.Foundation.Helpers.PropertyReference(option.ValueType,()=>registry.Read(option.Id),option.IsReadOnly?(Action<object>)null:value=>registry.Set(option.Id,value));
                    definition=new OptionDefinition(option.DisplayName,option.Category,0,property);
                }
                definitions.Add(definition);DreamyDebug.Instance.AddOption(definition);
            }
        }
        private OptionDefinition Value<T>(CheatOption option)=>OptionDefinition.Create(option.DisplayName,()=> (T)registry.Read(option.Id),option.IsReadOnly?(Action<T>)null:v=>registry.Set(option.Id,v),option.Category);
        public void Dispose(){registry.Changed-=Refresh;foreach(var d in definitions)if(DreamyDebug.IsInitialized)DreamyDebug.Instance.RemoveOption(d);definitions.Clear();}
        private sealed class EnumAdapter {
            private readonly CheatRegistry registry;private readonly CheatOption option;
            public EnumAdapter(CheatRegistry registry,CheatOption option){this.registry=registry;this.option=option;}
            public object Value {get=>registry.Read(option.Id);set=>registry.Set(option.Id,value);}
        }
    }
}
