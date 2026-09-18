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
