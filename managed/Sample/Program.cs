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
	public static int Main(string[] args)
	{
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
