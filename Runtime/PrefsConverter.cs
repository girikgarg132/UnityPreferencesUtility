using System;
using System.Globalization;
using UnityEngine;

namespace GirikGarg.PreferencesUtility
{
    /// <summary>
    /// Culture-invariant, allocation-light conversions between preference values and their stored representation.
    /// Generated accessors call this class for types that <see cref="PlayerPrefs"/> cannot store natively.
    /// Every <c>ToX</c> method returns the supplied fallback when the stored text is missing or malformed,
    /// so corrupted or legacy data never throws at runtime.
    /// </summary>
    public static class PrefsConverter
    {
        private const char ComponentSeparator = ',';
        private const string ComponentSeparatorText = ",";
        private const string RoundTripFormat = "R";
        private const string SingleFallbackFormat = "G9";
        private const string DoubleFallbackFormat = "G17";
        private const string DateTimeFormat = "o";
        private const string TimeSpanFormat = "c";
        private const string GuidFormat = "D";
        private const string TrueText = "true";
        private const string FalseText = "false";
        private const string TrueNumericText = "1";
        private const string FalseNumericText = "0";
        private const int Vector2ComponentCount = 2;
        private const int Vector3ComponentCount = 3;
        private const int Vector4ComponentCount = 4;
        private const int RectComponentCount = 4;
        private const int BoundsComponentCount = 6;
        private const int Color32ComponentCount = 4;
        private const int RedShift = 24;
        private const int GreenShift = 16;
        private const int BlueShift = 8;
        private const int ByteMask = 0xFF;
        private const NumberStyles IntegerStyle = NumberStyles.Integer;
        private const NumberStyles FloatStyle = NumberStyles.Float;
        private const NumberStyles DecimalStyle = NumberStyles.Number;

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        #region Primitive formatting

        /// <summary>Formats a boolean as <c>true</c> or <c>false</c>.</summary>
        public static string Format(bool value) => value ? TrueText : FalseText;

        /// <summary>Formats a signed byte using the invariant culture.</summary>
        public static string Format(sbyte value) => value.ToString(Invariant);

        /// <summary>Formats a byte using the invariant culture.</summary>
        public static string Format(byte value) => value.ToString(Invariant);

        /// <summary>Formats a 16-bit integer using the invariant culture.</summary>
        public static string Format(short value) => value.ToString(Invariant);

        /// <summary>Formats an unsigned 16-bit integer using the invariant culture.</summary>
        public static string Format(ushort value) => value.ToString(Invariant);

        /// <summary>Formats a 32-bit integer using the invariant culture.</summary>
        public static string Format(int value) => value.ToString(Invariant);

        /// <summary>Formats an unsigned 32-bit integer using the invariant culture.</summary>
        public static string Format(uint value) => value.ToString(Invariant);

        /// <summary>Formats a 64-bit integer using the invariant culture.</summary>
        public static string Format(long value) => value.ToString(Invariant);

        /// <summary>Formats an unsigned 64-bit integer using the invariant culture.</summary>
        public static string Format(ulong value) => value.ToString(Invariant);

        /// <summary>Formats a single-precision number with a lossless round-trip representation.</summary>
        public static string Format(float value)
        {
            string text = value.ToString(RoundTripFormat, Invariant);
            return float.TryParse(text, FloatStyle, Invariant, out float parsed) && parsed.Equals(value)
                ? text
                : value.ToString(SingleFallbackFormat, Invariant);
        }

        /// <summary>Formats a double-precision number with a lossless round-trip representation.</summary>
        public static string Format(double value)
        {
            string text = value.ToString(RoundTripFormat, Invariant);
            return double.TryParse(text, FloatStyle, Invariant, out double parsed) && parsed.Equals(value)
                ? text
                : value.ToString(DoubleFallbackFormat, Invariant);
        }

        /// <summary>Formats a decimal using the invariant culture.</summary>
        public static string Format(decimal value) => value.ToString(Invariant);

        /// <summary>Formats a character as a one-character string.</summary>
        public static string Format(char value) => value.ToString();

        /// <summary>Returns the string itself, or an empty string for <c>null</c>.</summary>
        public static string Format(string value) => value ?? string.Empty;

        /// <summary>Formats a <see cref="DateTime"/> with the ISO 8601 round-trip format (preserves <see cref="DateTimeKind"/>).</summary>
        public static string Format(DateTime value) => value.ToString(DateTimeFormat, Invariant);

        /// <summary>Formats a <see cref="DateTimeOffset"/> with the ISO 8601 round-trip format.</summary>
        public static string Format(DateTimeOffset value) => value.ToString(DateTimeFormat, Invariant);

        /// <summary>Formats a <see cref="TimeSpan"/> with the constant (<c>c</c>) format.</summary>
        public static string Format(TimeSpan value) => value.ToString(TimeSpanFormat, Invariant);

        /// <summary>Formats a <see cref="Guid"/> as 32 hyphen-separated digits.</summary>
        public static string Format(Guid value) => value.ToString(GuidFormat, Invariant);

        #endregion

        #region Unity formatting

        /// <summary>Formats a <see cref="Vector2"/> as <c>x,y</c>.</summary>
        public static string Format(Vector2 value) => Join(value.x, value.y);

        /// <summary>Formats a <see cref="Vector3"/> as <c>x,y,z</c>.</summary>
        public static string Format(Vector3 value) => Join(value.x, value.y, value.z);

        /// <summary>Formats a <see cref="Vector4"/> as <c>x,y,z,w</c>.</summary>
        public static string Format(Vector4 value) => Join(value.x, value.y, value.z, value.w);

        /// <summary>Formats a <see cref="Vector2Int"/> as <c>x,y</c>.</summary>
        public static string Format(Vector2Int value) => Join(value.x, value.y);

        /// <summary>Formats a <see cref="Vector3Int"/> as <c>x,y,z</c>.</summary>
        public static string Format(Vector3Int value) => Join(value.x, value.y, value.z);

        /// <summary>Formats a <see cref="Quaternion"/> as <c>x,y,z,w</c>.</summary>
        public static string Format(Quaternion value) => Join(value.x, value.y, value.z, value.w);

        /// <summary>Formats a <see cref="Color"/> as <c>r,g,b,a</c>.</summary>
        public static string Format(Color value) => Join(value.r, value.g, value.b, value.a);

        /// <summary>Formats a <see cref="Color32"/> as <c>r,g,b,a</c> (0-255).</summary>
        public static string Format(Color32 value) => Join(value.r, value.g, value.b, value.a);

        /// <summary>Formats a <see cref="Rect"/> as <c>x,y,width,height</c>.</summary>
        public static string Format(Rect value) => Join(value.x, value.y, value.width, value.height);

        /// <summary>Formats a <see cref="RectInt"/> as <c>x,y,width,height</c>.</summary>
        public static string Format(RectInt value) => Join(value.x, value.y, value.width, value.height);

        /// <summary>Formats <see cref="Bounds"/> as <c>centerX,centerY,centerZ,sizeX,sizeY,sizeZ</c>.</summary>
        public static string Format(Bounds value)
        {
            Vector3 center = value.center;
            Vector3 size = value.size;
            return string.Join(ComponentSeparatorText, Format(center.x), Format(center.y), Format(center.z),
                Format(size.x), Format(size.y), Format(size.z));
        }

        /// <summary>Formats <see cref="BoundsInt"/> as <c>x,y,z,sizeX,sizeY,sizeZ</c>.</summary>
        public static string Format(BoundsInt value)
        {
            Vector3Int position = value.position;
            Vector3Int size = value.size;
            return string.Join(ComponentSeparatorText, Format(position.x), Format(position.y), Format(position.z),
                Format(size.x), Format(size.y), Format(size.z));
        }

        #endregion

        #region Primitive parsing

        /// <summary>Parses <c>true</c>/<c>false</c> or <c>1</c>/<c>0</c>; returns <paramref name="fallback"/> otherwise.</summary>
        public static bool ToBoolean(string raw, bool fallback)
        {
            if (string.IsNullOrEmpty(raw)) return fallback;
            if (bool.TryParse(raw, out bool parsed)) return parsed;
            if (raw == TrueNumericText) return true;
            if (raw == FalseNumericText) return false;
            return fallback;
        }

        /// <summary>Parses a signed byte; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static sbyte ToSByte(string raw, sbyte fallback) =>
            sbyte.TryParse(raw, IntegerStyle, Invariant, out sbyte parsed) ? parsed : fallback;

        /// <summary>Parses a byte; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static byte ToByte(string raw, byte fallback) =>
            byte.TryParse(raw, IntegerStyle, Invariant, out byte parsed) ? parsed : fallback;

        /// <summary>Parses a 16-bit integer; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static short ToInt16(string raw, short fallback) =>
            short.TryParse(raw, IntegerStyle, Invariant, out short parsed) ? parsed : fallback;

        /// <summary>Parses an unsigned 16-bit integer; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static ushort ToUInt16(string raw, ushort fallback) =>
            ushort.TryParse(raw, IntegerStyle, Invariant, out ushort parsed) ? parsed : fallback;

        /// <summary>Parses a 32-bit integer; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static int ToInt32(string raw, int fallback) =>
            int.TryParse(raw, IntegerStyle, Invariant, out int parsed) ? parsed : fallback;

        /// <summary>Parses an unsigned 32-bit integer; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static uint ToUInt32(string raw, uint fallback) =>
            uint.TryParse(raw, IntegerStyle, Invariant, out uint parsed) ? parsed : fallback;

        /// <summary>Parses a 64-bit integer; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static long ToInt64(string raw, long fallback) =>
            long.TryParse(raw, IntegerStyle, Invariant, out long parsed) ? parsed : fallback;

        /// <summary>Parses an unsigned 64-bit integer; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static ulong ToUInt64(string raw, ulong fallback) =>
            ulong.TryParse(raw, IntegerStyle, Invariant, out ulong parsed) ? parsed : fallback;

        /// <summary>Parses a single-precision number; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static float ToSingle(string raw, float fallback) =>
            float.TryParse(raw, FloatStyle, Invariant, out float parsed) ? parsed : fallback;

        /// <summary>Parses a double-precision number; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static double ToDouble(string raw, double fallback) =>
            double.TryParse(raw, FloatStyle, Invariant, out double parsed) ? parsed : fallback;

        /// <summary>Parses a decimal; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static decimal ToDecimal(string raw, decimal fallback) =>
            decimal.TryParse(raw, DecimalStyle, Invariant, out decimal parsed) ? parsed : fallback;

        /// <summary>Parses a one-character string; returns <paramref name="fallback"/> otherwise.</summary>
        public static char ToChar(string raw, char fallback) =>
            raw != null && raw.Length == 1 ? raw[0] : fallback;

        /// <summary>Returns <paramref name="raw"/>, or <paramref name="fallback"/> when it is <c>null</c>.</summary>
        public static string ToString(string raw, string fallback) => raw ?? fallback;

        /// <summary>Parses an ISO 8601 round-trip <see cref="DateTime"/>; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static DateTime ToDateTime(string raw, DateTime fallback) =>
            !string.IsNullOrEmpty(raw) && DateTime.TryParse(raw, Invariant, DateTimeStyles.RoundtripKind, out DateTime parsed)
                ? parsed
                : fallback;

        /// <summary>Parses an ISO 8601 <see cref="DateTimeOffset"/>; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static DateTimeOffset ToDateTimeOffset(string raw, DateTimeOffset fallback) =>
            !string.IsNullOrEmpty(raw) && DateTimeOffset.TryParse(raw, Invariant, DateTimeStyles.RoundtripKind, out DateTimeOffset parsed)
                ? parsed
                : fallback;

        /// <summary>Parses a constant-format <see cref="TimeSpan"/>; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static TimeSpan ToTimeSpan(string raw, TimeSpan fallback) =>
            !string.IsNullOrEmpty(raw) && TimeSpan.TryParseExact(raw, TimeSpanFormat, Invariant, out TimeSpan parsed)
                ? parsed
                : fallback;

        /// <summary>Parses a <see cref="Guid"/>; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static Guid ToGuid(string raw, Guid fallback) =>
            !string.IsNullOrEmpty(raw) && Guid.TryParse(raw, out Guid parsed) ? parsed : fallback;

        #endregion

        #region Unity parsing

        /// <summary>Parses <c>x,y</c>; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static Vector2 ToVector2(string raw, Vector2 fallback)
        {
            Span<float> values = stackalloc float[Vector2ComponentCount];
            return TryParseComponents(raw, values) ? new Vector2(values[0], values[1]) : fallback;
        }

        /// <summary>Parses <c>x,y,z</c>; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static Vector3 ToVector3(string raw, Vector3 fallback)
        {
            Span<float> values = stackalloc float[Vector3ComponentCount];
            return TryParseComponents(raw, values) ? new Vector3(values[0], values[1], values[2]) : fallback;
        }

        /// <summary>Parses <c>x,y,z,w</c>; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static Vector4 ToVector4(string raw, Vector4 fallback)
        {
            Span<float> values = stackalloc float[Vector4ComponentCount];
            return TryParseComponents(raw, values) ? new Vector4(values[0], values[1], values[2], values[3]) : fallback;
        }

        /// <summary>Parses <c>x,y</c>; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static Vector2Int ToVector2Int(string raw, Vector2Int fallback)
        {
            Span<int> values = stackalloc int[Vector2ComponentCount];
            return TryParseComponents(raw, values) ? new Vector2Int(values[0], values[1]) : fallback;
        }

        /// <summary>Parses <c>x,y,z</c>; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static Vector3Int ToVector3Int(string raw, Vector3Int fallback)
        {
            Span<int> values = stackalloc int[Vector3ComponentCount];
            return TryParseComponents(raw, values) ? new Vector3Int(values[0], values[1], values[2]) : fallback;
        }

        /// <summary>Parses <c>x,y,z,w</c>; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static Quaternion ToQuaternion(string raw, Quaternion fallback)
        {
            Span<float> values = stackalloc float[Vector4ComponentCount];
            return TryParseComponents(raw, values) ? new Quaternion(values[0], values[1], values[2], values[3]) : fallback;
        }

        /// <summary>Parses <c>r,g,b,a</c>; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static Color ToColor(string raw, Color fallback)
        {
            Span<float> values = stackalloc float[Vector4ComponentCount];
            return TryParseComponents(raw, values) ? new Color(values[0], values[1], values[2], values[3]) : fallback;
        }

        /// <summary>Parses <c>r,g,b,a</c> (0-255); returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static Color32 ToColor32(string raw, Color32 fallback)
        {
            Span<int> values = stackalloc int[Color32ComponentCount];
            if (!TryParseComponents(raw, values)) return fallback;
            for (int i = 0; i < Color32ComponentCount; i++)
            {
                if (values[i] < byte.MinValue || values[i] > byte.MaxValue) return fallback;
            }

            return new Color32((byte)values[0], (byte)values[1], (byte)values[2], (byte)values[3]);
        }

        /// <summary>Parses <c>x,y,width,height</c>; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static Rect ToRect(string raw, Rect fallback)
        {
            Span<float> values = stackalloc float[RectComponentCount];
            return TryParseComponents(raw, values) ? new Rect(values[0], values[1], values[2], values[3]) : fallback;
        }

        /// <summary>Parses <c>x,y,width,height</c>; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static RectInt ToRectInt(string raw, RectInt fallback)
        {
            Span<int> values = stackalloc int[RectComponentCount];
            return TryParseComponents(raw, values) ? new RectInt(values[0], values[1], values[2], values[3]) : fallback;
        }

        /// <summary>Parses <c>centerX,centerY,centerZ,sizeX,sizeY,sizeZ</c>; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static Bounds ToBounds(string raw, Bounds fallback)
        {
            Span<float> values = stackalloc float[BoundsComponentCount];
            return TryParseComponents(raw, values)
                ? new Bounds(new Vector3(values[0], values[1], values[2]), new Vector3(values[3], values[4], values[5]))
                : fallback;
        }

        /// <summary>Parses <c>x,y,z,sizeX,sizeY,sizeZ</c>; returns <paramref name="fallback"/> when missing or malformed.</summary>
        public static BoundsInt ToBoundsInt(string raw, BoundsInt fallback)
        {
            Span<int> values = stackalloc int[BoundsComponentCount];
            return TryParseComponents(raw, values)
                ? new BoundsInt(values[0], values[1], values[2], values[3], values[4], values[5])
                : fallback;
        }

        #endregion

        #region Color32 packing

        /// <summary>Packs a <see cref="Color32"/> into a single integer (RGBA, 8 bits per channel) so it fits one <c>SetInt</c> call.</summary>
        public static int PackColor32(Color32 value) =>
            unchecked((value.r << RedShift) | (value.g << GreenShift) | (value.b << BlueShift) | value.a);

        /// <summary>Unpacks an integer produced by <see cref="PackColor32"/>.</summary>
        public static Color32 UnpackColor32(int packed) => new Color32(
            (byte)((packed >> RedShift) & ByteMask),
            (byte)((packed >> GreenShift) & ByteMask),
            (byte)((packed >> BlueShift) & ByteMask),
            (byte)(packed & ByteMask));

        #endregion

        #region Helpers

        private static string Join(float a, float b) =>
            string.Concat(Format(a), ComponentSeparatorText, Format(b));

        private static string Join(float a, float b, float c) =>
            string.Join(ComponentSeparatorText, Format(a), Format(b), Format(c));

        private static string Join(float a, float b, float c, float d) =>
            string.Join(ComponentSeparatorText, Format(a), Format(b), Format(c), Format(d));

        private static string Join(int a, int b) =>
            string.Concat(Format(a), ComponentSeparatorText, Format(b));

        private static string Join(int a, int b, int c) =>
            string.Join(ComponentSeparatorText, Format(a), Format(b), Format(c));

        private static string Join(int a, int b, int c, int d) =>
            string.Join(ComponentSeparatorText, Format(a), Format(b), Format(c), Format(d));

        private static bool TryParseComponents(string raw, Span<float> values)
        {
            if (string.IsNullOrEmpty(raw)) return false;

            ReadOnlySpan<char> remaining = raw.AsSpan();
            int lastIndex = values.Length - 1;
            for (int i = 0; i <= lastIndex; i++)
            {
                if (!TrySliceComponent(ref remaining, i == lastIndex, out ReadOnlySpan<char> part)) return false;
                if (!float.TryParse(part, FloatStyle, Invariant, out values[i])) return false;
            }

            return true;
        }

        private static bool TryParseComponents(string raw, Span<int> values)
        {
            if (string.IsNullOrEmpty(raw)) return false;

            ReadOnlySpan<char> remaining = raw.AsSpan();
            int lastIndex = values.Length - 1;
            for (int i = 0; i <= lastIndex; i++)
            {
                if (!TrySliceComponent(ref remaining, i == lastIndex, out ReadOnlySpan<char> part)) return false;
                if (!int.TryParse(part, IntegerStyle, Invariant, out values[i])) return false;
            }

            return true;
        }

        private static bool TrySliceComponent(ref ReadOnlySpan<char> remaining, bool isLast, out ReadOnlySpan<char> part)
        {
            int separatorIndex = remaining.IndexOf(ComponentSeparator);
            if (isLast)
            {
                part = remaining;
                return separatorIndex < 0;
            }

            if (separatorIndex < 0)
            {
                part = default;
                return false;
            }

            part = remaining.Slice(0, separatorIndex);
            remaining = remaining.Slice(separatorIndex + 1);
            return true;
        }

        #endregion
    }
}
