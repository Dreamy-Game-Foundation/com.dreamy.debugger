using System;
using System.Collections.Generic;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Presentation;
namespace Dreamy.Debugger {
    internal sealed class OptionsPresentationBridge : IDisposable {
        private readonly CheatRegistry registry;private readonly DebuggerRuntime owner;
        private readonly List<OptionDefinition> definitions=new List<OptionDefinition>();
        private SnapshotContainer container;
        public OptionsPresentationBridge(DebuggerRuntime owner,CheatRegistry registry){this.owner=owner;this.registry=registry;registry.Changed+=Refresh;Refresh();}
        public void Refresh(){
            if(container!=null)DreamyDebug.Instance.RemoveOptionContainer(container);definitions.Clear();
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
                definitions.Add(definition);
            }
            container=new SnapshotContainer(definitions.ToArray());DreamyDebug.Instance.AddOptionContainer(container);
        }
        private OptionDefinition Value<T>(CheatOption option){
            var attributes=option.Minimum.HasValue || option.Maximum.HasValue
                ?new Attribute[]{new NumberRangeAttribute(option.Minimum??double.MinValue,option.Maximum??double.MaxValue)}
                :Array.Empty<Attribute>();
            var property=Dreamy.Debugger.Foundation.Helpers.PropertyReference.FromLambda(
                ()=> (T)registry.Read(option.Id),option.IsReadOnly?(Action<T>)null:v=>registry.Set(option.Id,v),attributes);
            return new OptionDefinition(option.DisplayName,option.Category,0,property);
        }
        public void Dispose(){registry.Changed-=Refresh;if(container!=null && DreamyDebug.IsInitialized)DreamyDebug.Instance.RemoveOptionContainer(container);container=null;definitions.Clear();}
        private sealed class SnapshotContainer : IOptionContainer {
            private readonly OptionDefinition[] options;
            public SnapshotContainer(OptionDefinition[] options){this.options=options;}
            public IEnumerable<OptionDefinition> GetOptions()=>options;
            public bool IsDynamic=>false;
            public event Action<OptionDefinition> OptionAdded {add{} remove{}}
            public event Action<OptionDefinition> OptionRemoved {add{} remove{}}
        }
    }
}
