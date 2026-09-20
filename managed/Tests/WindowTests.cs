using System.Threading;
using Haiku.App;
using Haiku.Interface;
using Haiku.Testing;

/// <summary>
/// Regression coverage for Haiku.Interface.Window's lifecycle (see
/// managed/Haiku.Interface/Window.cs and native/include/hs_window.h) --
/// specifically the two things that make BWindow's lifecycle different from
/// BApplication's: there's no blocking "wait for this window" call, so
/// finding out a window actually died means polling for OnDestroyed(); and
/// Quit() has to work by posting B_QUIT_REQUESTED rather than calling
/// BWindow::Quit() directly, per that header's "QUITTING" note.
///
/// EACH test method here opens its own BApplication in a `using` block and
/// disposes it before returning, rather than sharing one across the whole
/// class. Constructing and disposing a never-Run() BApplication is safe to
/// repeat as many times as you like; see ApplicationTests' own [TestOrder]
/// remarks for the related (but distinct) fact about Run() being a
/// one-shot operation for the whole process.
///
/// ProbeWindow below deliberately does NOT override OnQuitRequested, even
/// though Window fully supports it (Window.cs always wires up the native
/// callback regardless of whether a subclass overrides the hook -- the
/// base Window.OnQuitRequested() just returns true, same effective result
/// BWindow's own default does). This is a deliberate, empirically-forced
/// choice, not an oversight: on real Haiku hardware, having a ProbeWindow
/// subclass override OnQuitRequested here -- even trivially, just to set a
/// bool field -- reliably causes ApplicationTests' later, separate
/// BApplication.Run() call to hang indefinitely, while leaving that
/// override out does not. Both configurations fire the exact same native
/// callback on the window's own thread either way (Window.cs's wiring is
/// unconditional); only whether a C# override exists made the difference,
/// pinned down by bisection against the real files, not guessed. The
/// underlying mechanism isn't nailed down (a foreign-thread/GC interaction
/// in Mono's embedding layer is the leading suspect, given how it
/// resembles the already-documented "spawned native thread calling into
/// unattached Mono" story in hs_application.h), but the workaround is
/// simple and the coverage loss is small: DestroyedFired becoming true
/// already proves QuitRequested() returned true somewhere in the chain
/// (destruction can't happen otherwise), so this test still verifies the
/// whole quit pipeline end to end without needing to observe
/// OnQuitRequested directly. If you need to actually override
/// OnQuitRequested in a future test, expect to hit this -- see the
/// commit that added this comment for the bisection that found it.
/// </summary>
[Haiku.Testing.TestModule("Interface Kit")]
public class WindowTests
{
	private class ProbeWindow : Window
	{
		public volatile bool DestroyedFired;

		public ProbeWindow()
			: base(new Rect(0, 0, 100, 100), "WindowTests probe")
		{
		}

		protected override void OnDestroyed()
		{
			DestroyedFired = true;
		}
	}

	[Test]
	public void QuitPostsRequestAndFiresDestroyedCallback()
	{
		// A BWindow needs a live BApplication already connected to the
		// app_server before it can be constructed -- unlike the
		// message-loop thread (which only starts on Show()), that
		// connection is set up by BApplication's constructor itself, no
		// Run() required. This `using` never calls Run() on it, so
		// disposing it at the end of this method is always safe to repeat
		// in a later test (see the class remarks above).
		using (new Application("application/x-vnd.HaikuSharp-Tests-Window")) {
			ProbeWindow window = new ProbeWindow();
			window.Show();
			window.Quit();

			// No blocking "wait for this window" call exists (unlike
			// Application.Run()), so poll for OnDestroyed with a generous
			// timeout instead of either an instant (flaky) check or an
			// infinite (hang-forever-on-regression) wait.
			const int timeoutMs = 3000;
			const int pollIntervalMs = 10;
			int waited = 0;
			while (!window.DestroyedFired && waited < timeoutMs) {
				Thread.Sleep(pollIntervalMs);
				waited += pollIntervalMs;
			}

			Assert.IsTrue(window.DestroyedFired, "OnDestroyed should have fired within " + timeoutMs + "ms of Quit() (which requires QuitRequested() to have returned true along the way)");
		}
	}
}
