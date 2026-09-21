using System;
using Haiku.App;
using Haiku.Interface;
using Haiku.Testing;

/// <summary>
/// Regression coverage for the mouse/keyboard "basic hooks" input slice on
/// top of View (see managed/Haiku.Interface/View.cs's OnMouseDown/OnMouseUp/
/// OnMouseMoved/OnKeyDown/OnKeyUp/MakeFocus/IsFocus/Invalidate, and
/// native/include/hs_view.h's MOUSE AND KEYBOARD INPUT note for the
/// verified BeAPI semantics behind them).
///
/// THERE IS NO AUTOMATED TEST HERE THAT ACTUALLY FIRES OnMouseDown/OnMouseUp/
/// OnMouseMoved/OnKeyDown/OnKeyUp. Those hooks only fire in response to
/// real input events dispatched by app_server through a shown window's
/// message loop -- there is no supported way to synthesize one from inside
/// the same process without either driving a real input device or
/// constructing raw BMessages and posting them by hand (both out of scope
/// for this slice, and the latter is exactly the kind of "poll a flag from
/// a thread that never called Application.Run()" shape that hung Tests.exe
/// after Draw()'s first fire -- see KNOWN_ISSUES.md issue #4, and
/// ViewTests.cs's own class remarks for why that pattern is avoided here
/// too). What IS automatable, and covered below, is everything that
/// doesn't depend on an actual input event arriving: MakeFocus/IsFocus
/// (plain getter/setter state, no event needed), Invalidate() not
/// throwing, and the new MouseButtons/MouseTransit/KeyBytes types having
/// the exact verified values/shape hs_view.h documents. Real hook-firing
/// is verified visually instead, via Sample.exe -- same division of labor
/// as Draw() itself.
///
/// The same applies to the ModifierKeys enum and the standalone
/// Modifiers.Current accessor added alongside OnMouseDown/OnMouseUp/
/// OnKeyDown/OnKeyUp's new "modifiers" parameter (see hs_view.h's
/// MODIFIERS note and Modifiers.cs): there is no automated way to hold a
/// real Shift/Ctrl/Option/Command key down while a test runs, so what's
/// covered below is the enum's exact verified bit values (same style as
/// the MouseButtons/MouseTransit/KeyBytes checks above) and
/// Modifiers.Current not throwing when called outside any input hook.
/// Modifiers.Current needs no BApplication/BWindow at all -- hs_modifiers()
/// wraps Haiku's standalone modifiers() function, documented safe to call
/// from any thread with no locking -- so, unlike this file's other tests,
/// that one test does not open a using (new Application(...)) block.
///
/// Follows ViewTests'/WindowTests' now-established pattern of opening (and
/// disposing, via `using`) a fresh, never-Run() BApplication per test
/// method, and never calling Show() on the window (per hs_window.h,
/// AddChild's AttachedToWindow fires synchronously regardless).
/// </summary>
[Haiku.Testing.TestModule("BView Input")]
public class ViewInputTests
{
	private const string AppSignature = "application/x-vnd.HaikuSharp-Tests-ViewInput";

	[Test]
	public void IsFocusDefaultsFalseBeforeMakeFocusIsCalled()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 100, 100), "ViewInputTests probe window")) {
				View view = new View(new Rect(0, 0, 50, 50), "probe");
				window.AddChild(view);

				Assert.IsFalse(view.IsFocus, "A freshly-attached view should not have keyboard focus until MakeFocus(true) is called");
			}
		}
	}

	[Test]
	public void FocusRoundTripsWhileAttachedToUnshownWindow()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 100, 100), "ViewInputTests probe window")) {
				View view = new View(new Rect(0, 0, 50, 50), "probe");
				window.AddChild(view);

				view.MakeFocus(true);
				Assert.IsTrue(view.IsFocus,
					"MakeFocus(true) should take effect even though this window has never been shown -- "
					+ "verified against View.h: MakeFocus works regardless of view flags/B_NAVIGABLE, "
					+ "which only affects Tab-key auto-cycling, not direct calls");

				view.MakeFocus(false);
				Assert.IsFalse(view.IsFocus, "MakeFocus(false) should clear focus");
			}
		}
	}

	[Test]
	public void MakeFocusDefaultParameterMeansTrue()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 100, 100), "ViewInputTests probe window")) {
				View view = new View(new Rect(0, 0, 50, 50), "probe");
				window.AddChild(view);

				view.MakeFocus();

				Assert.IsTrue(view.IsFocus, "MakeFocus() with no argument should default to focus = true, same as upstream BView::MakeFocus");
			}
		}
	}

	[Test]
	public void InvalidateDoesNotThrowOnAttachedUnshownView()
	{
		// Invalidate() just asks app_server to schedule a redraw -- it
		// does not wait for Draw() to actually fire, so this does not
		// carry the same hang risk as polling for a DrawFired flag (see
		// KNOWN_ISSUES.md issue #4). This only checks that the call
		// completes; it does not (and cannot, safely) assert that Draw()
		// is eventually re-invoked.
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 100, 100), "ViewInputTests probe window")) {
				View view = new View(new Rect(0, 0, 50, 50), "probe");
				window.AddChild(view);

				view.Invalidate();
			}
		}
	}

	[Test]
	public void MouseButtonsAreIndependentBitsThatCombine()
	{
		// Pure managed-side sanity check, no native calls -- verifies the
		// [Flags] values themselves match View.h's B_MOUSE_BUTTON(n) macro
		// (see MouseButtons.cs), and that combining them behaves like
		// independent bits rather than colliding.
		MouseButtons both = MouseButtons.Primary | MouseButtons.Secondary;

		Assert.AreEqual((uint)0x1, (uint)MouseButtons.Primary, "Primary should be B_PRIMARY_MOUSE_BUTTON (0x1)");
		Assert.AreEqual((uint)0x2, (uint)MouseButtons.Secondary, "Secondary should be B_SECONDARY_MOUSE_BUTTON (0x2)");
		Assert.AreEqual((uint)0x4, (uint)MouseButtons.Tertiary, "Tertiary should be B_TERTIARY_MOUSE_BUTTON (0x4)");
		Assert.IsTrue((both & MouseButtons.Primary) == MouseButtons.Primary, "Combining with | should keep Primary set");
		Assert.IsTrue((both & MouseButtons.Secondary) == MouseButtons.Secondary, "Combining with | should keep Secondary set");
		Assert.IsTrue((both & MouseButtons.Tertiary) == 0, "Combining Primary|Secondary should not set Tertiary");
	}

	[Test]
	public void MouseTransitValuesMatchViewH()
	{
		// Pure managed-side sanity check -- these are sequential/mutually
		// exclusive (not [Flags]), see MouseTransit.cs's remarks.
		Assert.AreEqual(0, (int)MouseTransit.Entered, "Entered should be B_ENTERED_VIEW (0)");
		Assert.AreEqual(1, (int)MouseTransit.Inside, "Inside should be B_INSIDE_VIEW (1)");
		Assert.AreEqual(2, (int)MouseTransit.Exited, "Exited should be B_EXITED_VIEW (2)");
		Assert.AreEqual(3, (int)MouseTransit.Outside, "Outside should be B_OUTSIDE_VIEW (3)");
	}

	[Test]
	public void KeyBytesMatchVerifiedInterfaceDefsConstants()
	{
		// Pure managed-side sanity check against the exact byte values
		// copied from InterfaceDefs.h into KeyBytes.cs -- catches a typo'd
		// hex literal without needing any native call or real key press.
		Assert.AreEqual((byte)0x01, KeyBytes.Home, "Home");
		Assert.AreEqual((byte)0x04, KeyBytes.End, "End");
		Assert.AreEqual((byte)0x05, KeyBytes.Insert, "Insert");
		Assert.AreEqual((byte)0x08, KeyBytes.Backspace, "Backspace");
		Assert.AreEqual((byte)0x09, KeyBytes.Tab, "Tab");
		Assert.AreEqual((byte)0x0a, KeyBytes.Return, "Return");
		Assert.AreEqual((byte)0x0a, KeyBytes.Enter, "Enter (alias of Return)");
		Assert.AreEqual((byte)0x0b, KeyBytes.PageUp, "PageUp");
		Assert.AreEqual((byte)0x0c, KeyBytes.PageDown, "PageDown");
		Assert.AreEqual((byte)0x10, KeyBytes.FunctionKey, "FunctionKey");
		Assert.AreEqual((byte)0x1b, KeyBytes.Escape, "Escape");
		Assert.AreEqual((byte)0x1c, KeyBytes.LeftArrow, "LeftArrow");
		Assert.AreEqual((byte)0x1d, KeyBytes.RightArrow, "RightArrow");
		Assert.AreEqual((byte)0x1e, KeyBytes.UpArrow, "UpArrow");
		Assert.AreEqual((byte)0x1f, KeyBytes.DownArrow, "DownArrow");
		Assert.AreEqual((byte)0x20, KeyBytes.Space, "Space");
		Assert.AreEqual((byte)0x7f, KeyBytes.Delete, "Delete");
	}

	[Test]
	public void ModifierKeysAreIndependentBitsThatMatchInterfaceDefsH()
	{
		// Pure managed-side sanity check against the exact hex literals
		// copied from InterfaceDefs.h into Modifiers.cs -- catches a
		// typo'd bit without needing any native call or a real key held
		// down. Covers both the "either side" convenience bits and the
		// side-specific ones, plus the lock keys and NoCommandKey.
		Assert.AreEqual((uint)0x00000000, (uint)ModifierKeys.None, "None");
		Assert.AreEqual((uint)0x00000001, (uint)ModifierKeys.Shift, "Shift");
		Assert.AreEqual((uint)0x00000002, (uint)ModifierKeys.Command, "Command");
		Assert.AreEqual((uint)0x00000004, (uint)ModifierKeys.Control, "Control");
		Assert.AreEqual((uint)0x00000008, (uint)ModifierKeys.CapsLock, "CapsLock");
		Assert.AreEqual((uint)0x00000010, (uint)ModifierKeys.ScrollLock, "ScrollLock");
		Assert.AreEqual((uint)0x00000020, (uint)ModifierKeys.NumLock, "NumLock");
		Assert.AreEqual((uint)0x00000040, (uint)ModifierKeys.Option, "Option");
		Assert.AreEqual((uint)0x00000080, (uint)ModifierKeys.Menu, "Menu");
		Assert.AreEqual((uint)0x00000100, (uint)ModifierKeys.LeftShift, "LeftShift");
		Assert.AreEqual((uint)0x00000200, (uint)ModifierKeys.RightShift, "RightShift");
		Assert.AreEqual((uint)0x00000400, (uint)ModifierKeys.LeftCommand, "LeftCommand");
		Assert.AreEqual((uint)0x00000800, (uint)ModifierKeys.RightCommand, "RightCommand");
		Assert.AreEqual((uint)0x00001000, (uint)ModifierKeys.LeftControl, "LeftControl");
		Assert.AreEqual((uint)0x00002000, (uint)ModifierKeys.RightControl, "RightControl");
		Assert.AreEqual((uint)0x00004000, (uint)ModifierKeys.LeftOption, "LeftOption");
		Assert.AreEqual((uint)0x00008000, (uint)ModifierKeys.RightOption, "RightOption");
		Assert.AreEqual((uint)0x00010000, (uint)ModifierKeys.NoCommandKey, "NoCommandKey");

		// And that they combine as independent bits, same shape as the
		// MouseButtonsAreIndependentBitsThatCombine check above.
		ModifierKeys shiftAndControl = ModifierKeys.Shift | ModifierKeys.Control;
		Assert.IsTrue((shiftAndControl & ModifierKeys.Shift) == ModifierKeys.Shift,
			"Shift|Control should still test true for Shift");
		Assert.IsTrue((shiftAndControl & ModifierKeys.Control) == ModifierKeys.Control,
			"Shift|Control should still test true for Control");
		Assert.IsTrue((shiftAndControl & ModifierKeys.Command) == ModifierKeys.None,
			"Shift|Control should NOT test true for Command");
	}

	[Test]
	public void ModifiersCurrentDoesNotThrow()
	{
		// Modifiers.Current wraps hs_modifiers(), which wraps Haiku's
		// standalone modifiers() -- documented safe to call from any
		// thread, with no BWindow/BApplication/locking involved at all
		// (unlike Invalidate() above, which needs an attached View). All
		// that's automatable without a real key held down is that the
		// call succeeds and returns some value; the actual bits set
		// depend on whatever the operator happens to be pressing when
		// this test runs, which is exactly why Sample.exe's DemoView
		// (not this file) is where real modifier state is verified, the
		// same division of labor as the other input hooks.
		// Following InvalidateDoesNotThrowOnAttachedUnshownView's pattern
		// above: the check IS that this completes without throwing --
		// there's nothing else automatable to assert about a value that
		// depends on whatever the operator happens to be pressing right
		// now. (An earlier draft asserted "current == current" to at
		// least touch the return value, but the compiler correctly flags
		// that as CS1718 -- almost certainly a typo -- and this project
		// builds warning-free, so the call itself is the whole test.)
		ModifierKeys current = Modifiers.Current;
		GC.KeepAlive(current);
	}
}
