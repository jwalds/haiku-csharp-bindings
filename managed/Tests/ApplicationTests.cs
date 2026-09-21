using Haiku.App;
using Haiku.Interface;
using Haiku.Testing;

/// <summary>
/// Regression coverage for Haiku.App.Application/BLooper's threading model
/// (see managed/Haiku.App/Application.cs and native/include/hs_application.h) --
/// this used to be Sample/Program.cs's whole reason for existing, proven by
/// eye via four numbered Console.WriteLine calls. It's an integration test,
/// not a fast isolated unit test (it spins up a real BApplication and blocks
/// on a real native message loop), but it belongs here rather than nowhere:
/// this is the one test that would catch it if Mono's embedding layer ever
/// stopped auto-attaching BLooper's native message-loop thread on first
/// entry into managed code -- see hs_application.h's threading note for why
/// that was never a given.
///
/// [TestOrder(100)] -- MUST run after every other test class that needs a
/// live BApplication (currently just WindowTests -- see its own remarks):
/// empirically verified on real Haiku hardware, once a BApplication's
/// Run() has spawned its message-loop thread and that thread has quit and
/// self-deleted the object, NO further BApplication can EVER be
/// constructed again in that same process -- not even a plain, never-run
/// one. (Constructing and disposing a never-run BApplication doesn't have
/// this effect; only a full Run()-to-quit cycle does.) Since this test is
/// the only place in the whole suite that runs a BApplication through that
/// full cycle, it has to run dead last, or it silently poisons every test
/// after it for the rest of the process. This isn't a guess -- see the
/// commit that added [TestOrder] for the throwaway repro that pinned it
/// down (two BApplications, neither ever coexisting, second construction
/// still hangs indefinitely) before assuming it was a WindowTests bug.
///
/// THIS ALSO CARRIES THIS PROJECT'S ONLY AUTOMATED Draw()-FIRING TEST --
/// see KNOWN_ISSUES.md issue #4 before touching ProbeDrawWindow/
/// ProbeDrawView. The original attempt at this (long since removed from
/// ViewTests.cs) showed a window from the TEST METHOD'S OWN thread while
/// Application was never Run() at all, and reliably hung the whole
/// process the first time Draw() fired, for reasons never root-caused.
/// This version deliberately does the opposite: the window and view are
/// built INSIDE OnReadyToRun(), on the same thread that calls Run() --
/// exactly the pattern Sample.exe's DemoView already proved safe on real
/// hardware (see issue #4's "UPDATE" paragraph) -- and it can't just be
/// bolted on as a second [Test] method, because issue #1 above means only
/// ONE test in this entire process may ever run a full Run()-to-quit
/// cycle. So the Draw() check is folded into this same method and this
/// same ProbeApplication instance instead.
///
/// A SECOND, LESS OBVIOUS CHANGE THIS REQUIRED: the original version of
/// this test ended Run() itself, by having OnMessageReceived respond to
/// its own self-posted PING with PostMessage(SystemMessages.QuitRequested)
/// -- entirely on the app's own thread, near-instantly. If that trigger
/// were left in place alongside the new window, it would almost certainly
/// win the race and quit the app before Draw() ever got a chance to fire
/// on the window's own (separate) thread, since Draw() only happens once
/// app_server has actually scheduled a paint. So that self-quit was
/// removed: ProbeDrawView.OnDraw() is now the ONLY thing that ends Run(),
/// by calling ProbeDrawWindow.Quit() (documented safe from any thread,
/// including the window's own -- see hs_window.h's QUITTING note) on a
/// window constructed with WindowFlags.QuitOnWindowClose. That flag is
/// native BWindow behavior (not anything this shim adds): once a window
/// built with it completes its own quit flow, BWindow itself additionally
/// posts B_QUIT_REQUESTED to the owning BApplication -- the exact same
/// mechanism Sample.exe's DemoWindow already relies on for its close-box
/// click (see hs_window.h's own callback-listing comment, "by the user
/// clicking its close box, by hs_window_quit(), by
/// B_QUIT_ON_WINDOW_CLOSE"), just triggered programmatically here instead
/// of by a UI event.
///
/// ProbeDrawWindow deliberately does NOT override OnQuitRequested -- see
/// WindowTests.cs's own remarks and KNOWN_ISSUES.md issue #2 for why that
/// reliably poisons the process for any BApplication constructed
/// afterward (not a concern for THIS process, since this is already the
/// dead-last [TestOrder(100)] test and nothing runs after it, but there is
/// no reason to take the risk when the default OnQuitRequested -- allow
/// it -- is all this test needs).
///
/// If this test ever hangs again on real hardware, that is itself a
/// finding worth adding to KNOWN_ISSUES.md issue #4, not something to
/// silently work around -- see that issue's "Where to pick this up" note,
/// which is exactly the approach implemented here.
/// </summary>
[Haiku.Testing.TestModule("Application Kit")]
[Haiku.Testing.TestOrder(100)]
public class ApplicationTests
{
	private const uint PingMessage = 0x50494E47; // 'PING'

	private class ProbeDrawView : View
	{
		public volatile bool DrawFired;
		public Rect LastUpdateRect;

		private readonly ProbeDrawWindow _owningWindow;

		public ProbeDrawView(ProbeDrawWindow owningWindow)
			: base(new Rect(0, 0, 50, 50), "draw probe")
		{
			_owningWindow = owningWindow;
		}

		protected override void OnDraw(Rect updateRect)
		{
			LastUpdateRect = updateRect;
			DrawFired = true;

			// Ends this whole test: see the class remarks above for why
			// this -- not the PING/PONG message round trip below -- is
			// the thing that actually terminates Application.Run().
			_owningWindow.Quit();
		}
	}

	private class ProbeDrawWindow : Window
	{
		public ProbeDrawWindow()
			: base(new Rect(0, 0, 100, 100), "ApplicationTests draw probe",
				WindowLook.Titled, WindowFeel.Normal, WindowFlags.QuitOnWindowClose)
		{
		}
	}

	private class ProbeApplication : Application
	{
		public bool ReadyToRunFired;
		public bool MessageReceivedFired;
		public string ReceivedGreeting;
		public bool QuitRequestedFired;
		public ProbeDrawView DrawView;

		public ProbeApplication() : base("application/x-vnd.HaikuSharp-Tests")
		{
		}

		protected override void OnReadyToRun()
		{
			ReadyToRunFired = true;

			// Built here, on the thread that is about to call Run() --
			// see the class remarks above for why that (and not the test
			// method's own thread, with Application never Run()) is the
			// pattern already proven safe on real hardware.
			ProbeDrawWindow window = new ProbeDrawWindow();
			DrawView = new ProbeDrawView(window);
			window.AddChild(DrawView);
			window.Show();

			// Unrelated to the Draw() check above -- this is the original
			// PING/PONG round trip, still verifying OnReadyToRun can post
			// to itself and OnMessageReceived receives it. It no longer
			// triggers quit itself (see class remarks); ProbeDrawView's
			// OnDraw() does that now.
			using (Message ping = new Message(PingMessage)) {
				ping.AddString("greeting", "probe");
				PostMessage(ping);
			}
		}

		protected override void OnMessageReceived(Message message)
		{
			if (message.What == PingMessage) {
				MessageReceivedFired = true;
				ReceivedGreeting = message.FindString("greeting");
			}
		}

		protected override bool OnQuitRequested()
		{
			QuitRequestedFired = true;
			return true;
		}
	}

	[Test]
	public void ReadyToRunMessageAndQuitRequestedAllFireInOrder()
	{
		ProbeApplication app = new ProbeApplication();
		try {
			app.Run(); // blocks until ProbeDrawView.OnDraw() fires and quits the window (see class remarks)
		} catch (HaikuException ex) {
			Assert.Fail("Application.Run() threw: " + ex.Message);
		}

		Assert.IsTrue(app.ReadyToRunFired, "OnReadyToRun should have fired on the looper thread");
		Assert.IsTrue(app.MessageReceivedFired, "OnMessageReceived should have fired for our own PING");
		Assert.AreEqual("probe", app.ReceivedGreeting, "the message posted from OnReadyToRun should round-trip");
		Assert.IsTrue(app.QuitRequestedFired, "OnQuitRequested should have fired");

		Assert.IsTrue(app.DrawView.DrawFired,
			"OnDraw should have fired on the probe window's own thread -- see KNOWN_ISSUES.md issue #4 "
			+ "if this ever fails or hangs instead of failing cleanly");
		Rect updateRect = app.DrawView.LastUpdateRect;
		Assert.IsTrue(updateRect.Right > updateRect.Left && updateRect.Bottom > updateRect.Top,
			"the update rect Draw() received should be a real, non-empty rect, not a default/zeroed struct");
	}
}
