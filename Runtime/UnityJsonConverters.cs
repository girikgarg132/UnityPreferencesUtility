using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using UnityEngine;

namespace GirikGarg.PreferencesUtility
{
    /// <summary>
    /// Newtonsoft converters for Unity value types. Without them, Newtonsoft fails on types such as
    /// <see cref="Vector3"/> (self-referencing <c>normalized</c> property) or writes redundant computed properties.
    /// Values are written as small objects (<c>{"x":1,"y":2}</c>); arrays (<c>[1,2]</c>) are also accepted when reading.
    /// </summary>
    public static class UnityJsonConverters
    {
        private const string CenterPropertyName = "center";
        private const string SizePropertyName = "size";
        private const string PositionPropertyName = "position";

        /// <summary>Adds a new instance of every Unity converter to <paramref name="converters"/>.</summary>
        public static void AddTo(IList<JsonConverter> converters)
        {
            if (converters == null) throw new ArgumentNullException(nameof(converters));

            foreach (JsonConverter converter in CreateAll())
            {
                converters.Add(converter);
            }
        }

        /// <summary>Creates new instances of every Unity converter.</summary>
        public static JsonConverter[] CreateAll() => new JsonConverter[]
        {
            new Vector2Converter(), new Vector3Converter(), new Vector4Converter(),
            new Vector2IntConverter(), new Vector3IntConverter(), new QuaternionConverter(),
            new ColorConverter(), new Color32Converter(), new RectConverter(), new RectIntConverter(),
            new BoundsConverter(), new BoundsIntConverter()
        };

        #region Base converters

        /// <summary>Base for structs serialized as a flat object of numeric components.</summary>
        private abstract class ComponentConverter<T> : JsonConverter where T : struct
        {
            private readonly string[] m_ComponentNames;
            private readonly bool m_IsInteger;

            protected ComponentConverter(bool isInteger, params string[] componentNames)
            {
                m_IsInteger = isInteger;
                m_ComponentNames = componentNames;
            }

            public override bool CanConvert(Type objectType) => objectType == typeof(T) || objectType == typeof(T?);

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }

                double[] components = new double[m_ComponentNames.Length];
                Decompose((T)value, components);
                writer.WriteStartObject();
                for (int i = 0; i < components.Length; i++)
                {
                    writer.WritePropertyName(m_ComponentNames[i]);
                    if (m_IsInteger) writer.WriteValue((long)components[i]);
                    else writer.WriteValue((float)components[i]);
                }

                writer.WriteEndObject();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    return objectType == typeof(T) ? (object)default(T) : null;
                }

                double[] components = new double[m_ComponentNames.Length];
                if (reader.TokenType == JsonToken.StartObject) ReadObject(reader, components);
                else if (reader.TokenType == JsonToken.StartArray) ReadArray(reader, components);
                else throw new JsonSerializationException($"Unexpected token {reader.TokenType} when reading {typeof(T).Name}.");

                return Compose(components);
            }

            protected abstract void Decompose(T value, double[] components);

            protected abstract T Compose(double[] components);

            private void ReadObject(JsonReader reader, double[] components)
            {
                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType != JsonToken.PropertyName) continue;

                    int index = IndexOf((string)reader.Value);
                    if (!reader.Read()) break;

                    if (index < 0) reader.Skip();
                    else components[index] = ReadNumber(reader);
                }
            }

            private void ReadArray(JsonReader reader, double[] components)
            {
                int index = 0;
                while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                {
                    if (index < components.Length) components[index] = ReadNumber(reader);
                    else reader.Skip();
                    index++;
                }
            }

            private int IndexOf(string propertyName)
            {
                for (int i = 0; i < m_ComponentNames.Length; i++)
                {
                    if (string.Equals(m_ComponentNames[i], propertyName, StringComparison.OrdinalIgnoreCase)) return i;
                }

                return -1;
            }

            private static double ReadNumber(JsonReader reader)
            {
                switch (reader.TokenType)
                {
                    case JsonToken.Integer:
                    case JsonToken.Float:
                        return Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture);
                    case JsonToken.String:
                        return double.Parse((string)reader.Value, NumberStyles.Float, CultureInfo.InvariantCulture);
                    case JsonToken.Null:
                        return 0d;
                    default:
                        throw new JsonSerializationException($"Expected a number but found {reader.TokenType}.");
                }
            }
        }

        #endregion

        #region Float structs

        private sealed class Vector2Converter : ComponentConverter<Vector2>
        {
            public Vector2Converter() : base(false, "x", "y") { }
            protected override void Decompose(Vector2 v, double[] c) { c[0] = v.x; c[1] = v.y; }
            protected override Vector2 Compose(double[] c) => new Vector2((float)c[0], (float)c[1]);
        }

        private sealed class Vector3Converter : ComponentConverter<Vector3>
        {
            public Vector3Converter() : base(false, "x", "y", "z") { }
            protected override void Decompose(Vector3 v, double[] c) { c[0] = v.x; c[1] = v.y; c[2] = v.z; }
            protected override Vector3 Compose(double[] c) => new Vector3((float)c[0], (float)c[1], (float)c[2]);
        }

        private sealed class Vector4Converter : ComponentConverter<Vector4>
        {
            public Vector4Converter() : base(false, "x", "y", "z", "w") { }
            protected override void Decompose(Vector4 v, double[] c) { c[0] = v.x; c[1] = v.y; c[2] = v.z; c[3] = v.w; }
            protected override Vector4 Compose(double[] c) => new Vector4((float)c[0], (float)c[1], (float)c[2], (float)c[3]);
        }

        private sealed class QuaternionConverter : ComponentConverter<Quaternion>
        {
            public QuaternionConverter() : base(false, "x", "y", "z", "w") { }
            protected override void Decompose(Quaternion q, double[] c) { c[0] = q.x; c[1] = q.y; c[2] = q.z; c[3] = q.w; }
            protected override Quaternion Compose(double[] c) => new Quaternion((float)c[0], (float)c[1], (float)c[2], (float)c[3]);
        }

        private sealed class ColorConverter : ComponentConverter<Color>
        {
            public ColorConverter() : base(false, "r", "g", "b", "a") { }
            protected override void Decompose(Color v, double[] c) { c[0] = v.r; c[1] = v.g; c[2] = v.b; c[3] = v.a; }
            protected override Color Compose(double[] c) => new Color((float)c[0], (float)c[1], (float)c[2], (float)c[3]);
        }

        private sealed class RectConverter : ComponentConverter<Rect>
        {
            public RectConverter() : base(false, "x", "y", "width", "height") { }
            protected override void Decompose(Rect v, double[] c) { c[0] = v.x; c[1] = v.y; c[2] = v.width; c[3] = v.height; }
            protected override Rect Compose(double[] c) => new Rect((float)c[0], (float)c[1], (float)c[2], (float)c[3]);
        }

        #endregion

        #region Integer structs

        private sealed class Vector2IntConverter : ComponentConverter<Vector2Int>
        {
            public Vector2IntConverter() : base(true, "x", "y") { }
            protected override void Decompose(Vector2Int v, double[] c) { c[0] = v.x; c[1] = v.y; }
            protected override Vector2Int Compose(double[] c) => new Vector2Int((int)c[0], (int)c[1]);
        }

        private sealed class Vector3IntConverter : ComponentConverter<Vector3Int>
        {
            public Vector3IntConverter() : base(true, "x", "y", "z") { }
            protected override void Decompose(Vector3Int v, double[] c) { c[0] = v.x; c[1] = v.y; c[2] = v.z; }
            protected override Vector3Int Compose(double[] c) => new Vector3Int((int)c[0], (int)c[1], (int)c[2]);
        }

        private sealed class Color32Converter : ComponentConverter<Color32>
        {
            public Color32Converter() : base(true, "r", "g", "b", "a") { }
            protected override void Decompose(Color32 v, double[] c) { c[0] = v.r; c[1] = v.g; c[2] = v.b; c[3] = v.a; }
            protected override Color32 Compose(double[] c) => new Color32(ToByte(c[0]), ToByte(c[1]), ToByte(c[2]), ToByte(c[3]));
            private static byte ToByte(double value) => (byte)Math.Max(byte.MinValue, Math.Min(byte.MaxValue, value));
        }

        private sealed class RectIntConverter : ComponentConverter<RectInt>
        {
            public RectIntConverter() : base(true, "x", "y", "width", "height") { }
            protected override void Decompose(RectInt v, double[] c) { c[0] = v.x; c[1] = v.y; c[2] = v.width; c[3] = v.height; }
            protected override RectInt Compose(double[] c) => new RectInt((int)c[0], (int)c[1], (int)c[2], (int)c[3]);
        }

        #endregion

        #region Composite structs

        /// <summary>Serializes <see cref="Bounds"/> as <c>{"center":{...},"size":{...}}</c>.</summary>
        private sealed class BoundsConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(Bounds) || objectType == typeof(Bounds?);

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }

                Bounds bounds = (Bounds)value;
                writer.WriteStartObject();
                writer.WritePropertyName(CenterPropertyName);
                serializer.Serialize(writer, bounds.center, typeof(Vector3));
                writer.WritePropertyName(SizePropertyName);
                serializer.Serialize(writer, bounds.size, typeof(Vector3));
                writer.WriteEndObject();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return objectType == typeof(Bounds) ? (object)default(Bounds) : null;

                Vector3 center = Vector3.zero;
                Vector3 size = Vector3.zero;
                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType != JsonToken.PropertyName) continue;

                    string propertyName = (string)reader.Value;
                    if (!reader.Read()) break;

                    if (string.Equals(propertyName, CenterPropertyName, StringComparison.OrdinalIgnoreCase)) center = serializer.Deserialize<Vector3>(reader);
                    else if (string.Equals(propertyName, SizePropertyName, StringComparison.OrdinalIgnoreCase)) size = serializer.Deserialize<Vector3>(reader);
                    else reader.Skip();
                }

                return new Bounds(center, size);
            }
        }

        /// <summary>Serializes <see cref="BoundsInt"/> as <c>{"position":{...},"size":{...}}</c>.</summary>
        private sealed class BoundsIntConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(BoundsInt) || objectType == typeof(BoundsInt?);

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }

                BoundsInt bounds = (BoundsInt)value;
                writer.WriteStartObject();
                writer.WritePropertyName(PositionPropertyName);
                serializer.Serialize(writer, bounds.position, typeof(Vector3Int));
                writer.WritePropertyName(SizePropertyName);
                serializer.Serialize(writer, bounds.size, typeof(Vector3Int));
                writer.WriteEndObject();
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return objectType == typeof(BoundsInt) ? (object)default(BoundsInt) : null;

                Vector3Int position = Vector3Int.zero;
                Vector3Int size = Vector3Int.zero;
                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType != JsonToken.PropertyName) continue;

                    string propertyName = (string)reader.Value;
                    if (!reader.Read()) break;

                    if (string.Equals(propertyName, PositionPropertyName, StringComparison.OrdinalIgnoreCase)) position = serializer.Deserialize<Vector3Int>(reader);
                    else if (string.Equals(propertyName, SizePropertyName, StringComparison.OrdinalIgnoreCase)) size = serializer.Deserialize<Vector3Int>(reader);
                    else reader.Skip();
                }

                return new BoundsInt(position, size);
            }
        }

        #endregion
    }
}
