using System;
using Haiku.App;
using Haiku.Interface;
using Haiku.Testing;

/// <summary>
/// Regression coverage for Haiku.Interface.ScrollView (see
/// managed/Haiku.Interface/ScrollView.cs and native/include/
/// hs_scroll_view.h) -- added to close a real, user-reported gap: a
/// plain ListView renders with no visible scrollbar, unlike the real
/// BeOS "Sliders, Tabs &amp; Lists" demo app the user compared it
/// against.
///
/// THE CENTRAL THING THIS FILE PROVES, NOT JUST ASSUMES: that
/// hs_scroll_view_create() really does reparent its target natively
/// (gdb-disassembly-confirmed, see hs_scroll_view.h's WRAPPING AND
/// REPARENTING note), with no separate View.AddChild() call from managed
/// code. Every test below that wraps a target and then checks
/// target.Dispose() throws, or checks target's OnDestroyed fires when
/// the ScrollView (or its own parent window) is destroyed, is exercising
/// that claim directly -- if hs_scroll_view_create() had silently failed
/// to reparent target, target._hasParent would still read true (this
/// managed wrapper sets it unconditionally after the native call
/// returns) but the native object graph would NOT actually match, and
/// WindowDisposeCascadesToScrollViewAndItsTarget below would fail
/// (target's destroyed callback would never fire, since nothing native
/// would actually own it).
///
/// Every test wraps in `using (new Application(...))`, matching this
/// suite's established conservative default for every BView-family
/// construction (see CheckBoxTests.cs's own remarks on why that default
/// exists even for a type that might not strictly need it) -- whether
/// BScrollView specifically requires a live BApplication at construction
/// was not separately verified on hardware for this slice, and there is
/// no reason to find out the hard way inside a shared-process test run.
/// </summary>
[Haiku.Testing.TestModule("BScrollView")]
public class ScrollViewTests
{
	private const string AppSignature = "application/x-vnd.HaikuSharp-Tests-ScrollView";

	private class ProbeView : View
	{
		public volatile bool DestroyedFired;

		public ProbeView(Rect frame, string name)
			: base(frame, name)
		{
		}

		protected override void OnDestroyed()
		{
			DestroyedFired = true;
		}
	}

	private class ProbeScrollView : ScrollView
	{
		public volatile bool DestroyedFired;

		public ProbeScrollView(string name, ViewBase target, bool horizontal, bool vertical)
			: base(name, target, horizontal, vertical)
		{
		}

		protected override void OnDestroyed()
		{
			DestroyedFired = true;
		}
	}

	[Test]
	public void ConstructionReparentsTargetAndDoesNotThrow()
	{
		using (new Application(AppSignature)) {
			View target = new View(new Rect(0, 0, 200, 100), "scroll target");
			ScrollView scrollView = new ScrollView("wrapper", target, false, true);

			// hs_scroll_view_create() reparents target natively (see this
			// class's own remarks) -- the managed constructor records
			// that by setting target._hasParent = true, which is enough
			// on its own to make target.Dispose() throw.
			bool threw = false;
			try {
				target.Dispose();
			} catch (InvalidOperationException) {
				threw = true;
			}
			Assert.IsTrue(threw,
				"target.Dispose() should throw once it's wrapped by a ScrollView -- ScrollView construction reparents it natively, exactly like AddChild() would");

			// The wrapper itself was never added to anything -- safe to
			// dispose directly; this cascades into target's own native
			// destruction too (proven separately below).
			scrollView.Dispose();
		}
	}

	[Test]
	public void GeometryMoveToAndResizeToRoundTrip()
	{
		using (new Application(AppSignature)) {
			View target = new View(new Rect(0, 0, 200, 100), "scroll target");
			ScrollView scrollView = new ScrollView("wrapper", target, false, true);

			// BScrollView computes its own initial frame from target's
			// frame/PreferredSize plus border and scrollbar thickness --
			// unlike every other hs_*_create() in this binding, there is
			// no frame parameter to read back verbatim (see
			// hs_scroll_view.h/ScrollView.cs: it isn't part of the real
			// BScrollView constructor's own signature either). What IS
			// verifiable is that MoveTo/ResizeTo -- reused unchanged from
			// hs_view_move_to()/hs_view_resize_to(), same ABI-offset-0
			// reasoning as every other HSView-family handle -- work the
			// ordinary way against a ScrollView handle.
			scrollView.MoveTo(15, 25);
			Rect afterMove = scrollView.Frame;
			Assert.AreEqual(15f, afterMove.Left, "MoveTo should update Frame.Left");
			Assert.AreEqual(25f, afterMove.Top, "MoveTo should update Frame.Top");

			scrollView.ResizeTo(220, 120);
			Rect afterResize = scrollView.Frame;
			Assert.AreEqual(220f, afterResize.Right - afterResize.Left, "ResizeTo should set the new width");
			Assert.AreEqual(120f, afterResize.Bottom - afterResize.Top, "ResizeTo should set the new height");

			scrollView.Dispose();
		}
	}

	[Test]
	public void WrapsAListViewJustAsWellAsAPlainView()
	{
		// The whole point of this feature -- ListView is what prompted
		// it (see this class's own remarks) -- but hs_scroll_view_create()
		// is generic over any HSView-family handle, not ListView-specific.
		using (new Application(AppSignature)) {
			ListView listView = new ListView(new Rect(0, 0, 180, 120), "list target");
			ScrollView scrollView = new ScrollView("list wrapper", listView, false, true);

			bool threw = false;
			try {
				listView.Dispose();
			} catch (InvalidOperationException) {
				threw = true;
			}
			Assert.IsTrue(threw, "A wrapped ListView should throw on direct Dispose(), same as a wrapped plain View");

			scrollView.Dispose();
		}
	}

	[Test]
	public void ConstructorNullTargetThrows()
	{
		using (new Application(AppSignature)) {
			bool threw = false;
			try {
				new ScrollView("wrapper", null, false, true);
			} catch (ArgumentNullException) {
				threw = true;
			}
			Assert.IsTrue(threw, "Constructing a ScrollView with a null target should throw ArgumentNullException");
		}
	}

	[Test]
	public void AddChildUnderWindowSucceeds()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 240, 160), "ScrollViewTests probe window")) {
				View target = new View(new Rect(0, 0, 200, 100), "scroll target");
				ScrollView scrollView = new ScrollView("wrapper", target, false, true);

				window.AddChild(scrollView);
				// Leave attached -- window's Dispose() cascades into it
				// (and, through it, into target too), exercised below.
			}
		}
	}

	[Test]
	public void DisposeWhileAttachedThrows()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 240, 160), "ScrollViewTests probe window")) {
				View target = new View(new Rect(0, 0, 200, 100), "scroll target");
				ScrollView scrollView = new ScrollView("wrapper", target, false, true);
				window.AddChild(scrollView);

				bool threw = false;
				try {
					scrollView.Dispose();
				} catch (InvalidOperationException) {
					threw = true;
				}
				Assert.IsTrue(threw,
					"Dispose() on a still-attached ScrollView should throw InvalidOperationException, same OWNERSHIP rule as every other ViewBase");
				// Leave attached -- window's own Dispose() cleans it up.
			}
		}
	}

	[Test]
	public void RemoveChildThenDisposeCascadesDestroyToTarget()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 240, 160), "ScrollViewTests probe window")) {
				ProbeView target = new ProbeView(new Rect(0, 0, 200, 100), "scroll target");
				ProbeScrollView scrollView = new ProbeScrollView("wrapper", target, false, true);
				window.AddChild(scrollView);

				bool removed = window.RemoveChild(scrollView);
				Assert.IsTrue(removed, "RemoveChild should return true for a scroll view that actually was a direct child");

				// scrollView is now detached from window, but target is
				// still attached to scrollView -- disposing scrollView
				// must cascade a real ~BView() delete into target, firing
				// target's own destroyed callback, with no separate
				// RemoveChild()/Dispose() call on target at all. This is
				// the direct proof that hs_scroll_view_create()'s native
				// reparenting is real, not just recorded on the managed side.
				scrollView.Dispose();

				Assert.IsTrue(scrollView.DestroyedFired, "Dispose() on a detached ScrollView should fire its own OnDestroyed");
				Assert.IsTrue(target.DestroyedFired,
					"Disposing a ScrollView should cascade-destroy its still-attached target and fire the target's own OnDestroyed, proving the native reparenting from construction was real");
			}
		}
	}

	[Test]
	public void WindowDisposeCascadesToScrollViewAndItsTarget()
	{
		using (new Application(AppSignature)) {
			ProbeView target = new ProbeView(new Rect(0, 0, 200, 100), "scroll target");
			ProbeScrollView scrollView = new ProbeScrollView("wrapper", target, false, true);

			using (Window window = new Window(new Rect(0, 0, 240, 160), "ScrollViewTests probe window")) {
				window.AddChild(scrollView);
				// window.Dispose() cascades through scrollView and into
				// target here, never shown, no explicit RemoveChild()/
				// Dispose() on either.
			}

			Assert.IsTrue(scrollView.DestroyedFired,
				"OnDestroyed should fire on a still-attached child ScrollView when its parent window is destroyed");
			Assert.IsTrue(target.DestroyedFired,
				"OnDestroyed should fire on a ScrollView's still-attached target too, two levels down from the destroyed window");
		}
	}
}
