using System;
using Haiku.App;
using Haiku.Interface;
using Haiku.Testing;

/// <summary>
/// Regression coverage for Haiku.Interface.View (see
/// managed/Haiku.Interface/View.cs, native/include/hs_view.h, and
/// Window.cs's AddChild(View)/RemoveChild(View)) -- the "shell + drawing
/// only" slice: construction/geometry, attach/detach hooks firing off
/// Window.AddChild()/RemoveChild(), and the ownership rules hs_view.h
/// documents as stricter than Window's own (Dispose() throws rather than
/// silently corrupting a parent's child list -- see View.cs's own
/// Dispose() remarks).
///
/// Follows WindowTests' now-established pattern of opening (and disposing,
/// via `using`) a fresh, never-Run() BApplication per test method rather
/// than sharing one static instance across the class -- see WindowTests'
/// own class remarks for why that repeat-construct-and-dispose pattern is
/// the verified-safe one here, distinct from ApplicationTests' one-shot
/// Run()-to-quit caveat.
///
/// None of these tests ever call Show() on their Window: per
/// hs_window.h's own hs_window_add_child() doc, AttachedToWindow() fires
/// on an added child synchronously, within the AddChild() call itself, on
/// whatever thread calls it -- a window never itself needs to "become
/// attached" to anything first, unlike a View nested inside another View
/// that isn't attached to a window yet. That means every test below can
/// use an unshown Window (so its Dispose() takes the synchronous
/// native-destroy path -- see Window.cs's own Dispose() remarks -- rather
/// than the asynchronous Quit()-and-poll path), which keeps them both
/// simple and fast. None of the tests here override OnQuitRequested or
/// OnDestroyed on the window itself either.
///
/// THERE IS NO Draw() TEST IN *THIS* FILE. There used to be an attempt
/// at one here (a ProbeView overriding OnDraw, added to a Show()n window
/// from this test method's own thread, polled for a DrawFired flag); it
/// reliably hung the whole Tests.exe process shortly after OnDraw() fired
/// for the first time -- not a flaky failure, a hang, which blocks every
/// test after it and never returns an exit code. See KNOWN_ISSUES.md
/// issue #4 for the full investigation and, since fixed, its resolution:
/// Draw() now DOES have automated coverage, in
/// managed/Tests/ApplicationTests.cs, but it could not be added here.
/// The fix was to build and Show() the probe window from inside
/// OnReadyToRun(), on the same thread that calls Application.Run() --
/// exactly the pattern that was already proven safe by Sample.exe -- and
/// that requires a real Run()-to-quit cycle, which issue #1 (see
/// KNOWN_ISSUES.md) restricts to exactly one test in this whole process.
/// ApplicationTests.cs already owned that one slot, so the Draw() check
/// was folded into its existing test rather than added as a second one
/// here. Read ApplicationTests.cs's class remarks before attempting to
/// add a Draw()-firing test to THIS file -- the original hang-prone
/// shape (Show() from a test method's own thread, Application never
/// Run()) is still exactly as dangerous as this file's remarks above
/// describe, regardless of the fix elsewhere.
/// </summary>
[Haiku.Testing.TestModule("BView")]
public class ViewTests
{
	private const string AppSignature = "application/x-vnd.HaikuSharp-Tests-View";

	private class ProbeView : View
	{
		public volatile bool AttachedToWindowFired;
		public volatile bool DetachedFromWindowFired;
		public volatile bool DestroyedFired;

		public ProbeView(Rect frame, string name)
			: base(frame, name)
		{
		}

		protected override void OnAttachedToWindow()
		{
			AttachedToWindowFired = true;
		}

		protected override void OnDetachedFromWindow()
		{
			DetachedFromWindowFired = true;
		}

		protected override void OnDestroyed()
		{
			DestroyedFired = true;
		}
	}

	[Test]
	public void ConstructionAndGeometryRoundTrip()
	{
		// Deliberately no Application/Window here -- a BView needs no
		// app_server connection to exist, only to actually appear on
		// screen (see the class remarks' "most tests never call Show()"
		// point, one level further: this one needs no Window at all).
		Rect frame = new Rect(10, 20, 110, 220);
		View view = new View(frame, "geometry probe");

		Assert.AreEqual(frame, view.Frame, "Frame should read back exactly what the constructor was given");

		Rect bounds = view.Bounds;
		Assert.AreEqual(0f, bounds.Left, "Bounds is always expressed in the view's own coordinate system, so Left must be 0");
		Assert.AreEqual(0f, bounds.Top, "Bounds is always expressed in the view's own coordinate system, so Top must be 0");
		Assert.AreEqual(frame.Right - frame.Left, bounds.Right - bounds.Left, "Bounds width should match the frame's width");
		Assert.AreEqual(frame.Bottom - frame.Top, bounds.Bottom - bounds.Top, "Bounds height should match the frame's height");

		view.MoveTo(5, 7);
		Rect afterMove = view.Frame;
		Assert.AreEqual(5f, afterMove.Left, "MoveTo should update Frame.Left");
		Assert.AreEqual(7f, afterMove.Top, "MoveTo should update Frame.Top");
		Assert.AreEqual(frame.Right - frame.Left, afterMove.Right - afterMove.Left, "MoveTo should not change the view's width");
		Assert.AreEqual(frame.Bottom - frame.Top, afterMove.Bottom - afterMove.Top, "MoveTo should not change the view's height");

		view.ResizeTo(50, 60);
		Rect afterResize = view.Frame;
		Assert.AreEqual(5f, afterResize.Left, "ResizeTo should not change Frame.Left");
		Assert.AreEqual(7f, afterResize.Top, "ResizeTo should not change Frame.Top");
		Assert.AreEqual(50f, afterResize.Right - afterResize.Left, "ResizeTo should set the new width");
		Assert.AreEqual(60f, afterResize.Bottom - afterResize.Top, "ResizeTo should set the new height");

		// Never added to any parent -- Dispose() should succeed and fire
		// OnDestroyed synchronously, right here.
		view.Dispose();
	}

	[Test]
	public void AddChildFiresAttachedToWindowSynchronously()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 100, 100), "ViewTests probe window")) {
				ProbeView view = new ProbeView(new Rect(0, 0, 50, 50), "probe");

				window.AddChild(view);

				Assert.IsTrue(view.AttachedToWindowFired,
					"OnAttachedToWindow should fire synchronously, within AddChild(), even though this window has never been shown");

				// Leave view attached -- window's own Dispose() below (never
				// shown, so a synchronous native destroy) cascades into it,
				// exercised separately by WindowDisposeCascadesDestroyToAttachedChild.
			}
		}
	}

	[Test]
	public void RemoveChildFiresDetachedFromWindowAndDetaches()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 100, 100), "ViewTests probe window")) {
				ProbeView view = new ProbeView(new Rect(0, 0, 50, 50), "probe");
				window.AddChild(view);

				bool removed = window.RemoveChild(view);

				Assert.IsTrue(removed, "RemoveChild should return true for a view that actually was a direct child");
				Assert.IsTrue(view.DetachedFromWindowFired, "OnDetachedFromWindow should fire as part of RemoveChild()");

				// No longer attached -- Dispose() should now succeed rather
				// than throw (see DisposeWhileAttachedThrows for the
				// still-attached case).
				view.Dispose();
				Assert.IsTrue(view.DestroyedFired, "Dispose() on a detached view should fire OnDestroyed");
			}
		}
	}

	[Test]
	public void RemoveChildReturnsFalseForNonChild()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 100, 100), "ViewTests probe window"))
			using (View view = new View(new Rect(0, 0, 50, 50), "never added")) {
				bool removed = window.RemoveChild(view);

				Assert.IsFalse(removed, "RemoveChild should return false for a view that was never added to this window");
			}
		}
	}

	[Test]
	public void DisposeWhileAttachedThrows()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 100, 100), "ViewTests probe window")) {
				View view = new View(new Rect(0, 0, 50, 50), "probe");
				window.AddChild(view);

				bool threw = false;
				try {
					view.Dispose();
				} catch (InvalidOperationException) {
					threw = true;
				}

				Assert.IsTrue(threw,
					"Dispose() on a still-attached View should throw InvalidOperationException rather than risk corrupting its parent's child list");

				// Leave it attached -- window's own Dispose() below cleans it
				// up via the implicit cascade (see hs_view.h's OWNERSHIP note).
			}
		}
	}

	[Test]
	public void WindowDisposeCascadesDestroyToAttachedChild()
	{
		using (new Application(AppSignature)) {
			ProbeView view = new ProbeView(new Rect(0, 0, 50, 50), "probe");

			using (Window window = new Window(new Rect(0, 0, 100, 100), "ViewTests probe window")) {
				window.AddChild(view);
				// window.Dispose() fires here, at the end of this `using`
				// block -- never shown, so it deletes the native BWindow
				// synchronously, which recursively deletes its still-
				// attached children, including this view, with no explicit
				// RemoveChild() or Dispose() call on the view at all.
			}

			Assert.IsTrue(view.DestroyedFired,
				"OnDestroyed should fire on a still-attached child view when its parent window is destroyed, even though nothing disposed the view directly");
		}
	}
}
