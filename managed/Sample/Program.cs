using System;
using System.Text;
using Haiku.App;
using Haiku.Interface;

/*
 * A minimal, runnable example of this binding's Interface Kit slice: a
 * BWindow you can actually see and close, containing a BView that actually
 * draws something and now also responds to mouse and keyboard input. This
 * is NOT where verification of the binding lives -- see managed/Tests/
 * (built as Tests.exe) for the actual regression suite, including
 * WindowTests.QuitPostsRequestAndFiresDestroyedCallback, ViewTests'
 * construction/attach/detach/ownership coverage, and ViewInputTests'
 * MakeFocus/IsFocus/Invalidate/enum-value coverage, which exercise these
 * same paths without needing a human to look at anything (real hook-firing
 * itself has no automated coverage -- see ViewInputTests.cs's class
 * remarks for why, same reasoning as Draw() itself in KNOWN_ISSUES.md
 * issue #4). This file exists purely to be a clear, working starting point
 * for your own windowed app, and -- for DemoView specifically -- to answer
 * a real open question: see DemoView's OnDraw remarks below.
 *
 * DemoWindow uses WindowFlags.QuitOnWindowClose, so clicking its title bar's
 * close box doesn't just end the window -- BWindow.h's own flag semantics
 * make it signal the owning BApplication to quit too, which is what lets
 * this Main() (blocked in app.Run(), same as the Application Kit sample)
 * return once you close the window.
 */
public class DemoView : View
{
	// State captured from the input hooks below, redrawn by OnDraw. All of
	// this is only ever touched from the owning window's message-loop
	// thread -- the same thread that fires these hooks and OnDraw itself
	// (see hs_view.h's threading note) -- so no locking is needed here.
	private string _lastEvent = "(none yet -- move the mouse over this view, click, or type)";
	private Point _lastMousePos;
	private MouseButtons _lastButtons;
	private ModifierKeys _lastModifiers;
	private string _lastKeyDown = "(none yet)";
	private string _lastKeyUp = "(none yet)";

	public DemoView()
		: base(new Rect(20, 20, 380, 210), "demo view")
	{
	}

	protected override void OnAttachedToWindow()
	{
		Console.WriteLine("[2] DemoView attached to its window.");

		// Give this view keyboard focus right away, so OnKeyDown/OnKeyUp
		// fire without requiring a click first -- verified against View.h
		// that MakeFocus works regardless of ViewFlags/B_NAVIGABLE (see
		// hs_view.h's MOUSE AND KEYBOARD INPUT note); safe to call here
		// since AttachedToWindow already fires on the window's own thread
		// with the view fully attached.
		MakeFocus(true);
	}

	protected override void OnMouseDown(Point where, MouseButtons buttons, ModifierKeys modifiers)
	{
		_lastEvent = "MouseDown at " + Describe(where) + ", buttons=" + buttons;
		_lastMousePos = where;
		_lastButtons = buttons;
		_lastModifiers = modifiers;
		Console.WriteLine("[6] " + _lastEvent);
		Invalidate();
	}

	protected override void OnMouseUp(Point where, ModifierKeys modifiers)
	{
		// No buttons parameter here -- B_MOUSE_UP carries no "buttons"
		// field, unlike B_MOUSE_DOWN/B_MOUSE_MOVED (verified against the
		// Be Book's message-constants docs, not assumed; see hs_view.h).
		// It DOES carry modifiers, the asymmetry runs the other way for
		// that field -- see hs_view.h's MODIFIERS note.
		_lastEvent = "MouseUp at " + Describe(where);
		_lastMousePos = where;
		_lastButtons = MouseButtons.None;
		_lastModifiers = modifiers;
		Console.WriteLine("[6] " + _lastEvent);
		Invalidate();
	}

	protected override void OnMouseMoved(Point where, MouseTransit transit, MouseButtons buttons)
	{
		_lastMousePos = where;
		_lastButtons = buttons;

		// No modifiers parameter on this hook -- B_MOUSE_MOVED genuinely
		// carries no "modifiers" field, unlike the other four input hooks
		// (see hs_view.h's MODIFIERS note). Modifiers.Current is exactly
		// the fallback that note describes for a case like this one.
		_lastModifiers = Modifiers.Current;

		// Only log the Entered/Exited transitions to the console -- plain
		// in-view movement fires this hook continuously and would flood
		// stdout, but the view still redraws every time so the on-screen
		// position tracks the pointer live.
		if (transit == MouseTransit.Entered || transit == MouseTransit.Exited) {
			_lastEvent = "MouseMoved (" + transit + ") at " + Describe(where);
			Console.WriteLine("[6] " + _lastEvent);
		} else {
			_lastEvent = "MouseMoved at " + Describe(where);
		}
		Invalidate();
	}

	protected override void OnKeyDown(byte[] bytes, ModifierKeys modifiers)
	{
		_lastKeyDown = Describe(bytes);
		_lastModifiers = modifiers;
		Console.WriteLine("[6] KeyDown: " + _lastKeyDown + ", modifiers=" + modifiers);
		Invalidate();
	}

	protected override void OnKeyUp(byte[] bytes, ModifierKeys modifiers)
	{
		_lastKeyUp = Describe(bytes);
		_lastModifiers = modifiers;
		Console.WriteLine("[6] KeyUp: " + _lastKeyUp + ", modifiers=" + modifiers);
		Invalidate();
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

		// Mouse/keyboard input slice: redraw whatever the hooks above last
		// captured, so moving the mouse, clicking, and typing all show up
		// here live (each hook calls Invalidate() to trigger this).
		DrawString("Last event: " + _lastEvent, new Point(10, 50));
		DrawString("Mouse position: " + Describe(_lastMousePos) + "   Buttons down: " + _lastButtons,
			new Point(10, 68));
		DrawString("Last key down: " + _lastKeyDown, new Point(10, 86));
		DrawString("Last key up: " + _lastKeyUp, new Point(10, 104));
		DrawString("Modifiers held: " + _lastModifiers, new Point(10, 122));
		DrawString("(this view has keyboard focus -- just type, try holding Shift/Ctrl/Option/Command)",
			new Point(10, 148));
	}

	private static string Describe(Point point)
	{
		return "(" + point.X + ", " + point.Y + ")";
	}

	// Turns a raw KeyDown/KeyUp byte sequence into something readable:
	// the friendly name for a single-byte control character we know about
	// (see KeyBytes.cs), the literal character for anything printable, or
	// a hex dump otherwise. Deliberately does not attempt function-key
	// (F1-F12) identification -- that needs the raw message fields, out of
	// scope for this basic-hooks slice (see hs_view.h's MOUSE AND KEYBOARD
	// INPUT note).
	private static string Describe(byte[] bytes)
	{
		if (bytes.Length == 1) {
			byte b = bytes[0];
			string name = NameForControlByte(b);
			if (name != null)
				return name + " (0x" + b.ToString("x2") + ")";
			if (b >= 0x20 && b < 0x7f)
				return "'" + (char)b + "'";
		}

		StringBuilder hex = new StringBuilder();
		foreach (byte b in bytes) {
			if (hex.Length > 0)
				hex.Append(' ');
			hex.Append("0x").Append(b.ToString("x2"));
		}
		return hex.Length > 0 ? hex.ToString() : "(empty)";
	}

	private static string NameForControlByte(byte b)
	{
		if (b == KeyBytes.Home) return "Home";
		if (b == KeyBytes.End) return "End";
		if (b == KeyBytes.Insert) return "Insert";
		if (b == KeyBytes.Backspace) return "Backspace";
		if (b == KeyBytes.Tab) return "Tab";
		if (b == KeyBytes.Return) return "Return/Enter";
		if (b == KeyBytes.PageUp) return "PageUp";
		if (b == KeyBytes.PageDown) return "PageDown";
		if (b == KeyBytes.Escape) return "Escape";
		if (b == KeyBytes.LeftArrow) return "LeftArrow";
		if (b == KeyBytes.RightArrow) return "RightArrow";
		if (b == KeyBytes.UpArrow) return "UpArrow";
		if (b == KeyBytes.DownArrow) return "DownArrow";
		if (b == KeyBytes.Space) return "Space";
		if (b == KeyBytes.Delete) return "Delete";
		return null;
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
		Console.WriteLine("[2b] Window shown -- move/click the mouse over it or type, close it (its title bar's close box) to quit.");
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
