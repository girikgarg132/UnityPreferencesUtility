using System;
using System.Globalization;
using UnityEngine;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>Emits C# literal expressions for default values.</summary>
    public static class CSharpLiterals
    {
        private const string Separator = ", ";
        private const string UnityPrefix = "global::UnityEngine.";
        private const string SystemPrefix = "global::System.";

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary><c>true</c> or <c>false</c>.</summary>
        public static string Bool(bool value) => value ? "true" : "false";

        /// <summary>Integer literal without suffix (also valid for byte, sbyte, short and ushort constants).</summary>
        public static string Int(long value) => value.ToString(Invariant);

        /// <summary><c>123U</c>.</summary>
        public static string UInt(uint value) => value.ToString(Invariant) + "U";

        /// <summary><c>123L</c>.</summary>
        public static string Long(long value) => value.ToString(Invariant) + "L";

        /// <summary><c>123UL</c>.</summary>
        public static string ULong(ulong value) => value.ToString(Invariant) + "UL";

        /// <summary>Float literal, including NaN and infinities.</summary>
        public static string Float(float value)
        {
            if (float.IsNaN(value)) return "float.NaN";
            if (float.IsPositiveInfinity(value)) return "float.PositiveInfinity";
            if (float.IsNegativeInfinity(value)) return "float.NegativeInfinity";
            return PrefsConverter.Format(value) + "f";
        }

        /// <summary>Double literal, including NaN and infinities.</summary>
        public static string Double(double value)
        {
            if (double.IsNaN(value)) return "double.NaN";
            if (double.IsPositiveInfinity(value)) return "double.PositiveInfinity";
            if (double.IsNegativeInfinity(value)) return "double.NegativeInfinity";
            return PrefsConverter.Format(value) + "d";
        }

        /// <summary><c>1.5m</c>.</summary>
        public static string Decimal(decimal value) => value.ToString(Invariant) + "m";

        /// <summary><c>new global::System.DateTime(ticks, kind)</c>.</summary>
        public static string DateTime(DateTime value) =>
            $"new {SystemPrefix}DateTime({Long(value.Ticks)}, {SystemPrefix}DateTimeKind.{value.Kind})";

        /// <summary><c>new global::System.DateTimeOffset(ticks, offset)</c>.</summary>
        public static string DateTimeOffset(DateTimeOffset value) =>
            $"new {SystemPrefix}DateTimeOffset({Long(value.Ticks)}, new {SystemPrefix}TimeSpan({Long(value.Offset.Ticks)}))";

        /// <summary><c>new global::System.TimeSpan(ticks)</c>.</summary>
        public static string TimeSpan(TimeSpan value) => $"new {SystemPrefix}TimeSpan({Long(value.Ticks)})";

        /// <summary><c>new global::System.Guid("...")</c>.</summary>
        public static string Guid(Guid value) => $"new {SystemPrefix}Guid({CSharpSyntax.StringLiteral(PrefsConverter.Format(value))})";

        /// <summary>Vector2 constructor.</summary>
        public static string Vector2(Vector2 v) => New("Vector2", Float(v.x), Float(v.y));

        /// <summary>Vector3 constructor.</summary>
        public static string Vector3(Vector3 v) => New("Vector3", Float(v.x), Float(v.y), Float(v.z));

        /// <summary>Vector4 constructor.</summary>
        public static string Vector4(Vector4 v) => New("Vector4", Float(v.x), Float(v.y), Float(v.z), Float(v.w));

        /// <summary>Vector2Int constructor.</summary>
        public static string Vector2Int(Vector2Int v) => New("Vector2Int", Int(v.x), Int(v.y));

        /// <summary>Vector3Int constructor.</summary>
        public static string Vector3Int(Vector3Int v) => New("Vector3Int", Int(v.x), Int(v.y), Int(v.z));

        /// <summary>Quaternion constructor.</summary>
        public static string Quaternion(Quaternion q) => New("Quaternion", Float(q.x), Float(q.y), Float(q.z), Float(q.w));

        /// <summary>Color constructor.</summary>
        public static string Color(Color c) => New("Color", Float(c.r), Float(c.g), Float(c.b), Float(c.a));

        /// <summary>Color32 constructor.</summary>
        public static string Color32(Color32 c) => New("Color32", Int(c.r), Int(c.g), Int(c.b), Int(c.a));

        /// <summary>Rect constructor.</summary>
        public static string Rect(Rect r) => New("Rect", Float(r.x), Float(r.y), Float(r.width), Float(r.height));

        /// <summary>RectInt constructor.</summary>
        public static string RectInt(RectInt r) => New("RectInt", Int(r.x), Int(r.y), Int(r.width), Int(r.height));

        /// <summary>Bounds constructor.</summary>
        public static string Bounds(Bounds b) => New("Bounds", Vector3(b.center), Vector3(b.size));

        /// <summary>BoundsInt constructor.</summary>
        public static string BoundsInt(BoundsInt b) => New("BoundsInt",
            Int(b.position.x), Int(b.position.y), Int(b.position.z), Int(b.size.x), Int(b.size.y), Int(b.size.z));

        private static string New(string unityType, params string[] arguments) =>
            $"new {UnityPrefix}{unityType}({string.Join(Separator, arguments)})";
    }
}
