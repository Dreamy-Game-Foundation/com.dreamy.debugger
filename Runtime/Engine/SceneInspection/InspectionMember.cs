using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dreamy.Debugger.SceneInspection
{
    /// <summary>A live member path; every access resolves parents again, including collection elements.</summary>
    public sealed class InspectionMember
    {
        private interface IStep
        {
            object Get(object owner);
            void Set(object owner, object value);
            bool Writable { get; }
        }
        private sealed class FieldStep : IStep
        {
            public FieldInfo Field;
            public object Get(object owner) => Field.GetValue(owner);
            public void Set(object owner, object value) => Field.SetValue(owner, value);
            public bool Writable => !Field.IsInitOnly;
        }
        private sealed class AdapterStep : IStep
        {
            public PropertyInfo Property;
            public bool Editable;
            public object Get(object owner) => Property.GetValue(owner, null);
            public void Set(object owner, object value) => Property.SetValue(owner, value, null);
            public bool Writable => Editable && Property.CanWrite;
        }
        private sealed class ElementStep : IStep
        {
            public int Index;
            public object Get(object owner) => ((IList)owner)[Index];
            public void Set(object owner, object value) => ((IList)owner)[Index] = value;
            public bool Writable => true;
        }
        private readonly Object root;
        private readonly IStep[] steps;
        public string Name { get; }
        public Type ValueType { get; }
        public bool Writable { get; }
        private InspectionMember(Object root, string name, Type type, IStep[] steps)
        {
            this.root = root; Name = name; ValueType = type; this.steps = steps;
            Writable = true;
            foreach (var step in steps) if (!step.Writable) Writable = false;
        }
        public static InspectionMember Field(Object owner, FieldInfo field) =>
            new InspectionMember(owner, field.Name, field.FieldType, new IStep[] { new FieldStep { Field = field } });
        public static InspectionMember Adapter(Object owner, string name, bool editable)
        {
            var property = owner.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property == null) throw new ArgumentException("Unknown adapter property: " + name);
            return new InspectionMember(owner, name, property.PropertyType, new IStep[] { new AdapterStep { Property = property, Editable = editable } });
        }
        public InspectionMember Child(FieldInfo field) => Append(field.Name, field.FieldType, new FieldStep { Field = field });
        public InspectionMember Element(int index, Type type) => Append("[" + index + "]", type, new ElementStep { Index = index });
        private InspectionMember Append(string name, Type type, IStep step)
        {
            var path = new IStep[steps.Length + 1]; Array.Copy(steps, path, steps.Length); path[path.Length - 1] = step;
            return new InspectionMember(root, name, type, path);
        }
        public bool TryRead(out object value, out string error)
        {
            value = null; error = null;
            if (root == null) { error = "Object destroyed"; return false; }
            try { value = root; foreach (var step in steps) value = step.Get(value); return true; }
            catch (Exception e) { error = (e.InnerException ?? e).Message; return false; }
        }
        public bool TryCommit(object baseline, object value, Func<bool> policy, out string error)
        {
            error = null;
            if (!Writable || !InspectionValues.IsEditable(ValueType) || !policy()) { error = "Editing is locked"; return false; }
            if (!TryRead(out var current, out error)) return false;
            if (!Equals(current, baseline)) { error = "Value changed in game. Reload before applying again."; return false; }
            try
            {
                if (value == null ? ValueType.IsValueType : value.GetType() != ValueType) throw new ArgumentException("Value type mismatch");
                if (!policy()) { error = "Editing is locked"; return false; }
                Write(root, 0, value); return true;
            }
            catch (Exception e) { error = (e.InnerException ?? e).Message; return false; }
        }
        private object Write(object owner, int depth, object value)
        {
            var step = steps[depth];
            if (depth == steps.Length - 1) step.Set(owner, value);
            else
            {
                var child = step.Get(owner);
                var changed = Write(child, depth + 1, value);
                // Boxed structs must be assigned all the way back to their owning field/list slot.
                if (child != null && child.GetType().IsValueType) step.Set(owner, changed);
            }
            return owner;
        }
    }

    public sealed class InspectionDescriptors
    {
        private readonly Dictionary<Type, FieldInfo[]> cache = new Dictionary<Type, FieldInfo[]>();
        public FieldInfo[] Fields(Type type)
        {
            if (cache.TryGetValue(type, out var known)) return known;
            var fields = new List<FieldInfo>();
            for (var current = type; current != null && current != typeof(MonoBehaviour) && current != typeof(Component) && current != typeof(Object); current = current.BaseType)
                foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (!field.IsStatic && !field.IsLiteral && !field.IsNotSerialized &&
                        (field.IsPublic || field.IsDefined(typeof(SerializeField), true)) &&
                        !field.IsDefined(typeof(HideInInspector), true) && !field.IsDefined(typeof(ObsoleteAttribute), true)) fields.Add(field);
            known = fields.ToArray();
            if (cache.Count >= 128) cache.Clear();
            cache[type] = known; return known;
        }
        public void Clear() => cache.Clear();
    }

    public static class InspectionValues
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        public static bool IsEditable(Type type) => type == typeof(bool) || type == typeof(string) || type.IsEnum ||
            type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) ||
            type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong) ||
            type == typeof(float) || type == typeof(double) || type == typeof(decimal) ||
            type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4) ||
            type == typeof(Vector2Int) || type == typeof(Vector3Int) || type == typeof(Color);
        public static Type ElementType(Type type)
        {
            if (type.IsArray && type.GetArrayRank() == 1) return type.GetElementType();
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) ? type.GetGenericArguments()[0] : null;
        }
        public static bool IsComposite(Type type) => !IsEditable(type) && !typeof(Object).IsAssignableFrom(type) &&
            !typeof(Delegate).IsAssignableFrom(type) && !typeof(IDictionary).IsAssignableFrom(type) &&
            !type.IsArray && !type.IsGenericType && type.IsSerializable;
        public static string Format(object value)
        {
            if (value == null) return "null";
            if (value is Object unity) return unity == null ? "Destroyed" : unity.name + " (" + unity.GetType().Name + ")";
            if (value is Vector2 v2) return Join(v2.x, v2.y);
            if (value is Vector3 v3) return Join(v3.x, v3.y, v3.z);
            if (value is Vector4 v4) return Join(v4.x, v4.y, v4.z, v4.w);
            if (value is Vector2Int i2) return i2.x.ToString(Invariant) + ", " + i2.y.ToString(Invariant);
            if (value is Vector3Int i3) return i3.x.ToString(Invariant) + ", " + i3.y.ToString(Invariant) + ", " + i3.z.ToString(Invariant);
            if (value is Color color) return Join(color.r, color.g, color.b, color.a);
            if (value is float f) return f.ToString("R", Invariant);
            if (value is double d) return d.ToString("R", Invariant);
            if (IsEditable(value.GetType())) return Convert.ToString(value, Invariant);
            if (ElementType(value.GetType()) != null && value is IList collection) return value.GetType().Name + " / " + collection.Count + " items";
            return value.GetType().Name + " / read only";
        }
        private static string Join(params float[] values)
        {
            var text = new string[values.Length];
            for (var i = 0; i < values.Length; i++) text[i] = values[i].ToString("R", Invariant);
            return string.Join(", ", text);
        }
        public static bool TryParse(string text, Type type, out object value, out string error)
        {
            value = null; error = null;
            try
            {
                if (!IsEditable(type)) throw new ArgumentException("Unsupported type");
                if (type == typeof(string)) { if (text.Length > 4096) throw new ArgumentException("Maximum 4096 characters"); value = text; }
                else if (type.IsEnum) value = Enum.Parse(type, text, true);
                else if (type == typeof(Vector2Int) || type == typeof(Vector3Int))
                {
                    var parts = text.Split(','); var count = type == typeof(Vector2Int) ? 2 : 3;
                    if (parts.Length != count) throw new ArgumentException("Expected " + count + " comma-separated integers");
                    var x = int.Parse(parts[0], Invariant); var y = int.Parse(parts[1], Invariant);
                    value = count == 2 ? (object)new Vector2Int(x, y) : new Vector3Int(x, y, int.Parse(parts[2], Invariant));
                }
                else if (type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4) || type == typeof(Color))
                {
                    var parts = text.Split(','); var count = type == typeof(Vector2) ? 2 : type == typeof(Vector3) ? 3 : 4;
                    if (parts.Length != count) throw new ArgumentException("Expected " + count + " comma-separated numbers");
                    var numbers = new float[count];
                    for (var i = 0; i < count; i++) { numbers[i] = float.Parse(parts[i], NumberStyles.Float, Invariant); if (float.IsNaN(numbers[i]) || float.IsInfinity(numbers[i])) throw new ArgumentException("Numbers must be finite"); }
                    if (type == typeof(Vector2)) value = new Vector2(numbers[0], numbers[1]);
                    else if (type == typeof(Vector3)) value = new Vector3(numbers[0], numbers[1], numbers[2]);
                    else if (type == typeof(Vector4)) value = new Vector4(numbers[0], numbers[1], numbers[2], numbers[3]);
                    else value = new Color(numbers[0], numbers[1], numbers[2], numbers[3]);
                }
                else value = Convert.ChangeType(text, type, Invariant);
                if (value is float f && (float.IsNaN(f) || float.IsInfinity(f)) || value is double d && (double.IsNaN(d) || double.IsInfinity(d))) throw new ArgumentException("Numbers must be finite");
                return true;
            }
            catch (Exception e) { error = e.Message; value = null; return false; }
        }
    }
}
