using System;
using System.Linq;
using System.Collections.Generic;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Presentation;
namespace Dreamy.Debugger {
    internal sealed class OptionsPresentationBridge : IDisposable {
        private readonly CheatRegistry registry;private readonly DebuggerRuntime owner;
        private readonly List<OptionDefinition> definitions=new List<OptionDefinition>();
        private readonly List<Dreamy.Debugger.Foundation.Helpers.PropertyReference> values=new List<Dreamy.Debugger.Foundation.Helpers.PropertyReference>();
        private DynamicOptionContainer container;
        private readonly Dictionary<CheatOption,OptionDefinition> bindings=new Dictionary<CheatOption,OptionDefinition>();
        private double nextPoll;
        private bool structureDirty;
        private void QueueStructureRefresh(){structureDirty=true;}
        private int generation;
        public void Tick(double now) {
            if(structureDirty || generation!=Dreamy.Debugger.Foundation.Service.ServiceManager.Generation){Refresh();}
            if(registry.FlushValueNotifications())nextPoll=now+.25;
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
        public OptionsPresentationBridge(DebuggerRuntime owner,CheatRegistry registry){this.owner=owner;this.registry=registry;registry.Changed+=QueueStructureRefresh;registry.ValuesChanged+=RefreshValues;Refresh();}
        public void Refresh(){
            structureDirty=false;
            var nextGeneration=Dreamy.Debugger.Foundation.Service.ServiceManager.Generation;
            var presentation=DreamyDebug.Instance;
            if(presentation==null || generation!=nextGeneration){container=null;bindings.Clear();definitions.Clear();values.Clear();}
            generation=nextGeneration;
            if(presentation==null)return;
            if(container==null){container=new DynamicOptionContainer();presentation.AddOptionContainer(container);}
            using(var batch=container.BeginUpdate()){
            var snapshot=registry.Snapshot();
            var live=new HashSet<CheatOption>(snapshot);
            foreach(var stale in bindings.Keys.Where(option=>!live.Contains(option)).ToArray()){
                container.RemoveOption(bindings[stale]);bindings.Remove(stale);
            }
            foreach(var option in snapshot){
                if(bindings.ContainsKey(option))continue;
                OptionDefinition definition;
                if(option.IsAction)definition=OptionDefinition.FromMethod(option.DisplayName,()=>{
                    EnsureCurrent(option);
                    if(option.RequiresConfirmation)Confirm(option);else registry.Execute(option.Id);
                },option.Category,option.Order);
                else {
                    var attributes=new List<Attribute>();
                    if(option.Minimum.HasValue||option.Maximum.HasValue)attributes.Add(new NumberRangeAttribute(option.Minimum??double.MinValue,option.Maximum??double.MaxValue));
                    if(option.Increment.HasValue)attributes.Add(new IncrementAttribute(option.Increment.Value));
                    var property=new Dreamy.Debugger.Foundation.Helpers.PropertyReference(option.ValueType,
                        ()=>{EnsureCurrent(option);return registry.Read(option.Id);},
                        option.IsReadOnly?(Action<object>)null:value=>{EnsureCurrent(option);registry.Set(option.Id,value);},attributes.ToArray());
                    definition=new OptionDefinition(option.DisplayName,option.Category,option.Order,property);
                }
                definition.WithIdentity(option.Id,option.Card);
                bindings.Add(option,definition);container.AddOption(definition);
            }
            definitions.Clear();definitions.AddRange(snapshot.Select(option=>bindings[option]));
            values.Clear();values.AddRange(definitions.Where(d=>d.IsProperty).Select(d=>d.Property));
            }
        }
        private void EnsureCurrent(CheatOption option){
            if(!registry.IsRegistered(option))throw new InvalidOperationException("The cheat owner has been removed or replaced.");
        }
        private void Confirm(CheatOption option){
            CheatOption amount=null;object expected=null;var label=option.DisplayName;
            if(option.Card!=null && option.Card.Kind==CheatCardKind.Resource){
                amount=registry.Snapshot().FirstOrDefault(o=>o.Category==option.Category && o.Card!=null && o.Card.Id==option.Card.Id && o.Card.Role==CheatCardRole.Amount);
                if(amount!=null){expected=registry.Read(amount.Id);label+=" · "+Convert.ToString(expected,System.Globalization.CultureInfo.InvariantCulture);}
            }
            owner.Confirm(label,()=>registry.Execute(option.Id,true),()=>{
                var current=registry.Snapshot();
                return current.Contains(option) && (amount==null || current.Contains(amount) && Equals(expected,registry.Read(amount.Id)));
            });
        }
        public void Dispose(){registry.Changed-=QueueStructureRefresh;registry.ValuesChanged-=RefreshValues;if(container!=null && DreamyDebug.IsInitialized)DreamyDebug.Instance?.RemoveOptionContainer(container);container=null;bindings.Clear();definitions.Clear();values.Clear();}
    }
}
