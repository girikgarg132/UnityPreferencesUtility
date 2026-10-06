using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GirikGarg.PreferencesUtility.Tests
{
    /// <summary>Round-trip and fallback tests for <see cref="PrefsConverter"/> and <see cref="PrefsJson"/>.</summary>
    public sealed class PrefsConverterTests
    {
        private const string Garbage = "not a value";

        [Test]
        public void Primitives_RoundTrip()
        {
            Assert.AreEqual(long.MinValue, PrefsConverter.ToInt64(PrefsConverter.Format(long.MinValue), 0L));
            Assert.AreEqual(ulong.MaxValue, PrefsConverter.ToUInt64(PrefsConverter.Format(ulong.MaxValue), 0UL));
            Assert.AreEqual(0.1d, PrefsConverter.ToDouble(PrefsConverter.Format(0.1d), 0d));
            Assert.AreEqual(0.1f, PrefsConverter.ToSingle(PrefsConverter.Format(0.1f), 0f));
            Assert.AreEqual(123.456m, PrefsConverter.ToDecimal(PrefsConverter.Format(123.456m), 0m));
            Assert.AreEqual('Z', PrefsConverter.ToChar(PrefsConverter.Format('Z'), 'A'));
            Assert.IsTrue(PrefsConverter.ToBoolean(PrefsConverter.Format(true), false));
            Assert.IsTrue(float.IsNaN(PrefsConverter.ToSingle(PrefsConverter.Format(float.NaN), 0f)));
        }

        [Test]
        public void SystemTypes_RoundTrip()
        {
            DateTime utc = new DateTime(2024, 5, 6, 7, 8, 9, 123, DateTimeKind.Utc);
            DateTime parsed = PrefsConverter.ToDateTime(PrefsConverter.Format(utc), default);
            Assert.AreEqual(utc, parsed);
            Assert.AreEqual(DateTimeKind.Utc, parsed.Kind);

            DateTimeOffset offset = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(5.5));
            Assert.AreEqual(offset, PrefsConverter.ToDateTimeOffset(PrefsConverter.Format(offset), default));

            TimeSpan span = new TimeSpan(1, 2, 3, 4, 5);
            Assert.AreEqual(span, PrefsConverter.ToTimeSpan(PrefsConverter.Format(span), TimeSpan.Zero));

            Guid guid = Guid.NewGuid();
            Assert.AreEqual(guid, PrefsConverter.ToGuid(PrefsConverter.Format(guid), Guid.Empty));
        }

        [Test]
        public void UnityTypes_RoundTrip()
        {
            Vector3 vector = new Vector3(1.5f, -2.25f, 1e-7f);
            Assert.AreEqual(vector, PrefsConverter.ToVector3(PrefsConverter.Format(vector), Vector3.zero));

            Color color = new Color(0.1f, 0.2f, 0.3f, 0.4f);
            Assert.AreEqual(color, PrefsConverter.ToColor(PrefsConverter.Format(color), Color.clear));

            Color32 color32 = new Color32(1, 2, 250, 128);
            Assert.AreEqual(color32, PrefsConverter.UnpackColor32(PrefsConverter.PackColor32(color32)));
            Assert.AreEqual(color32, PrefsConverter.ToColor32(PrefsConverter.Format(color32), default));

            Bounds bounds = new Bounds(new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f));
            Assert.AreEqual(bounds, PrefsConverter.ToBounds(PrefsConverter.Format(bounds), default));

            BoundsInt boundsInt = new BoundsInt(1, 2, 3, 4, 5, 6);
            Assert.AreEqual(boundsInt, PrefsConverter.ToBoundsInt(PrefsConverter.Format(boundsInt), default));

            RectInt rectInt = new RectInt(-1, 2, 30, 40);
            Assert.AreEqual(rectInt, PrefsConverter.ToRectInt(PrefsConverter.Format(rectInt), default));
        }

        [Test]
        public void MalformedText_ReturnsFallback()
        {
            Assert.AreEqual(7L, PrefsConverter.ToInt64(Garbage, 7L));
            Assert.AreEqual(Vector3.one, PrefsConverter.ToVector3("1,2", Vector3.one));
            Assert.AreEqual(Vector3.one, PrefsConverter.ToVector3("1,2,3,4", Vector3.one));
            Assert.AreEqual(Vector2.one, PrefsConverter.ToVector2(null, Vector2.one));
            Assert.AreEqual(Guid.Empty, PrefsConverter.ToGuid(Garbage, Guid.Empty));
        }

        [Serializable]
        private sealed class SaveData
        {
            public int Level = 1;
            public List<string> Items = new List<string> { "Sword" };
            public Vector3 Position;
            public Dictionary<string, Color> Colors = new Dictionary<string, Color>();
        }

        [Test]
        public void Json_RoundTripsUnityStructsWithoutDuplicatingCollections()
        {
            SaveData data = new SaveData { Level = 5, Position = new Vector3(1f, 2f, 3f) };
            data.Items.Add("Shield");
            data.Colors["Team"] = Color.red;

            SaveData parsed = PrefsJson.Read<SaveData>(PrefsJson.Serialize(data), null, "Test");

            Assert.AreEqual(5, parsed.Level);
            CollectionAssert.AreEqual(new[] { "Sword", "Shield" }, parsed.Items);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), parsed.Position);
            Assert.AreEqual(Color.red, parsed.Colors["Team"]);
        }

        [Test]
        public void Json_InvalidStoredValue_FallsBackToDefaultJson()
        {
            bool previous = PrefsJson.LogFailures;
            PrefsJson.LogFailures = false;
            try
            {
                SaveData parsed = PrefsJson.Read<SaveData>("{ broken", "{\"Level\":9}", "Test");
                Assert.AreEqual(9, parsed.Level);
                Assert.IsNull(PrefsJson.Read<SaveData>(string.Empty, string.Empty, "Test"));
            }
            finally
            {
                PrefsJson.LogFailures = previous;
            }
        }
    }
}
