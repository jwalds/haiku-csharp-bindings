using System;
using Haiku.App;

/*
 * This sample exists to PROVE the whole chain works end to end on this
 * exact Mono-on-Haiku build, not just to be a nice demo:
 *
 *   1. Main() (the thread Mono started) constructs a native HSApplication
 *      and calls Run(), which blocks this thread.
 *   2. BLooper::Run() spawns a SEPARATE native OS thread for the message
 *      loop, which Mono never created directly.
 *   3. ReadyToRun() fires on THAT thread and calls back into our C#
 *      OnReadyToRun() override -- this is the specific interaction flagged
 *      as unverified in hs_application.h: a native-spawned thread invoking
 *      a managed delegate for the first time.
 *   4. If that worked, PostMessage/MessageReceived and the QuitRequested-
 *      triggered clean shutdown (without the BLooper::Quit() double-free
 *      trap) are comparatively low-risk by comparison.
 *
 * Run it, and if you see all four Console.WriteLine calls below in order
 * followed by a clean process exit (no crash, no hang), that confirms the
 * cross-thread managed-callback story holds up here.
 */
public class DemoApplication : Application
{
	private const uint PingMessage = 0x50494E47; // 'PING'

	public DemoApplication() : base("application/x-vnd.HaikuSharp-Demo")
	{
	}

	protected override void OnReadyToRun()
	{
		Console.WriteLine("[1] OnReadyToRun fired -- native callback into managed code works.");

		using (var ping = new Message(PingMessage)) {
			ping.AddString("greeting", "hello from the looper thread's own message");
			PostMessage(ping);
		}
	}

	protected override void OnMessageReceived(Message message)
	{
		if (message.What == PingMessage) {
			Console.WriteLine("[2] Received our own PING message back: \"{0}\"",
				message.FindString("greeting"));
			Console.WriteLine("[3] Requesting quit via SystemMessages.QuitRequested...");
			PostMessage(SystemMessages.QuitRequested);
		}
	}

	protected override bool OnQuitRequested()
	{
		Console.WriteLine("[4] OnQuitRequested fired -- allowing shutdown.");
		return true;
	}
}

public class Program
{
	/// <summary>
	/// Exercises every BMessage Add/Find pair this binding supports,
	/// round-tripping each one through a real native BMessage and checking
	/// the value that comes back. Runs before the Application Kit's own
	/// [1]-[4] proof sequence below, on the ordinary startup thread (no
	/// BLooper involved yet), so a failure here narrows the problem down to
	/// BMessage itself rather than anything about the threading model.
	/// </summary>
	// Haiku's B_INT32_TYPE, from TypeConstants.h ('LONG' packed as a uint32) --
	// used below with CountNames, which needs a real Haiku type_code.
	private const uint B_INT32_TYPE = 0x4C4F4E47;

	private static bool TestMessageRoundTrip()
	{
		bool ok = true;
		using (Message msg = new Message(0x54455354 /* 'TEST' */)) {
			msg.AddInt8("i8", -12);
			msg.AddInt16("i16", -1234);
			msg.AddInt32("i32", -123456);
			msg.AddInt64("i64", -123456789012345L);
			msg.AddFloat("f", 3.25f);
			msg.AddDouble("d", 2.718281828);
			msg.AddBool("b", true);
			msg.AddString("s", "round trip");
			msg.AddPoint("pt", new Point(1.5f, -2.5f));
			msg.AddRect("rc", new Rect(0f, 0f, 100f, 50f));

			ok &= Check("i8", msg.FindInt8("i8") == -12);
			ok &= Check("i16", msg.FindInt16("i16") == -1234);
			ok &= Check("i32", msg.FindInt32("i32") == -123456);
			ok &= Check("i64", msg.FindInt64("i64") == -123456789012345L);
			ok &= Check("f", msg.FindFloat("f") == 3.25f);
			ok &= Check("d", msg.FindDouble("d") == 2.718281828);
			ok &= Check("b", msg.FindBool("b") == true);
			ok &= Check("s", msg.FindString("s") == "round trip");
			ok &= Check("pt", msg.FindPoint("pt") == new Point(1.5f, -2.5f));
			ok &= Check("rc", msg.FindRect("rc") == new Rect(0f, 0f, 100f, 50f));
			ok &= Check("missing key returns null", msg.FindInt32("does-not-exist") == null);

			// -- Round 2: the "core data round-trip" expansion (unsigned
			// scalars, Size/RgbColor/Alignment, nested messages, generic
			// Data, Has*/Remove*/MakeEmpty/IsEmpty/CountNames/Rename/Append,
			// and Replace* for everything above). --
			msg.AddUInt8("u8", 200);
			msg.AddUInt16("u16", 50000);
			msg.AddUInt32("u32", 3000000000u);
			msg.AddUInt64("u64", 12345678901234567890UL);
			msg.AddSize("sz", new Size(640f, 480f));
			msg.AddColor("col", new RgbColor(10, 20, 30, 40));
			msg.AddAlignment("align", new Alignment(HorizontalAlignment.Right, VerticalAlignment.Bottom));
			msg.AddData("blob", 0x52415720 /* 'RAW ' */, new byte[] { 1, 2, 3, 4, 5 });

			ok &= Check("u8", msg.FindUInt8("u8") == 200);
			ok &= Check("u16", msg.FindUInt16("u16") == 50000);
			ok &= Check("u32", msg.FindUInt32("u32") == 3000000000u);
			ok &= Check("u64", msg.FindUInt64("u64") == 12345678901234567890UL);
			ok &= Check("sz", msg.FindSize("sz") == new Size(640f, 480f));
			ok &= Check("col", msg.FindColor("col") == new RgbColor(10, 20, 30, 40));
			ok &= Check("align", msg.FindAlignment("align")
				== new Alignment(HorizontalAlignment.Right, VerticalAlignment.Bottom));

			byte[] blob = msg.FindData("blob", 0x52415720);
			ok &= Check("blob", blob != null && blob.Length == 5
				&& blob[0] == 1 && blob[4] == 5);

			// Has* for the fields we just added, plus one that was never added.
			ok &= Check("HasInt32 true", msg.HasInt32("i32"));
			ok &= Check("HasUInt64 true", msg.HasUInt64("u64"));
			ok &= Check("HasColor true", msg.HasColor("col"));
			ok &= Check("HasData true", msg.HasData("blob", 0x52415720));
			ok &= Check("HasInt32 false for missing key", !msg.HasInt32("does-not-exist"));

			// Nested message.
			using (Message nested = new Message(0x4E455354 /* 'NEST' */)) {
				nested.AddString("who", "inner message");
				msg.AddMessage("child", nested);
			}
			using (Message found = msg.FindMessage("child")) {
				ok &= Check("nested message found", found != null);
				ok &= Check("nested message field", found != null && found.FindString("who") == "inner message");
			}
			ok &= Check("FindMessage returns null for missing key", msg.FindMessage("does-not-exist") == null);

			// Replace* -- overwrite a handful of the fields above and confirm
			// the new value round-trips (BMessage's Replace* requires the name
			// to already exist with the same type, which every field below does).
			msg.ReplaceInt32("i32", 999);
			msg.ReplaceUInt8("u8", 1);
			msg.ReplaceString("s", "replaced");
			msg.ReplacePoint("pt", new Point(7f, 8f));
			msg.ReplaceColor("col", new RgbColor(1, 2, 3, 4));
			ok &= Check("ReplaceInt32", msg.FindInt32("i32") == 999);
			ok &= Check("ReplaceUInt8", msg.FindUInt8("u8") == 1);
			ok &= Check("ReplaceString", msg.FindString("s") == "replaced");
			ok &= Check("ReplacePoint", msg.FindPoint("pt") == new Point(7f, 8f));
			ok &= Check("ReplaceColor", msg.FindColor("col") == new RgbColor(1, 2, 3, 4));

			// RemoveName / CountNames / Rename / IsEmpty / MakeEmpty.
			int countBefore = msg.CountNames(B_INT32_TYPE);
			msg.RemoveName("i32");
			ok &= Check("RemoveName drops the field", msg.FindInt32("i32") == null);
			ok &= Check("CountNames drops by one", msg.CountNames(B_INT32_TYPE) == countBefore - 1);

			msg.Rename("s", "s-renamed");
			ok &= Check("Rename moves the value", msg.FindString("s-renamed") == "replaced");
			ok &= Check("Rename leaves old name empty", msg.FindString("s") == null);

			ok &= Check("IsEmpty false before MakeEmpty", !msg.IsEmpty());
			msg.MakeEmpty();
			ok &= Check("IsEmpty true after MakeEmpty", msg.IsEmpty());

			// Append: copy fields from one message into another.
			using (Message a = new Message(1)) {
				using (Message b = new Message(2)) {
					a.AddInt32("from-a", 1);
					b.AddInt32("from-b", 2);
					a.Append(b);
					ok &= Check("Append merges fields", a.FindInt32("from-a") == 1 && a.FindInt32("from-b") == 2);
				}
			}
		}

		Console.WriteLine(ok
			? "[0] BMessage Add/Find round-trip: all fields OK"
			: "[0] BMessage Add/Find round-trip: SOME FIELDS FAILED (see above)");
		return ok;
	}

	private static bool Check(string label, bool condition)
	{
		if (!condition)
			Console.WriteLine("  FAIL: " + label);
		return condition;
	}

	public static int Main(string[] args)
	{
		if (!TestMessageRoundTrip())
			return 1;

		var app = new DemoApplication();
		try {
			app.Run(); // blocks here until DemoApplication quits
		} catch (HaikuException ex) {
			Console.Error.WriteLine("Haiku API error: " + ex.Message);
			return 1;
		}

		Console.WriteLine("App exited cleanly.");
		return 0;
	}
}
