using System;
using Haiku.App;
using Haiku.Interface;

/*
 * A minimal, runnable example of this binding's Interface Kit slice: a
 * BWindow you can actually see and close, containing a BView that actually
 * draws something. This is NOT where verification of the binding lives --
 * see managed/Tests/ (built as Tests.exe) for the actual regression suite,
 * including WindowTests.QuitPostsRequestAndFiresDestroyedCallback and
 * ViewTests' construction/attach/detach/ownership coverage, which exercise
 * these same paths without needing a human to look at anything. This file
 * exists purely to be a clear, working starting point for your own
 * windowed app, and -- for DemoView specifically -- to answer a real open
 * question: see DemoView's own remarks below.
 *
 * DemoWindow uses WindowFlags.QuitOnWindowClose, so clicking its title bar's
 * close box doesn't just end the window -- BWindow.h's own flag semantics
 * make it signal the owning BApplication to quit too, which is what lets
 * this Main() (blocked in app.Run(), same as the Application Kit sample)
 * return once you close the window.
 */
public class DemoView : View
{
	public DemoView()
		: base(new Rect(20, 20, 380, 210), "demo view")
	{
	}

	protected override void OnAttachedToWindow()
	{
		Console.WriteLine("[2] DemoView attached to its window.");
	}

	/*
	 * KNOWN_ISSUES.md issue #4 open question: a hand-rolled test (since
	 * removed from managed/Tests/ViewTests.cs -- see that file's own
	 * remarks and the issue write-up) reliably hung the whole process
	 * shortly after a view's first OnDraw() call returned, when the
	 * window was Show()n from a thread other than the one running
	 * Application.Run(), and Run() itself was never called at all. This
	 * DemoView is the first real-usage-shaped test of whether that hang
	 * also happens here, where the window (and this view) are created
	 * from OnReadyToRun -- the app's OWN thread, the same one blocked in
	 * app.Run() in Main() below -- which is how every real app using this
	 * binding is expected to work. If you're reading this because the
	 * demo window appeared but then never responded to anything
	 * (including its own close box) once this text got drawn, that's
	 * issue #4 reproducing here too; update that issue's "Not yet known"
	 * section with this finding rather than re-discovering it.
	 */
	protected override void OnDraw(Rect updateRect)
	{
		Console.WriteLine("[3] DemoView.OnDraw fired, updateRect=" + updateRect);

		// FillRect() paints with the current HIGH color (BeAPI's default
		// B_SOLID_HIGH pattern), NOT the view color -- SetViewColor() only
		// controls the color app_server auto-erases with before Draw() is
		// even called, a separate thing. Set high color to the fill color
		// right before each drawing call that should use it.
		SetHighColor(new RgbColor(216, 216, 255));
		FillRect(Bounds);

		SetHighColor(new RgbColor(40, 40, 40));
		StrokeRect(Bounds);

		SetHighColor(new RgbColor(0, 0, 0));
		DrawString("Hello from a C# BView, drawn by real BeAPI calls.", new Point(10, 20));
		StrokeLine(new Point(10, 30), new Point(Bounds.Right - 10, 30));
	}
}

public class DemoWindow : Window
{
	public DemoWindow()
		: base(new Rect(100, 100, 500, 350), "Haiku C# Bindings Demo",
			WindowLook.Titled, WindowFeel.Normal, WindowFlags.QuitOnWindowClose)
	{
		AddChild(new DemoView());
	}

	protected override void OnDestroyed()
	{
		Console.WriteLine("[4] DemoWindow destroyed.");
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
		Console.WriteLine("[2b] Window shown -- close it (its title bar's close box) to quit.");
	}

	protected override bool OnQuitRequested()
	{
		Console.WriteLine("[5] Application OnQuitRequested fired -- allowing shutdown.");
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
