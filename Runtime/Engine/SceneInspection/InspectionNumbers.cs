using System;
using System.Globalization;
using UnityEngine;

namespace Dreamy.Debugger.SceneInspection
{
    public static class InspectionNumbers
    {
        public static int Axes(Type t) => t == typeof(Vector2) || t == typeof(Vector2Int) ? 2 :
            t == typeof(Vector3) || t == typeof(Vector3Int) ? 3 : t == typeof(Vector4) ? 4 : 0;
        public static bool Scalar(Type t) => t == typeof(float) || t == typeof(double) || t == typeof(decimal) ||
            t == typeof(byte) || t == typeof(sbyte) || t == typeof(short) || t == typeof(ushort) ||
            t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong);
        public static bool Integral(Type t) => t != typeof(float) && t != typeof(double) && t != typeof(decimal);
        public static string AxisText(object value, int axis) => InspectionValues.Format(Axis(value, axis));
        private static object Axis(object value, int axis)
        {
            if (value is Vector2 a) return a[axis];
            if (value is Vector3 b) return b[axis];
            if (value is Vector4 c) return c[axis];
            if (value is Vector2Int d) return d[axis];
            if (value is Vector3Int e) return e[axis];
            return value;
        }
        public static bool TryAxis(object source, int axis, string text, out object value, out string error)
        {
            value = source;
            var integer = source is Vector2Int || source is Vector3Int;
            if (!InspectionValues.TryParse(text, integer ? typeof(int) : typeof(float), out var parsed, out error)) return false;
            if (source is Vector2 a) { a[axis] = (float)parsed; value = a; }
            else if (source is Vector3 b) { b[axis] = (float)parsed; value = b; }
            else if (source is Vector4 c) { c[axis] = (float)parsed; value = c; }
            else if (source is Vector2Int d) { d[axis] = (int)parsed; value = d; }
            else if (source is Vector3Int e) { e[axis] = (int)parsed; value = e; }
            else { error = "Unsupported vector"; return false; }
            return true;
        }
        public static bool TryOffset(object source, int axis, float distance, out object value, out string error)
        {
            value = null; error = null;
            try
            {
                var original = axis < 0 ? source : Axis(source, axis);
                var type = original.GetType();
                var amount = Integral(type) ? Math.Truncate(distance / 6d) : distance * .02d;
                string text;
                if (type == typeof(double) || type == typeof(float))
                    text = (Convert.ToDouble(original, CultureInfo.InvariantCulture) + amount).ToString("R", CultureInfo.InvariantCulture);
                else
                    text = checked(Convert.ToDecimal(original, CultureInfo.InvariantCulture) + (decimal)amount).ToString(CultureInfo.InvariantCulture);
                return axis < 0 ? InspectionValues.TryParse(text, type, out value, out error) : TryAxis(source, axis, text, out value, out error);
            }
            catch (Exception e) { error = e.Message; return false; }
        }
    }
}
