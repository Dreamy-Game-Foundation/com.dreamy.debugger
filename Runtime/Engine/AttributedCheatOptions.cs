using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Presentation;

namespace Dreamy.Debugger
{
    /// <summary>Reflect once at registration; values and actions use the authorized registry.</summary>
    public static class AttributedCheatOptions
    {
        public static IEnumerable<CheatOption> From(object target, string prefix)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("A container needs an ID prefix.", nameof(prefix));

            foreach (var member in target.GetType().GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                if (member.GetCustomAttribute<BrowsableAttribute>()?.Browsable == false) continue;

                var card = member.GetCustomAttribute<CheatCardAttribute>();
                var id = member.GetCustomAttribute<CheatIdAttribute>()?.Id ?? card?.OptionId ?? prefix + "." + member.Name;
                var category = member.GetCustomAttribute<CategoryAttribute>()?.Category ?? "Default";
                var label = member.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? card?.DisplayName ?? member.Name;
                var order = member.GetCustomAttribute<SortAttribute>()?.SortPriority ?? 0;
                CheatOption option;

                if (member is PropertyInfo property && property.GetGetMethod() != null && property.GetIndexParameters().Length == 0)
                {
                    var range = property.GetCustomAttribute<NumberRangeAttribute>();
                    var step = property.GetCustomAttribute<IncrementAttribute>();
                    var setter = property.GetSetMethod();
                    option = CheatOption.BoundValue(id, category, label, property.PropertyType,
                        () => property.GetValue(target),
                        setter == null ? (Action<object>)null : value => property.SetValue(target, value),
                        range?.Min, range?.Max).WithPresentation(order, step?.Increment);
                }
                else if (member is MethodInfo method && method.ReturnType == typeof(void) &&
                         method.GetParameters().Length == 0 && method.DeclaringType != typeof(object) && !method.IsSpecialName)
                {
                    option = CheatOption.Button(id, category, label, () => method.Invoke(target, null),
                            card != null || method.IsDefined(typeof(CheatConfirmationAttribute), true))
                        .WithPresentation(order);
                }
                else
                {
                    if (card != null) throw new ArgumentException("Card member must be a public readable property or parameterless void action: " + member.Name);
                    continue;
                }

                if (card != null)
                    option.WithCard(card.Card.Id, card.Card.Title, card.Card.Kind, card.Card.Role);
                yield return option;
            }
        }
    }
}
