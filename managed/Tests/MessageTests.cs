using Haiku.App;
using Haiku.Testing;

/// <summary>
/// Regression coverage for Haiku.App.Message (see managed/Haiku.App/Message.cs
/// and native/src/hs_message.cpp) -- one focused test per Add/Find pair or
/// whole-message operation, replacing the single giant TestMessageRoundTrip
/// method that used to live in Sample/Program.cs. Run with `mono Tests.exe
/// Message` to run just this class.
/// </summary>
public class MessageTests
{
	private const uint TestWhat = 0x54455354; // 'TEST'
	private const uint RawType = 0x52415720; // 'RAW ', an arbitrary Haiku type_code for AddData/FindData
	private const uint Int32Type = 0x4C4F4E47; // 'LONG', Haiku's B_INT32_TYPE (see TypeConstants.h)

	[Test]
	public void WhatFieldIsReadableAndSettable()
	{
		using (Message msg = new Message(TestWhat)) {
			Assert.AreEqual(TestWhat, msg.What, "constructor should set What");
			msg.What = 42;
			Assert.AreEqual((uint)42, msg.What, "What should be settable after construction");
		}
	}

	[Test]
	public void Int8RoundTrips()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddInt8("v", -12);
			Assert.AreEqual((sbyte)-12, msg.FindInt8("v"), "int8 round trip");
		}
	}

	[Test]
	public void Int16RoundTrips()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddInt16("v", -1234);
			Assert.AreEqual((short)-1234, msg.FindInt16("v"), "int16 round trip");
		}
	}

	[Test]
	public void Int32RoundTrips()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddInt32("v", -123456);
			Assert.AreEqual(-123456, msg.FindInt32("v"), "int32 round trip");
		}
	}

	[Test]
	public void Int64RoundTrips()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddInt64("v", -123456789012345L);
			Assert.AreEqual(-123456789012345L, msg.FindInt64("v"), "int64 round trip");
		}
	}

	[Test]
	public void UInt8RoundTrips()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddUInt8("v", 200);
			Assert.AreEqual((byte)200, msg.FindUInt8("v"), "uint8 round trip");
		}
	}

	[Test]
	public void UInt16RoundTrips()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddUInt16("v", 50000);
			Assert.AreEqual((ushort)50000, msg.FindUInt16("v"), "uint16 round trip");
		}
	}

	[Test]
	public void UInt32RoundTrips()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddUInt32("v", 3000000000u);
			Assert.AreEqual(3000000000u, msg.FindUInt32("v"), "uint32 round trip");
		}
	}

	[Test]
	public void UInt64RoundTrips()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddUInt64("v", 12345678901234567890UL);
			Assert.AreEqual(12345678901234567890UL, msg.FindUInt64("v"), "uint64 round trip");
		}
	}

	[Test]
	public void FloatRoundTrips()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddFloat("v", 3.25f);
			Assert.AreEqual(3.25f, msg.FindFloat("v"), "float round trip");
		}
	}

	[Test]
	public void DoubleRoundTrips()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddDouble("v", 2.718281828);
			Assert.AreEqual(2.718281828, msg.FindDouble("v"), "double round trip");
		}
	}

	[Test]
	public void BoolRoundTrips()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddBool("v", true);
			Assert.AreEqual(true, msg.FindBool("v"), "bool round trip");
		}
	}

	[Test]
	public void StringRoundTrips()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddString("v", "round trip");
			Assert.AreEqual("round trip", msg.FindString("v"), "string round trip");
		}
	}

	[Test]
	public void PointRoundTrips()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddPoint("v", new Point(1.5f, -2.5f));
			Assert.AreEqual(new Point(1.5f, -2.5f), msg.FindPoint("v"), "point round trip");
		}
	}

	[Test]
	public void RectRoundTrips()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddRect("v", new Rect(0f, 0f, 100f, 50f));
			Assert.AreEqual(new Rect(0f, 0f, 100f, 50f), msg.FindRect("v"), "rect round trip");
		}
	}

	[Test]
	public void SizeRoundTrips()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddSize("v", new Size(640f, 480f));
			Assert.AreEqual(new Size(640f, 480f), msg.FindSize("v"), "size round trip");
		}
	}

	[Test]
	public void ColorRoundTrips()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddColor("v", new RgbColor(10, 20, 30, 40));
			Assert.AreEqual(new RgbColor(10, 20, 30, 40), msg.FindColor("v"), "color round trip");
		}
	}

	[Test]
	public void AlignmentRoundTrips()
	{
		using (Message msg = new Message(TestWhat)) {
			Alignment value = new Alignment(HorizontalAlignment.Right, VerticalAlignment.Bottom);
			msg.AddAlignment("v", value);
			Assert.AreEqual(value, msg.FindAlignment("v"), "alignment round trip");
		}
	}

	[Test]
	public void PointerRoundTrips()
	{
		using (Message msg = new Message(TestWhat)) {
			System.IntPtr value = new System.IntPtr(0x1234);
			msg.AddPointer("v", value);
			Assert.AreEqual(value, msg.FindPointer("v"), "pointer round trip");
		}
	}

	[Test]
	public void DataRoundTrips()
	{
		using (Message msg = new Message(TestWhat)) {
			byte[] sent = new byte[] { 1, 2, 3, 4, 5 };
			msg.AddData("v", RawType, sent);
			byte[] received = msg.FindData("v", RawType);
			Assert.IsNotNull(received, "FindData should return the bytes back");
			Assert.AreEqual(sent.Length, received.Length, "FindData byte count");
			for (int i = 0; i < sent.Length; i++)
				Assert.AreEqual(sent[i], received[i], "FindData byte at index " + i);
		}
	}

	[Test]
	public void FindReturnsNullForMissingKey()
	{
		using (Message msg = new Message(TestWhat)) {
			Assert.IsNull(msg.FindInt32("does-not-exist"), "FindInt32 on a name never added");
			Assert.IsNull(msg.FindString("does-not-exist"), "FindString on a name never added");
		}
	}

	[Test]
	public void NestedMessageRoundTrips()
	{
		using (Message outer = new Message(TestWhat))
		using (Message inner = new Message(0x4E455354 /* 'NEST' */)) {
			inner.AddString("who", "inner message");
			outer.AddMessage("child", inner);

			using (Message found = outer.FindMessage("child")) {
				Assert.IsNotNull(found, "FindMessage should find the nested message");
				Assert.AreEqual("inner message", found.FindString("who"), "nested message field");
			}
		}
	}

	[Test]
	public void FindMessageReturnsNullForMissingKey()
	{
		using (Message msg = new Message(TestWhat))
			Assert.IsNull(msg.FindMessage("does-not-exist"), "FindMessage on a name never added");
	}

	[Test]
	public void HasReturnsTrueForPresentField()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddInt32("v", 1);
			Assert.IsTrue(msg.HasInt32("v"), "HasInt32 should be true right after AddInt32");
		}
	}

	[Test]
	public void HasReturnsFalseForMissingField()
	{
		using (Message msg = new Message(TestWhat))
			Assert.IsFalse(msg.HasInt32("does-not-exist"), "HasInt32 should be false for a name never added");
	}

	[Test]
	public void HasDataReturnsTrueForPresentField()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddData("v", RawType, new byte[] { 1 });
			Assert.IsTrue(msg.HasData("v", RawType), "HasData should be true right after AddData");
		}
	}

	[Test]
	public void RemoveNameDropsTheField()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddInt32("v", 1);
			msg.RemoveName("v");
			Assert.IsNull(msg.FindInt32("v"), "field should be gone after RemoveName");
		}
	}

	[Test]
	public void CountNamesReflectsFieldCount()
	{
		using (Message msg = new Message(TestWhat)) {
			int before = msg.CountNames(Int32Type);
			msg.AddInt32("a", 1);
			msg.AddInt32("b", 2);
			Assert.AreEqual(before + 2, msg.CountNames(Int32Type), "CountNames after adding two int32 fields");
			msg.RemoveName("a");
			Assert.AreEqual(before + 1, msg.CountNames(Int32Type), "CountNames after removing one of them");
		}
	}

	[Test]
	public void RenameMovesTheValue()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddString("old", "value");
			msg.Rename("old", "new");
			Assert.AreEqual("value", msg.FindString("new"), "value should be readable under the new name");
			Assert.IsNull(msg.FindString("old"), "old name should no longer resolve");
		}
	}

	[Test]
	public void IsEmptyAndMakeEmpty()
	{
		using (Message msg = new Message(TestWhat)) {
			Assert.IsTrue(msg.IsEmpty(), "a freshly constructed message should be empty");
			msg.AddInt32("v", 1);
			Assert.IsFalse(msg.IsEmpty(), "should not be empty once a field is added");
			msg.MakeEmpty();
			Assert.IsTrue(msg.IsEmpty(), "should be empty again after MakeEmpty");
		}
	}

	[Test]
	public void AppendMergesFieldsFromAnotherMessage()
	{
		using (Message a = new Message(1))
		using (Message b = new Message(2)) {
			a.AddInt32("from-a", 1);
			b.AddInt32("from-b", 2);
			a.Append(b);
			Assert.AreEqual(1, a.FindInt32("from-a"), "Append should keep the destination's own fields");
			Assert.AreEqual(2, a.FindInt32("from-b"), "Append should copy in the source's fields");
		}
	}

	[Test]
	public void ReplaceInt32OverwritesExistingValue()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddInt32("v", 1);
			msg.ReplaceInt32("v", 999);
			Assert.AreEqual(999, msg.FindInt32("v"), "value after ReplaceInt32");
		}
	}

	[Test]
	public void ReplaceColorOverwritesExistingValue()
	{
		using (Message msg = new Message(TestWhat)) {
			msg.AddColor("v", new RgbColor(1, 2, 3, 4));
			msg.ReplaceColor("v", new RgbColor(5, 6, 7, 8));
			Assert.AreEqual(new RgbColor(5, 6, 7, 8), msg.FindColor("v"), "value after ReplaceColor");
		}
	}
}
