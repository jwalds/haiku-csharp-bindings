using System;
using Haiku.App;
using Haiku.Interface;
using Haiku.Testing;

/// <summary>
/// Regression coverage for Haiku.Interface.TextControl (see
/// managed/Haiku.Interface/TextControl.cs and native/include/
/// hs_text_control.h) -- this binding's second BControl-derived widget,
/// re-exercising the shared Control/ViewBase surface Button already
/// proved out, plus BTextControl's own Text/SetText and its two distinct
/// change events.
///
/// THERE IS NO AUTOMATED TEST HERE THAT ACTUALLY FIRES OnTextChanged OR
/// OnTextCommitted. Same reasoning as ButtonTests.cs's own remarks on
/// OnClick and ViewInputTests.cs's on the mouse/keyboard hooks: there is
/// no supported way to synthesize real keystrokes or a real focus change
/// from inside the same process without either driving actual hardware
/// or hand-constructing and posting raw BMessages (and this binding
/// doesn't even expose the BMessage/BInvoker machinery those would
/// normally go through -- see hs_text_control.h's "TWO DIFFERENT
/// 'CHANGED' EVENTS" note). What IS automatable, and covered below, is
/// everything that doesn't depend on a real input event arriving:
/// construction/geometry (inherited from ViewBase -- see
/// ConstructionAndGeometryRoundTrip's own remarks for a real, hardware-
/// verified surprise here: BTextControl's constructor silently overrides
/// whatever height the frame argument asks for), Text round-tripping
/// (including the initial-text constructor and SetText afterward),
/// Label/IsEnabled (inherited via Control, re-verified here against a
/// second concrete control type, same spirit as
/// ButtonBehaviorValuesMatchButtonH re-verifying rather than assuming),
/// and every ownership/parenting rule ButtonTests.cs already covers for
/// Button (AddChild under a Window and under a plain View, RemoveChild,
/// Dispose-while-attached throwing, cascade-on-parent-destroy). Real
/// event-firing is verified visually instead, via Sample.exe -- same
/// division of labor as Draw(), the mouse/keyboard hooks, and Button's
/// own OnClick.
///
/// UNLIKE ButtonTests.cs, EVERY TEST HERE OPENS AN Application FIRST --
/// including the pure construction/geometry ones. This is not stylistic
/// consistency; it's load-bearing. Constructing a BTextControl (and
/// therefore an HSTextControl) with no BApplication object yet
/// constructed in the process hangs indefinitely -- verified empirically
/// on real Haiku hardware (see hs_text_control.h's "CRITICAL" note) --
/// unlike BView/BButton, which both construct fine with zero
/// BApplication anywhere in the process. Every test method below
/// therefore wraps its entire body in `using (new Application(AppSignature))`,
/// even the ones that never touch a Window, matching this project's
/// now-established "never-Run() BApplication, disposed via `using`, is
/// safe to construct/dispose repeatedly" pattern (see Application.cs's
/// own "ONE-SHOT PER PROCESS" remarks for why that repeatability holds
/// even though a Run()-then-quit BApplication cannot be followed by
/// another one in the same process).
/// </summary>
[Haiku.Testing.TestModule("BTextControl")]
public class TextControlTests
{
	private const string AppSignature = "application/x-vnd.HaikuSharp-Tests-TextControl";

	private class ProbeTextControl : TextControl
	{
		public volatile bool TextChangedFired;
		public volatile bool TextCommittedFired;
		public volatile bool DestroyedFired;

		public ProbeTextControl(Rect frame, string name, string label, string text)
			: base(frame, name, label, text)
		{
		}

		protected override void OnTextChanged()
		{
			TextChangedFired = true;
		}

		protected override void OnTextCommitted()
		{
			TextCommittedFired = true;
		}

		protected override void OnDestroyed()
		{
			DestroyedFired = true;
		}
	}

	[Test]
	public void ConstructionAndGeometryRoundTrip()
	{
		using (new Application(AppSignature)) {
			// NOTE: unlike View/Button, BTextControl's constructor
			// silently overrides whatever height the frame argument
			// asks for with its own fixed, font-derived height --
			// verified empirically on real hardware (constructing with
			// requested heights of 10, 30, and 100 all came back with
			// the same actual height); the requested WIDTH is honored
			// exactly. See hs_text_control.h's "CONSTRUCTION SILENTLY
			// OVERRIDES THE REQUESTED HEIGHT" note for the full probe.
			// So this test checks Left/Top/width against what was given,
			// and only pins down the actual height as "whatever the
			// control reports right after construction", then uses that
			// as the known-good baseline for the MoveTo check below --
			// it does NOT assert that baseline equals frame.Bottom - frame.Top.
			Rect frame = new Rect(10, 20, 260, 50);
			TextControl textControl = new TextControl(frame, "geometry probe", "Name:", "initial");

			Rect afterConstruct = textControl.Frame;
			Assert.AreEqual(frame.Left, afterConstruct.Left, "Frame.Left should read back exactly what the constructor was given");
			Assert.AreEqual(frame.Top, afterConstruct.Top, "Frame.Top should read back exactly what the constructor was given");
			Assert.AreEqual(frame.Right - frame.Left, afterConstruct.Right - afterConstruct.Left, "Frame width should read back exactly what the constructor was given");
			float actualConstructedHeight = afterConstruct.Bottom - afterConstruct.Top;

			textControl.MoveTo(5, 7);
			Rect afterMove = textControl.Frame;
			Assert.AreEqual(5f, afterMove.Left, "MoveTo should update Frame.Left");
			Assert.AreEqual(7f, afterMove.Top, "MoveTo should update Frame.Top");
			Assert.AreEqual(frame.Right - frame.Left, afterMove.Right - afterMove.Left, "MoveTo should not change the control's width");
			Assert.AreEqual(actualConstructedHeight, afterMove.Bottom - afterMove.Top, "MoveTo should not change the control's height");

			// Unlike construction, a plain ResizeTo() is NOT overridden --
			// verified by the same probe referenced above -- so this
			// DOES assert the exact height requested.
			textControl.ResizeTo(150, 25);
			Rect afterResize = textControl.Frame;
			Assert.AreEqual(5f, afterResize.Left, "ResizeTo should not change Frame.Left");
			Assert.AreEqual(7f, afterResize.Top, "ResizeTo should not change Frame.Top");
			Assert.AreEqual(150f, afterResize.Right - afterResize.Left, "ResizeTo should set the new width");
			Assert.AreEqual(25f, afterResize.Bottom - afterResize.Top, "ResizeTo should set the new height exactly, unlike construction");

			// Never added to any parent -- Dispose() should succeed and
			// fire OnDestroyed synchronously, right here.
			textControl.Dispose();
		}
	}

	[Test]
	public void TextRoundTripsFromConstructor()
	{
		using (new Application(AppSignature)) {
			TextControl textControl = new TextControl(new Rect(0, 0, 200, 20), "text probe", "Name:", "hello");
			Assert.AreEqual("hello", textControl.Text, "Text should read back the constructor's initial text argument");

			textControl.Dispose();
		}
	}

	[Test]
	public void TextRoundTripsViaSetText()
	{
		using (new Application(AppSignature)) {
			TextControl textControl = new TextControl(new Rect(0, 0, 200, 20), "text probe", "Name:", "original");

			textControl.Text = "changed";
			Assert.AreEqual("changed", textControl.Text, "Text should read back what the property setter (SetText) was just given");

			textControl.Text = "";
			Assert.AreEqual("", textControl.Text, "Text should read back an empty string after being cleared");

			textControl.Dispose();
		}
	}

	[Test]
	public void LabelRoundTrips()
	{
		using (new Application(AppSignature)) {
			// Re-verifies Control's shared Label property against a
			// second concrete control type, not just Button -- exercises
			// hs_control_label()/hs_control_set_label() cast against a
			// real HSTextControl handle for the first time.
			TextControl textControl = new TextControl(new Rect(0, 0, 200, 20), "label probe", "Original:", "");
			Assert.AreEqual("Original:", textControl.Label, "Label should read back what the constructor was given");

			textControl.Label = "Changed:";
			Assert.AreEqual("Changed:", textControl.Label, "Label should read back what the property setter was just given");

			textControl.Dispose();
		}
	}

	[Test]
	public void IsEnabledRoundTrips()
	{
		using (new Application(AppSignature)) {
			// Same re-verification purpose as LabelRoundTrips above, for
			// hs_control_is_enabled()/hs_control_set_enabled().
			TextControl textControl = new TextControl(new Rect(0, 0, 200, 20), "enabled probe", "Name:", "");

			textControl.IsEnabled = false;
			Assert.IsFalse(textControl.IsEnabled, "IsEnabled should read back false after SetEnabled(false)");

			textControl.IsEnabled = true;
			Assert.IsTrue(textControl.IsEnabled, "IsEnabled should read back true after SetEnabled(true)");

			textControl.Dispose();
		}
	}

	[Test]
	public void AddChildUnderWindowSucceeds()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 300, 100), "TextControlTests probe window")) {
				TextControl textControl = new TextControl(new Rect(10, 10, 280, 35), "probe", "Name:", "");

				// Should not throw -- exercises hs_window_add_child()
				// against a real HSTextControl handle for the first time.
				window.AddChild(textControl);

				// Leave textControl attached -- window's own Dispose()
				// below cascades into it, exercised separately by
				// WindowDisposeCascadesDestroyToAttachedTextControl.
			}
		}
	}

	[Test]
	public void AddChildUnderPlainViewSucceeds()
	{
		// Same key regression AddChildUnderPlainViewSucceeds covers in
		// ButtonTests.cs, now against HSTextControl -- exercises
		// hs_view_add_child()'s BView*-cast reuse for a second concrete
		// control type.
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 300, 100), "TextControlTests probe window")) {
				View view = new View(new Rect(0, 0, 300, 100), "container");
				window.AddChild(view);

				TextControl textControl = new TextControl(new Rect(10, 10, 280, 35), "probe", "Name:", "");
				view.AddChild(textControl);

				// Leave textControl attached -- the window's cascading
				// Dispose() below tears down view, which tears down
				// textControl in turn.
			}
		}
	}

	[Test]
	public void RemoveChildDetachesTextControl()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 300, 100), "TextControlTests probe window")) {
				ProbeTextControl textControl = new ProbeTextControl(new Rect(10, 10, 280, 35), "probe", "Name:", "");
				window.AddChild(textControl);

				bool removed = window.RemoveChild(textControl);

				Assert.IsTrue(removed, "RemoveChild should return true for a text control that actually was a direct child");

				// No longer attached -- Dispose() should now succeed
				// rather than throw (see DisposeWhileAttachedThrows for
				// the still-attached case).
				textControl.Dispose();
				Assert.IsTrue(textControl.DestroyedFired, "Dispose() on a detached text control should fire OnDestroyed");
			}
		}
	}

	[Test]
	public void RemoveChildReturnsFalseForNonChild()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 300, 100), "TextControlTests probe window"))
			using (TextControl textControl = new TextControl(new Rect(10, 10, 280, 35), "never added", "Name:", "")) {
				bool removed = window.RemoveChild(textControl);

				Assert.IsFalse(removed, "RemoveChild should return false for a text control that was never added to this window");
			}
		}
	}

	[Test]
	public void DisposeWhileAttachedThrows()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 300, 100), "TextControlTests probe window")) {
				TextControl textControl = new TextControl(new Rect(10, 10, 280, 35), "probe", "Name:", "");
				window.AddChild(textControl);

				bool threw = false;
				try {
					textControl.Dispose();
				} catch (InvalidOperationException) {
					threw = true;
				}

				Assert.IsTrue(threw,
					"Dispose() on a still-attached TextControl should throw InvalidOperationException rather than risk corrupting its parent's child list");

				// Leave it attached -- window's own Dispose() below cleans
				// it up via the implicit cascade (see hs_view.h's
				// OWNERSHIP note, which ViewBase's Dispose() doc points to).
			}
		}
	}

	[Test]
	public void WindowDisposeCascadesDestroyToAttachedTextControl()
	{
		using (new Application(AppSignature)) {
			ProbeTextControl textControl = new ProbeTextControl(new Rect(10, 10, 280, 35), "probe", "Name:", "");

			using (Window window = new Window(new Rect(0, 0, 300, 100), "TextControlTests probe window")) {
				window.AddChild(textControl);
				// window.Dispose() fires here, at the end of this `using`
				// block -- never shown, so it deletes the native BWindow
				// synchronously, which recursively deletes its still-
				// attached children, including this text control, with
				// no explicit RemoveChild() or Dispose() call on it at
				// all. Exercises hs_window_destroy()'s cascade against a
				// real HSTextControl child for the first time --
				// HSTextControl's own destructor (see
				// hs_text_control.cpp) fires its destroyed callback
				// unconditionally, same as HSButton's/HSView's/
				// HSWindow's.
			}

			Assert.IsTrue(textControl.DestroyedFired,
				"OnDestroyed should fire on a still-attached child text control when its parent window is destroyed, even though nothing disposed it directly");
		}
	}
}
