using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Presentation;
namespace Dreamy.Debugger {
    /// <summary>Project-owned partial containers register through the same authorized registry as typed options.</summary>
    public static class AttributedCheatOptions {
        public static IEnumerable<CheatOption> From(object target,string prefix){
            if(target==null)throw new ArgumentNullException(nameof(target));
            foreach(var member in target.GetType().GetMembers(BindingFlags.Public|BindingFlags.Instance)){
                if(member.GetCustomAttribute<BrowsableAttribute>()?.Browsable==false)continue;
                var category=member.GetCustomAttribute<CategoryAttribute>()?.Category??"Default";
                var label=member.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName??member.Name;
                var order=member.GetCustomAttribute<SortAttribute>()?.SortPriority??0;
                var id=prefix+"."+member.Name;
                if(member is PropertyInfo property && property.GetGetMethod()!=null && property.GetIndexParameters().Length==0){
                    var range=property.GetCustomAttribute<NumberRangeAttribute>();var step=property.GetCustomAttribute<IncrementAttribute>();
                    var setter=property.GetSetMethod();
                    yield return CheatOption.BoundValue(id,category,label,property.PropertyType,()=>property.GetValue(target),setter==null?(Action<object>)null:value=>property.SetValue(target,value),range?.Min,range?.Max).WithPresentation(order,step?.Increment);
                }else if(member is MethodInfo method && method.ReturnType==typeof(void)&&method.GetParameters().Length==0 && method.DeclaringType!=typeof(object)&&!method.IsSpecialName){
                    yield return CheatOption.Button(id,category,label,()=>method.Invoke(target,null),true).WithPresentation(order);
                }
            }
        }
    }
}
