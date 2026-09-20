using System;
using Haiku.App;
using Haiku.Interface;

/*
 * A minimal, runnable example of this binding's Interface Kit slice: a
 * BWindow you can actually see and close. This is NOT where verification of
 * the binding lives -- see managed/Tests/ (built as Tests.exe) for the
 * actual regression suite, including WindowTests.QuitPostsRequestAndFires-
 * DestroyedCallback, which exercises this same Quit()/OnDestroyed() path
 * without needing a human to click anything. This file exists purely to be
 * a clear, working starting point for your own windowed app.
 *
 * DemoWindow uses WindowFlags.QuitOnWindowClose, so clicking its title bar's
 * close box doesn't just end the window -- BWindow.h's own flag semantics
 * make it signal the owning BApplication to quit too, which is what lets
 * this Main() (blocked in app.Run(), same as the Application Kit sample)
 * return once you close the window.
 */
public class DemoWindow : Window
{
	public DemoWindow()
		: base(new Rect(100, 100, 500, 350), "Haiku C# Bindings Demo",
			WindowLook.Titled, WindowFeel.Normal, WindowFlags.QuitOnWindowClose)
	{
	}

	protected override void OnDestroyed()
	{
		Console.WriteLine("[3] DemoWindow destroyed.");
	}
}

public class DemoApplication : Application
{
	public DemoApplication() : base("application/x-vnd.HaikuSharp-Demo")
	{
	}

	protected override void OnReadyToRun()
	{
		Console.WriteLine("[1] OnReadyToRun fired -- creating and showing the demo window.");
		DemoWindow window = new DemoWindow();
		window.Show();
		Console.WriteLine("[2] Window shown -- close it (its title bar's close box) to quit.");
	}

	protected override bool OnQuitRequested()
	{
		Console.WriteLine("[4] Application OnQuitRequested fired -- allowing shutdown.");
		return true;
	}
}

public class Program
{
	public static int Main(string[] args)
	{
		var app = new DemoApplication();
		try {
			app.Run(); // blocks here until the window's close box quits the app
		} catch (HaikuException ex) {
			Console.Error.WriteLine("Haiku API error: " + ex.Message);
			return 1;
		}

		Console.WriteLine("App exited cleanly.");
		return 0;
	}
}
