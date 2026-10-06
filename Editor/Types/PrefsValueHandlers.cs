using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GirikGarg.PreferencesUtility.Editor
{
    /// <summary>Registry of every selectable value type, in menu order.</summary>
    public static class PrefsValueHandlers
    {
        /// <summary>Id of the type used for new entries.</summary>
        public const string DefaultId = "bool";

        /// <summary>Id of the enum handler.</summary>
        public const string EnumId = "Enum";

        /// <summary>Id of the Json handler.</summary>
        public const string JsonId = "Json";

        private const string IntGetter = "{P}.GetInt({K}, {D})";
        private const string IntSetter = "{P}.SetInt({K}, value)";
        private const string StringSetter = "{P}.SetString({K}, {C}.Format(value))";
        private const string StoredString = "{P}.GetString({K}, string.Empty)";
        private const string IntegersMenu = "Integers/";
        private const string NumbersMenu = "Numbers/";
        private const string SystemMenu = "System/";
        private const string UnityMenu = "Unity/";

        private static readonly List<PrefsValueHandler> s_All = CreateAll();
        private static readonly Dictionary<string, PrefsValueHandler> s_ById = s_All.ToDictionary(handler => handler.Id);
        private static readonly string[] s_MenuPaths = s_All.Select(handler => handler.MenuPath).ToArray();

        /// <summary>All handlers in menu order.</summary>
        public static IReadOnlyList<PrefsValueHandler> All => s_All;

        /// <summary>Menu paths, index-aligned with <see cref="All"/>.</summary>
        public static string[] MenuPaths => s_MenuPaths;

        /// <summary>Returns the handler for <paramref name="id"/>, or <c>null</c>.</summary>
        public static PrefsValueHandler Get(string id) =>
            id != null && s_ById.TryGetValue(id, out PrefsValueHandler handler) ? handler : null;

        /// <summary>Index of <paramref name="id"/> in <see cref="All"/>, or -1.</summary>
        public static int IndexOf(string id) => s_All.FindIndex(handler => handler.Id == id);

        /// <summary>Returns the built-in handler that natively supports <paramref name="type"/>, or <c>null</c>.</summary>
        public static PrefsValueHandler FindBuiltIn(Type type)
        {
            if (type == null) return null;
            if (type.IsEnum) return Get(EnumId);

            foreach (PrefsValueHandler handler in s_All)
            {
                if (!handler.UsesTypeName && handler.GetValueType(null) == type) return handler;
            }

            return null;
        }

        private static string Parsed(string converterMethod) => "{C}." + converterMethod + "(" + StoredString + ", {D})";

        private static List<PrefsValueHandler> CreateAll()
        {
            const PrefsDefaultDeclaration constant = PrefsDefaultDeclaration.Const;
            const PrefsDefaultDeclaration readOnly = PrefsDefaultDeclaration.StaticReadonly;

            return new List<PrefsValueHandler>
            {
                new BuiltInValueHandler<bool>("bool", "bool", PrefsStorageSlot.Bool, false, true,
                    PrefsConverter.ToBoolean, PrefsConverter.Format, (l, v) => EditorGUILayout.Toggle(l, v),
                    CSharpLiterals.Bool, constant, "{P}.GetInt({K}, {D} ? 1 : 0) != 0", "{P}.SetInt({K}, value ? 1 : 0)")
                {
                    EditorGetter = "{P}.GetBool({K}, {D})",
                    EditorSetter = "{P}.SetBool({K}, value)"
                },
                new BuiltInValueHandler<int>("int", "int", PrefsStorageSlot.Int, 0, 1,
                    PrefsConverter.ToInt32, PrefsConverter.Format, (l, v) => EditorGUILayout.IntField(l, v),
                    v => CSharpLiterals.Int(v), constant, IntGetter, IntSetter),
                new BuiltInValueHandler<float>("float", "float", PrefsStorageSlot.Float, 0f, 1f,
                    PrefsConverter.ToSingle, PrefsConverter.Format, (l, v) => EditorGUILayout.FloatField(l, v),
                    CSharpLiterals.Float, constant, "{P}.GetFloat({K}, {D})", "{P}.SetFloat({K}, value)"),
                new BuiltInValueHandler<string>("string", "string", PrefsStorageSlot.String, string.Empty, string.Empty,
                    PrefsConverter.ToString, PrefsConverter.Format, (l, v) => EditorGUILayout.TextField(l, v),
                    CSharpSyntax.StringLiteral, constant, "{P}.GetString({K}, {D})", "{P}.SetString({K}, value ?? string.Empty)"),

                new BuiltInValueHandler<byte>("byte", IntegersMenu + "byte", PrefsStorageSlot.Int, 0, 1,
                    PrefsConverter.ToByte, PrefsConverter.Format,
                    (l, v) => (byte)PrefsFields.ClampedInt(l, v, byte.MinValue, byte.MaxValue),
                    v => CSharpLiterals.Int(v), constant, "(byte)" + IntGetter, IntSetter)
                {
                    FromInt = i => unchecked((byte)i), ToInt = v => v
                },
                new BuiltInValueHandler<sbyte>("sbyte", IntegersMenu + "sbyte", PrefsStorageSlot.Int, 0, 1,
                    PrefsConverter.ToSByte, PrefsConverter.Format,
                    (l, v) => (sbyte)PrefsFields.ClampedInt(l, v, sbyte.MinValue, sbyte.MaxValue),
                    v => CSharpLiterals.Int(v), constant, "(sbyte)" + IntGetter, IntSetter)
                {
                    FromInt = i => unchecked((sbyte)i), ToInt = v => v
                },
                new BuiltInValueHandler<short>("short", IntegersMenu + "short", PrefsStorageSlot.Int, 0, 1,
                    PrefsConverter.ToInt16, PrefsConverter.Format,
                    (l, v) => (short)PrefsFields.ClampedInt(l, v, short.MinValue, short.MaxValue),
                    v => CSharpLiterals.Int(v), constant, "(short)" + IntGetter, IntSetter)
                {
                    FromInt = i => unchecked((short)i), ToInt = v => v
                },
                new BuiltInValueHandler<ushort>("ushort", IntegersMenu + "ushort", PrefsStorageSlot.Int, 0, 1,
                    PrefsConverter.ToUInt16, PrefsConverter.Format,
                    (l, v) => (ushort)PrefsFields.ClampedInt(l, v, ushort.MinValue, ushort.MaxValue),
                    v => CSharpLiterals.Int(v), constant, "(ushort)" + IntGetter, IntSetter)
                {
                    FromInt = i => unchecked((ushort)i), ToInt = v => v
                },
                new BuiltInValueHandler<uint>("uint", IntegersMenu + "uint", PrefsStorageSlot.Int, 0U, 1U,
                    PrefsConverter.ToUInt32, PrefsConverter.Format,
                    (l, v) => (uint)Math.Max(uint.MinValue, Math.Min(uint.MaxValue, EditorGUILayout.LongField(l, v))),
                    CSharpLiterals.UInt, constant, "unchecked((uint){P}.GetInt({K}, unchecked((int){D})))",
                    "{P}.SetInt({K}, unchecked((int)value))")
                {
                    FromInt = i => unchecked((uint)i), ToInt = v => unchecked((int)v)
                },
                new BuiltInValueHandler<long>("long", IntegersMenu + "long", PrefsStorageSlot.String, 0L, 1L,
                    PrefsConverter.ToInt64, PrefsConverter.Format, (l, v) => EditorGUILayout.LongField(l, v),
                    CSharpLiterals.Long, constant, Parsed(nameof(PrefsConverter.ToInt64)), StringSetter),
                new BuiltInValueHandler<ulong>("ulong", IntegersMenu + "ulong", PrefsStorageSlot.String, 0UL, 1UL,
                    PrefsConverter.ToUInt64, PrefsConverter.Format,
                    (l, v) => PrefsFields.TextParsed(l, v, PrefsConverter.Format, PrefsConverter.ToUInt64, 1UL),
                    CSharpLiterals.ULong, constant, Parsed(nameof(PrefsConverter.ToUInt64)), StringSetter),

                new BuiltInValueHandler<double>("double", NumbersMenu + "double", PrefsStorageSlot.String, 0d, 1d,
                    PrefsConverter.ToDouble, PrefsConverter.Format, (l, v) => EditorGUILayout.DoubleField(l, v),
                    CSharpLiterals.Double, constant, Parsed(nameof(PrefsConverter.ToDouble)), StringSetter),
                new BuiltInValueHandler<decimal>("decimal", NumbersMenu + "decimal", PrefsStorageSlot.String, 0m, 1m,
                    PrefsConverter.ToDecimal, PrefsConverter.Format,
                    (l, v) => PrefsFields.TextParsed(l, v, PrefsConverter.Format, PrefsConverter.ToDecimal, 1m),
                    CSharpLiterals.Decimal, constant, Parsed(nameof(PrefsConverter.ToDecimal)), StringSetter),
                new BuiltInValueHandler<char>("char", "char", PrefsStorageSlot.Int, 'A', 'B',
                    PrefsConverter.ToChar, PrefsConverter.Format, PrefsFields.Char,
                    CSharpSyntax.CharLiteral, constant, "(char)" + IntGetter, IntSetter)
                {
                    FromInt = i => unchecked((char)i), ToInt = v => v
                },

                new BuiltInValueHandler<DateTime>("DateTime", SystemMenu + "DateTime", PrefsStorageSlot.String,
                    default, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    PrefsConverter.ToDateTime, PrefsConverter.Format,
                    (l, v) => PrefsFields.TextParsed(l, v, PrefsConverter.Format, PrefsConverter.ToDateTime, DateTime.MaxValue),
                    CSharpLiterals.DateTime, readOnly, Parsed(nameof(PrefsConverter.ToDateTime)), StringSetter),
                new BuiltInValueHandler<DateTimeOffset>("DateTimeOffset", SystemMenu + "DateTimeOffset", PrefsStorageSlot.String,
                    default, new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    PrefsConverter.ToDateTimeOffset, PrefsConverter.Format,
                    (l, v) => PrefsFields.TextParsed(l, v, PrefsConverter.Format, PrefsConverter.ToDateTimeOffset, DateTimeOffset.MaxValue),
                    CSharpLiterals.DateTimeOffset, readOnly, Parsed(nameof(PrefsConverter.ToDateTimeOffset)), StringSetter),
                new BuiltInValueHandler<TimeSpan>("TimeSpan", SystemMenu + "TimeSpan", PrefsStorageSlot.String,
                    TimeSpan.Zero, TimeSpan.FromSeconds(1),
                    PrefsConverter.ToTimeSpan, PrefsConverter.Format,
                    (l, v) => PrefsFields.TextParsed(l, v, PrefsConverter.Format, PrefsConverter.ToTimeSpan, TimeSpan.MaxValue),
                    CSharpLiterals.TimeSpan, readOnly, Parsed(nameof(PrefsConverter.ToTimeSpan)), StringSetter),
                new BuiltInValueHandler<Guid>("Guid", SystemMenu + "Guid", PrefsStorageSlot.String,
                    Guid.Empty, new Guid("00000000-0000-0000-0000-000000000001"),
                    PrefsConverter.ToGuid, PrefsConverter.Format,
                    (l, v) => PrefsFields.TextParsed(l, v, PrefsConverter.Format, PrefsConverter.ToGuid, new Guid("00000000-0000-0000-0000-000000000001")),
                    CSharpLiterals.Guid, readOnly, Parsed(nameof(PrefsConverter.ToGuid)), StringSetter),

                new BuiltInValueHandler<Vector2>("Vector2", UnityMenu + "Vector2", PrefsStorageSlot.String, Vector2.zero, Vector2.one,
                    PrefsConverter.ToVector2, PrefsConverter.Format, (l, v) => EditorGUILayout.Vector2Field(l, v),
                    CSharpLiterals.Vector2, readOnly, Parsed(nameof(PrefsConverter.ToVector2)), StringSetter),
                new BuiltInValueHandler<Vector3>("Vector3", UnityMenu + "Vector3", PrefsStorageSlot.String, Vector3.zero, Vector3.one,
                    PrefsConverter.ToVector3, PrefsConverter.Format, (l, v) => EditorGUILayout.Vector3Field(l, v),
                    CSharpLiterals.Vector3, readOnly, Parsed(nameof(PrefsConverter.ToVector3)), StringSetter),
                new BuiltInValueHandler<Vector4>("Vector4", UnityMenu + "Vector4", PrefsStorageSlot.String, Vector4.zero, Vector4.one,
                    PrefsConverter.ToVector4, PrefsConverter.Format, (l, v) => EditorGUILayout.Vector4Field(l.text, v),
                    CSharpLiterals.Vector4, readOnly, Parsed(nameof(PrefsConverter.ToVector4)), StringSetter),
                new BuiltInValueHandler<Vector2Int>("Vector2Int", UnityMenu + "Vector2Int", PrefsStorageSlot.String, Vector2Int.zero, Vector2Int.one,
                    PrefsConverter.ToVector2Int, PrefsConverter.Format, (l, v) => EditorGUILayout.Vector2IntField(l, v),
                    CSharpLiterals.Vector2Int, readOnly, Parsed(nameof(PrefsConverter.ToVector2Int)), StringSetter),
                new BuiltInValueHandler<Vector3Int>("Vector3Int", UnityMenu + "Vector3Int", PrefsStorageSlot.String, Vector3Int.zero, Vector3Int.one,
                    PrefsConverter.ToVector3Int, PrefsConverter.Format, (l, v) => EditorGUILayout.Vector3IntField(l, v),
                    CSharpLiterals.Vector3Int, readOnly, Parsed(nameof(PrefsConverter.ToVector3Int)), StringSetter),
                new BuiltInValueHandler<Quaternion>("Quaternion", UnityMenu + "Quaternion", PrefsStorageSlot.String,
                    Quaternion.identity, new Quaternion(1f, 0f, 0f, 0f),
                    PrefsConverter.ToQuaternion, PrefsConverter.Format, PrefsFields.Rotation,
                    CSharpLiterals.Quaternion, readOnly, Parsed(nameof(PrefsConverter.ToQuaternion)), StringSetter),
                new BuiltInValueHandler<Color>("Color", UnityMenu + "Color", PrefsStorageSlot.String, Color.white, Color.clear,
                    PrefsConverter.ToColor, PrefsConverter.Format, (l, v) => EditorGUILayout.ColorField(l, v, true, true, true),
                    CSharpLiterals.Color, readOnly, Parsed(nameof(PrefsConverter.ToColor)), StringSetter),
                new BuiltInValueHandler<Color32>("Color32", UnityMenu + "Color32", PrefsStorageSlot.Int,
                    new Color32(255, 255, 255, 255), new Color32(0, 0, 0, 0),
                    PrefsConverter.ToColor32, PrefsConverter.Format, (l, v) => (Color32)EditorGUILayout.ColorField(l, v),
                    CSharpLiterals.Color32, readOnly, "{C}.UnpackColor32({P}.GetInt({K}, {C}.PackColor32({D})))",
                    "{P}.SetInt({K}, {C}.PackColor32(value))")
                {
                    FromInt = PrefsConverter.UnpackColor32, ToInt = PrefsConverter.PackColor32
                },
                new BuiltInValueHandler<Rect>("Rect", UnityMenu + "Rect", PrefsStorageSlot.String, Rect.zero, new Rect(1f, 1f, 1f, 1f),
                    PrefsConverter.ToRect, PrefsConverter.Format, (l, v) => EditorGUILayout.RectField(l, v),
                    CSharpLiterals.Rect, readOnly, Parsed(nameof(PrefsConverter.ToRect)), StringSetter),
                new BuiltInValueHandler<RectInt>("RectInt", UnityMenu + "RectInt", PrefsStorageSlot.String, new RectInt(0, 0, 0, 0), new RectInt(1, 1, 1, 1),
                    PrefsConverter.ToRectInt, PrefsConverter.Format, (l, v) => EditorGUILayout.RectIntField(l, v),
                    CSharpLiterals.RectInt, readOnly, Parsed(nameof(PrefsConverter.ToRectInt)), StringSetter),
                new BuiltInValueHandler<Bounds>("Bounds", UnityMenu + "Bounds", PrefsStorageSlot.String,
                    new Bounds(Vector3.zero, Vector3.zero), new Bounds(Vector3.one, Vector3.one),
                    PrefsConverter.ToBounds, PrefsConverter.Format, (l, v) => EditorGUILayout.BoundsField(l, v),
                    CSharpLiterals.Bounds, readOnly, Parsed(nameof(PrefsConverter.ToBounds)), StringSetter),
                new BuiltInValueHandler<BoundsInt>("BoundsInt", UnityMenu + "BoundsInt", PrefsStorageSlot.String,
                    new BoundsInt(0, 0, 0, 0, 0, 0), new BoundsInt(1, 1, 1, 1, 1, 1),
                    PrefsConverter.ToBoundsInt, PrefsConverter.Format, (l, v) => EditorGUILayout.BoundsIntField(l, v),
                    CSharpLiterals.BoundsInt, readOnly, Parsed(nameof(PrefsConverter.ToBoundsInt)), StringSetter),

                new EnumValueHandler(),
                new JsonValueHandler()
            };
        }
    }
}
