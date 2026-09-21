using System;
using Haiku.App;
using Haiku.Interface;
using Haiku.Testing;

/// <summary>
/// Regression coverage for Haiku.Interface.Button/Control/ViewBase (see
/// managed/Haiku.Interface/Button.cs, Control.cs, ViewBase.cs, and
/// native/include/hs_button.h) -- the first BControl-derived widget this
/// binding wraps, and with it, the ViewBase split that lets a Control be
/// added as a child of a Window OR a plain View the exact same way a View
/// itself can (see Window.AddChild(ViewBase)/View.AddChild(ViewBase)).
///
/// THERE IS NO AUTOMATED TEST HERE THAT ACTUALLY FIRES OnClick. Same
/// reasoning as ViewInputTests.cs's own class remarks for
/// OnMouseDown/OnKeyDown/etc: there is no supported way to synthesize a
/// real click from inside the same process without either driving actual
/// hardware or hand-constructing and posting raw BMessages (and this
/// binding doesn't even expose the BMessage/BInvoker machinery a real
/// click would normally go through -- see hs_button.h's "NO BMessage/
/// BInvoker/TARGET PLUMBING" note). What IS automatable, and covered
/// below, is everything that doesn't depend on a real click arriving:
/// construction/geometry (inherited from ViewBase, reusing
/// hs_view_get_frame/move_to/resize_to against a button handle -- the
/// ABI-offset fact hs_view.cpp's hs_view_add_child() comment documents
/// and a native probe verified on real hardware), Label/Value/IsEnabled/
/// IsDefault/IsFlat/Behavior round-tripping, the ButtonBehavior enum's
/// exact values, and every ownership/parenting rule ViewTests.cs already
/// covers for View (AddChild/RemoveChild, Dispose-while-attached
/// throwing, cascade-on-parent-destroy) -- re-verified here for Button
/// specifically since it now goes through a shared ViewBase rather than
/// duplicating View's own implementation. Real click-firing is verified
/// visually instead, via Sample.exe -- same division of labor as Draw()
/// and the mouse/keyboard hooks.
///
/// Follows ViewTests'/ViewInputTests' now-established pattern of opening
/// (and disposing, via `using`) a fresh, never-Run() BApplication per test
/// method, and never calling Show() on the window (per hs_window.h,
/// AddChild's AttachedToWindow-equivalent parenting happens synchronously
/// regardless -- though note Button itself never gets an AttachedToWindow
/// callback at all, see Control.cs's own remarks for why).
/// </summary>
[Haiku.Testing.TestModule("BButton")]
public class ButtonTests
{
	private const string AppSignature = "application/x-vnd.HaikuSharp-Tests-Button";

	private class ProbeButton : Button
	{
		public volatile bool ClickFired;
		public volatile bool DestroyedFired;

		public ProbeButton(Rect frame, string name, string label)
			: base(frame, name, label)
		{
		}

		protected override void OnClick()
		{
			ClickFired = true;
		}

		protected override void OnDestroyed()
		{
			DestroyedFired = true;
		}
	}

	[Test]
	public void ConstructionAndGeometryRoundTrip()
	{
		// Deliberately no Application/Window here -- same "no app_server
		// connection needed to just exist" point ViewTests' own
		// ConstructionAndGeometryRoundTrip makes, now exercised through
		// ViewBase's shared Frame/MoveTo/ResizeTo instead of View's own.
		Rect frame = new Rect(10, 20, 210, 60);
		Button button = new Button(frame, "geometry probe", "Click Me");

		Assert.AreEqual(frame, button.Frame, "Frame should read back exactly what the constructor was given");

		button.MoveTo(5, 7);
		Rect afterMove = button.Frame;
		Assert.AreEqual(5f, afterMove.Left, "MoveTo should update Frame.Left");
		Assert.AreEqual(7f, afterMove.Top, "MoveTo should update Frame.Top");
		Assert.AreEqual(frame.Right - frame.Left, afterMove.Right - afterMove.Left, "MoveTo should not change the button's width");
		Assert.AreEqual(frame.Bottom - frame.Top, afterMove.Bottom - afterMove.Top, "MoveTo should not change the button's height");

		button.ResizeTo(120, 30);
		Rect afterResize = button.Frame;
		Assert.AreEqual(5f, afterResize.Left, "ResizeTo should not change Frame.Left");
		Assert.AreEqual(7f, afterResize.Top, "ResizeTo should not change Frame.Top");
		Assert.AreEqual(120f, afterResize.Right - afterResize.Left, "ResizeTo should set the new width");
		Assert.AreEqual(30f, afterResize.Bottom - afterResize.Top, "ResizeTo should set the new height");

		// Never added to any parent -- Dispose() should succeed and fire
		// OnDestroyed synchronously, right here.
		button.Dispose();
	}

	[Test]
	public void LabelRoundTrips()
	{
		Button button = new Button(new Rect(0, 0, 100, 20), "label probe", "Original");
		Assert.AreEqual("Original", button.Label, "Label should read back what the constructor was given");

		button.Label = "Changed";
		Assert.AreEqual("Changed", button.Label, "Label should read back what SetLabel (via the property setter) was just given");

		button.Dispose();
	}

	[Test]
	public void ValueRoundTrips()
	{
		Button button = new Button(new Rect(0, 0, 100, 20), "value probe", "Value");

		button.Value = 1;
		Assert.AreEqual(1, button.Value, "Value should read back what was just set (B_CONTROL_ON)");

		button.Value = 0;
		Assert.AreEqual(0, button.Value, "Value should read back what was just set (B_CONTROL_OFF)");

		button.Dispose();
	}

	[Test]
	public void IsEnabledRoundTrips()
	{
		Button button = new Button(new Rect(0, 0, 100, 20), "enabled probe", "Enabled");

		button.IsEnabled = false;
		Assert.IsFalse(button.IsEnabled, "IsEnabled should read back false after SetEnabled(false)");

		button.IsEnabled = true;
		Assert.IsTrue(button.IsEnabled, "IsEnabled should read back true after SetEnabled(true)");

		button.Dispose();
	}

	[Test]
	public void IsFlatRoundTrips()
	{
		Button button = new Button(new Rect(0, 0, 100, 20), "flat probe", "Flat");
		Assert.IsFalse(button.IsFlat, "A freshly-constructed button should not be flat by default");

		button.IsFlat = true;
		Assert.IsTrue(button.IsFlat, "IsFlat should read back true after being set");

		button.IsFlat = false;
		Assert.IsFalse(button.IsFlat, "IsFlat should read back false after being unset");

		button.Dispose();
	}

	[Test]
	public void IsDefaultRoundTripsViaMakeDefault()
	{
		Button button = new Button(new Rect(0, 0, 100, 20), "default probe", "Default");
		Assert.IsFalse(button.IsDefault, "A freshly-constructed button should not be the default button");

		button.MakeDefault();
		Assert.IsTrue(button.IsDefault, "IsDefault should read back true after MakeDefault()'s defaulted-true parameter");

		button.MakeDefault(false);
		Assert.IsFalse(button.IsDefault, "IsDefault should read back false after MakeDefault(false)");

		button.Dispose();
	}

	[Test]
	public void BehaviorRoundTrips()
	{
		Button button = new Button(new Rect(0, 0, 100, 20), "behavior probe", "Behavior");
		Assert.AreEqual(ButtonBehavior.PushButton, button.Behavior, "A freshly-constructed button should default to PushButton behavior");

		button.Behavior = ButtonBehavior.Toggle;
		Assert.AreEqual(ButtonBehavior.Toggle, button.Behavior, "Behavior should read back Toggle after being set");

		button.Behavior = ButtonBehavior.PopUpMenu;
		Assert.AreEqual(ButtonBehavior.PopUpMenu, button.Behavior, "Behavior should read back PopUpMenu after being set");

		button.Behavior = ButtonBehavior.PushButton;
		Assert.AreEqual(ButtonBehavior.PushButton, button.Behavior, "Behavior should read back PushButton after being set back");

		button.Dispose();
	}

	[Test]
	public void ButtonBehaviorValuesMatchButtonH()
	{
		// Pure managed-side sanity check against the exact sequential
		// values copied from Button.h's BBehavior into ButtonBehavior.cs
		// -- same style as KeyBytesMatchVerifiedInterfaceDefsConstants
		// and ModifierKeysAreIndependentBitsThatMatchInterfaceDefsH,
		// catching a typo'd value without needing any native call.
		Assert.AreEqual(0, (int)ButtonBehavior.PushButton, "PushButton should be B_BUTTON_BEHAVIOR (0)");
		Assert.AreEqual(1, (int)ButtonBehavior.Toggle, "Toggle should be B_TOGGLE_BEHAVIOR (1)");
		Assert.AreEqual(2, (int)ButtonBehavior.PopUpMenu, "PopUpMenu should be B_POP_UP_BEHAVIOR (2)");
	}

	[Test]
	public void AddChildUnderWindowSucceeds()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 200, 100), "ButtonTests probe window")) {
				Button button = new Button(new Rect(10, 10, 150, 40), "probe", "Click Me");

				// Should not throw -- exercises hs_window_add_child()
				// against a real HSButton handle for the first time.
				window.AddChild(button);

				// Leave button attached -- window's own Dispose() below
				// cascades into it, exercised separately by
				// WindowDisposeCascadesDestroyToAttachedButton.
			}
		}
	}

	[Test]
	public void AddChildUnderPlainViewSucceeds()
	{
		// The key regression for this slice's ViewBase refactor: a
		// Button nested inside a plain View (not directly a Window) --
		// exercises hs_view_add_child()'s BView*-cast fix (see
		// hs_view.cpp's comment on it) against a real HSButton handle,
		// not just an HSView one.
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 200, 100), "ButtonTests probe window")) {
				View view = new View(new Rect(0, 0, 200, 100), "container");
				window.AddChild(view);

				Button button = new Button(new Rect(10, 10, 150, 40), "probe", "Nested");
				view.AddChild(button);

				// Leave button attached -- the window's cascading Dispose()
				// below tears down view, which tears down button in turn.
			}
		}
	}

	[Test]
	public void RemoveChildDetachesButton()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 200, 100), "ButtonTests probe window")) {
				ProbeButton button = new ProbeButton(new Rect(10, 10, 150, 40), "probe", "Click Me");
				window.AddChild(button);

				bool removed = window.RemoveChild(button);

				Assert.IsTrue(removed, "RemoveChild should return true for a button that actually was a direct child");

				// No longer attached -- Dispose() should now succeed
				// rather than throw (see DisposeWhileAttachedThrows for
				// the still-attached case).
				button.Dispose();
				Assert.IsTrue(button.DestroyedFired, "Dispose() on a detached button should fire OnDestroyed");
			}
		}
	}

	[Test]
	public void RemoveChildReturnsFalseForNonChild()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 200, 100), "ButtonTests probe window"))
			using (Button button = new Button(new Rect(10, 10, 150, 40), "never added", "Click Me")) {
				bool removed = window.RemoveChild(button);

				Assert.IsFalse(removed, "RemoveChild should return false for a button that was never added to this window");
			}
		}
	}

	[Test]
	public void DisposeWhileAttachedThrows()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 200, 100), "ButtonTests probe window")) {
				Button button = new Button(new Rect(10, 10, 150, 40), "probe", "Click Me");
				window.AddChild(button);

				bool threw = false;
				try {
					button.Dispose();
				} catch (InvalidOperationException) {
					threw = true;
				}

				Assert.IsTrue(threw,
					"Dispose() on a still-attached Button should throw InvalidOperationException rather than risk corrupting its parent's child list");

				// Leave it attached -- window's own Dispose() below cleans
				// it up via the implicit cascade (see hs_view.h's
				// OWNERSHIP note, which ViewBase's Dispose() doc points to).
			}
		}
	}

	[Test]
	public void WindowDisposeCascadesDestroyToAttachedButton()
	{
		using (new Application(AppSignature)) {
			ProbeButton button = new ProbeButton(new Rect(10, 10, 150, 40), "probe", "Click Me");

			using (Window window = new Window(new Rect(0, 0, 200, 100), "ButtonTests probe window")) {
				window.AddChild(button);
				// window.Dispose() fires here, at the end of this `using`
				// block -- never shown, so it deletes the native BWindow
				// synchronously, which recursively deletes its still-
				// attached children, including this button, with no
				// explicit RemoveChild() or Dispose() call on the button
				// at all. Exercises hs_window_destroy()'s cascade against
				// a real HSButton child for the first time -- HSButton's
				// own destructor (see hs_button.cpp) fires its destroyed
				// callback unconditionally, same as HSView's/HSWindow's.
			}

			Assert.IsTrue(button.DestroyedFired,
				"OnDestroyed should fire on a still-attached child button when its parent window is destroyed, even though nothing disposed the button directly");
		}
	}
}
