using System;
using System.Collections.Generic;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Presentation;
namespace Dreamy.Debugger {
    internal sealed class OptionsPresentationBridge : IDisposable {
        private readonly CheatRegistry registry;private readonly DebuggerRuntime owner;
        private readonly List<OptionDefinition> definitions=new List<OptionDefinition>();
        private readonly List<Dreamy.Debugger.Foundation.Helpers.PropertyReference> values=new List<Dreamy.Debugger.Foundation.Helpers.PropertyReference>();
        private SnapshotContainer container;
        private double nextPoll;
        private int generation;
        public void Tick(double now) {
            if(generation!=Dreamy.Debugger.Foundation.Service.ServiceManager.Generation){Refresh();}
            if(now<nextPoll || !owner.CanCheat)return;
            nextPoll=now+.25;
            var ui=DreamyDebug.Instance;
            if(ui==null)return;
            var visible=ui.IsDebugPanelVisible;
            if(!visible && Dreamy.Debugger.Foundation.Service.ServiceManager.HasService<Dreamy.Debugger.Presentation.Services.IPinnedUIService>()){
                var pinned=Dreamy.Debugger.Foundation.Service.ServiceManager.GetService<Dreamy.Debugger.Presentation.Services.IPinnedUIService>();
                foreach(var definition in definitions)if(pinned.HasPinned(definition)){visible=true;break;}
            }
            if(visible)RefreshValues();
        }
        private void RefreshValues(){
            if(!owner.CanCheat)return;
            foreach(var property in values){try{property.NotifyValueChanged();}catch(Exception){/* One broken getter must not stop the other controls. */}}
        }
        public OptionsPresentationBridge(DebuggerRuntime owner,CheatRegistry registry){this.owner=owner;this.registry=registry;registry.Changed+=Refresh;registry.ValuesChanged+=RefreshValues;Refresh();}
        public void Refresh(){
            generation=Dreamy.Debugger.Foundation.Service.ServiceManager.Generation;
            var presentation=DreamyDebug.Instance;
            if(presentation==null){container=null;definitions.Clear();values.Clear();return;}
            if(container!=null)presentation.RemoveOptionContainer(container);definitions.Clear();values.Clear();
            foreach(var option in registry.Snapshot()){
                OptionDefinition definition;
                if(option.IsAction)definition=OptionDefinition.FromMethod(option.DisplayName,()=>{
                    if(option.RequiresConfirmation)owner.Confirm(option.DisplayName,()=>registry.Execute(option.Id,true));else registry.Execute(option.Id);
                },option.Category,option.Order);
                else {
                    var attributes=new List<Attribute>();
                    if(option.Minimum.HasValue||option.Maximum.HasValue)attributes.Add(new NumberRangeAttribute(option.Minimum??double.MinValue,option.Maximum??double.MaxValue));
                    if(option.Increment.HasValue)attributes.Add(new IncrementAttribute(option.Increment.Value));
                    var property=new Dreamy.Debugger.Foundation.Helpers.PropertyReference(option.ValueType,()=>registry.Read(option.Id),option.IsReadOnly?(Action<object>)null:value=>registry.Set(option.Id,value),attributes.ToArray());
                    values.Add(property);
                    definition=new OptionDefinition(option.DisplayName,option.Category,option.Order,property);
                }
                definitions.Add(definition);
            }
            container=new SnapshotContainer(definitions.ToArray());presentation.AddOptionContainer(container);
        }
        public void Dispose(){registry.Changed-=Refresh;registry.ValuesChanged-=RefreshValues;if(container!=null && DreamyDebug.IsInitialized)DreamyDebug.Instance?.RemoveOptionContainer(container);container=null;definitions.Clear();values.Clear();}
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
