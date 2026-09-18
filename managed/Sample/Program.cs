using System;
using Haiku.App;

/*
 * A minimal, runnable example of this binding's Application Kit slice.
 * This is NOT where verification of the binding lives anymore -- see
 * managed/Tests/ (built as Tests.exe) for the actual regression suite,
 * including ApplicationTests.ReadyToRunMessageAndQuitRequestedAllFireInOrder,
 * which is this exact scenario wrapped in assertions instead of eyeballed
 * Console output. This file exists purely to be a clear, working starting
 * point for your own app: subclass Application, override the callbacks you
 * need, construct it, call Run().
 *
 * The callbacks below fire on a native OS thread BLooper::Run() spawns --
 * one Mono never created -- not on the thread that called Run(). See
 * hs_application.h's threading note and Application.cs's class remarks if
 * you're wondering why that matters.
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
