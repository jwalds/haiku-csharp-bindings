using System;
using Haiku.App;
using Haiku.Interface;
using Haiku.Testing;

/// <summary>
/// Regression coverage for Haiku.Interface.RadioButton (see
/// managed/Haiku.Interface/RadioButton.cs and native/include/
/// hs_radio_button.h) -- this binding's fourth BControl-derived widget,
/// re-exercising the shared Control/ViewBase surface CheckBox/Button/
/// TextControl already proved out, plus real BeAPI's own automatic
/// mutual-exclusivity grouping.
///
/// THERE IS NO AUTOMATED TEST HERE THAT ACTUALLY FIRES OnClick, same
/// reasoning as ButtonTests.cs's/CheckBoxTests.cs's own remarks. The
/// automatic-grouping behavior IS automatable, though, and covered
/// below (see AutomaticGroupingTurnsOffSiblings) -- it doesn't need a
/// real click, since it can be driven entirely through Value/IsChecked
/// (SetValue), and BRadioButton's grouping logic runs off that directly.
///
/// UNLIKE CheckBoxTests.cs BUT LIKE TextControlTests.cs, EVERY TEST HERE
/// OPENS AN Application FIRST -- including the pure construction/
/// geometry ones. This is not stylistic consistency; it's load-bearing.
/// Constructing a BRadioButton (and therefore an HSRadioButton) with no
/// BApplication object yet constructed in the process hangs indefinitely
/// -- verified empirically on real Haiku hardware, with TWO separate
/// native probes: the first alongside a same-process BCheckBox
/// construction (which itself completed fine), the second in complete
/// isolation (constructing ONLY a BRadioButton, nothing else, in a fresh
/// process) to rule out any ordering artifact from the first probe. Both
/// hung at the exact same point, immediately on entering the
/// constructor. This is a genuine, unexplained asymmetry with CheckBox
/// -- see hs_radio_button.h's own note for the full story -- not
/// something this binding can paper over, so every test method below
/// wraps its entire body in `using (new Application(AppSignature))`,
/// matching TextControlTests.cs's own established pattern.
///
/// THE GROUPING TEST DELIBERATELY USES AN UNSHOWN PARENT VIEW, NOT A
/// SHOWN WINDOW. Also verified on real hardware: driving grouping via
/// Value/SetValue against radio buttons parented to a plain View that is
/// never added to a Window (let alone Show()n) works correctly and
/// instantly. Doing the same thing against a radio button that IS a
/// child of an already-Show()n Window, from a thread other than the one
/// that would call Application.Run() (which nothing in this binding's
/// test suite does), was separately found to hang -- the same underlying
/// hazard KNOWN_ISSUES.md issue #4 documents for Draw(), now confirmed to
/// extend to at least this one more operation. See hs_radio_button.h's
/// own grouping note for the full writeup. This is exactly why the test
/// below never calls Window.Show() -- matching every other test file in
/// this suite's own established "never Show() a test window" rule.
/// </summary>
[Haiku.Testing.TestModule("BRadioButton")]
public class RadioButtonTests
{
	private const string AppSignature = "application/x-vnd.HaikuSharp-Tests-RadioButton";

	private class ProbeRadioButton : RadioButton
	{
		public volatile bool ClickFired;
		public volatile bool DestroyedFired;

		public ProbeRadioButton(Rect frame, string name, string label)
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
		using (new Application(AppSignature)) {
			Rect frame = new Rect(10, 20, 210, 50);
			RadioButton radioButton = new RadioButton(frame, "geometry probe", "Option A");

			Assert.AreEqual(frame, radioButton.Frame, "Frame should read back exactly what the constructor was given");

			radioButton.MoveTo(5, 7);
			Rect afterMove = radioButton.Frame;
			Assert.AreEqual(5f, afterMove.Left, "MoveTo should update Frame.Left");
			Assert.AreEqual(7f, afterMove.Top, "MoveTo should update Frame.Top");

			radioButton.ResizeTo(160, 24);
			Rect afterResize = radioButton.Frame;
			Assert.AreEqual(160f, afterResize.Right - afterResize.Left, "ResizeTo should set the new width");
			Assert.AreEqual(24f, afterResize.Bottom - afterResize.Top, "ResizeTo should set the new height");

			radioButton.Dispose();
		}
	}

	[Test]
	public void LabelRoundTrips()
	{
		using (new Application(AppSignature)) {
			RadioButton radioButton = new RadioButton(new Rect(0, 0, 160, 20), "label probe", "Original");
			Assert.AreEqual("Original", radioButton.Label, "Label should read back what the constructor was given");

			radioButton.Label = "Changed";
			Assert.AreEqual("Changed", radioButton.Label, "Label should read back what the property setter was just given");

			radioButton.Dispose();
		}
	}

	[Test]
	public void ValueRoundTrips()
	{
		using (new Application(AppSignature)) {
			RadioButton radioButton = new RadioButton(new Rect(0, 0, 160, 20), "value probe", "Value");
			Assert.AreEqual(0, radioButton.Value, "A freshly-constructed, ungrouped radio button should default to B_CONTROL_OFF (0)");

			radioButton.Value = 1;
			Assert.AreEqual(1, radioButton.Value, "Value should read back what was just set (B_CONTROL_ON)");

			radioButton.Dispose();
		}
	}

	[Test]
	public void IsCheckedRoundTripsAndMirrorsValue()
	{
		using (new Application(AppSignature)) {
			RadioButton radioButton = new RadioButton(new Rect(0, 0, 160, 20), "ischecked probe", "Option A");
			Assert.IsFalse(radioButton.IsChecked, "A freshly-constructed, ungrouped radio button should not be checked by default");

			radioButton.IsChecked = true;
			Assert.IsTrue(radioButton.IsChecked, "IsChecked should read back true after being set");
			Assert.AreEqual(1, radioButton.Value, "IsChecked=true should be backed by Value=B_CONTROL_ON (1)");

			radioButton.Dispose();
		}
	}

	[Test]
	public void IsEnabledRoundTrips()
	{
		using (new Application(AppSignature)) {
			RadioButton radioButton = new RadioButton(new Rect(0, 0, 160, 20), "enabled probe", "Option A");

			radioButton.IsEnabled = false;
			Assert.IsFalse(radioButton.IsEnabled, "IsEnabled should read back false after being set");

			radioButton.IsEnabled = true;
			Assert.IsTrue(radioButton.IsEnabled, "IsEnabled should read back true after being set");

			radioButton.Dispose();
		}
	}

	[Test]
	public void AutomaticGroupingTurnsOffSiblings()
	{
		// Verified on real hardware: three RadioButtons under one shared,
		// UNSHOWN parent View -- see this class's own remarks for why the
		// parent is deliberately never added to a Window/Show()n.
		using (new Application(AppSignature)) {
			View group = new View(new Rect(0, 0, 200, 200), "group");

			RadioButton one = new RadioButton(new Rect(10, 10, 190, 30), "one", "One");
			RadioButton two = new RadioButton(new Rect(10, 40, 190, 60), "two", "Two");
			RadioButton three = new RadioButton(new Rect(10, 70, 190, 90), "three", "Three");

			group.AddChild(one);
			group.AddChild(two);
			group.AddChild(three);

			one.IsChecked = true;
			Assert.IsTrue(one.IsChecked, "Setting IsChecked on 'one' should turn it on");
			Assert.IsFalse(two.IsChecked, "'two' should remain off after only 'one' was turned on");
			Assert.IsFalse(three.IsChecked, "'three' should remain off after only 'one' was turned on");

			two.IsChecked = true;
			Assert.IsFalse(one.IsChecked, "Turning 'two' on should automatically turn 'one' off (real BeAPI grouping, not code in this binding)");
			Assert.IsTrue(two.IsChecked, "'two' should read back checked after being set");
			Assert.IsFalse(three.IsChecked, "'three' should remain off after 'two' was turned on");

			three.IsChecked = true;
			Assert.IsFalse(one.IsChecked, "'one' should remain off after 'three' was turned on");
			Assert.IsFalse(two.IsChecked, "Turning 'three' on should automatically turn 'two' off");
			Assert.IsTrue(three.IsChecked, "'three' should read back checked after being set");

			// group.Dispose() is never called explicitly (matching
			// ViewTests'/ButtonTests' pattern of leaving a container
			// unattached-to-window at the end of a test) -- there is no
			// parent to cascade through here since group itself was
			// never added to a Window, so nothing leaks across test
			// methods: each Application is fresh and disposed at the end
			// of this using block, taking the whole app_server connection
			// (and therefore these native objects) down with it.
		}
	}

	[Test]
	public void AddChildUnderWindowSucceeds()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 200, 100), "RadioButtonTests probe window")) {
				RadioButton radioButton = new RadioButton(new Rect(10, 10, 190, 30), "probe", "Option A");

				window.AddChild(radioButton);
				// Leave attached -- window's Dispose() cascades into it.
			}
		}
	}

	[Test]
	public void AddChildUnderPlainViewSucceeds()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 200, 100), "RadioButtonTests probe window")) {
				View view = new View(new Rect(0, 0, 200, 100), "container");
				window.AddChild(view);

				RadioButton radioButton = new RadioButton(new Rect(10, 10, 190, 30), "probe", "Nested");
				view.AddChild(radioButton);
			}
		}
	}

	[Test]
	public void RemoveChildDetachesRadioButton()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 200, 100), "RadioButtonTests probe window")) {
				ProbeRadioButton radioButton = new ProbeRadioButton(new Rect(10, 10, 190, 30), "probe", "Option A");
				window.AddChild(radioButton);

				bool removed = window.RemoveChild(radioButton);
				Assert.IsTrue(removed, "RemoveChild should return true for a radio button that actually was a direct child");

				radioButton.Dispose();
				Assert.IsTrue(radioButton.DestroyedFired, "Dispose() on a detached radio button should fire OnDestroyed");
			}
		}
	}

	[Test]
	public void RemoveChildReturnsFalseForNonChild()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 200, 100), "RadioButtonTests probe window"))
			using (RadioButton radioButton = new RadioButton(new Rect(10, 10, 190, 30), "never added", "Option A")) {
				bool removed = window.RemoveChild(radioButton);
				Assert.IsFalse(removed, "RemoveChild should return false for a radio button that was never added to this window");
			}
		}
	}

	[Test]
	public void DisposeWhileAttachedThrows()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 200, 100), "RadioButtonTests probe window")) {
				RadioButton radioButton = new RadioButton(new Rect(10, 10, 190, 30), "probe", "Option A");
				window.AddChild(radioButton);

				bool threw = false;
				try {
					radioButton.Dispose();
				} catch (InvalidOperationException) {
					threw = true;
				}

				Assert.IsTrue(threw,
					"Dispose() on a still-attached RadioButton should throw InvalidOperationException rather than risk corrupting its parent's child list");
			}
		}
	}

	[Test]
	public void WindowDisposeCascadesDestroyToAttachedRadioButton()
	{
		using (new Application(AppSignature)) {
			ProbeRadioButton radioButton = new ProbeRadioButton(new Rect(10, 10, 190, 30), "probe", "Option A");

			using (Window window = new Window(new Rect(0, 0, 200, 100), "RadioButtonTests probe window")) {
				window.AddChild(radioButton);
			}

			Assert.IsTrue(radioButton.DestroyedFired,
				"OnDestroyed should fire on a still-attached child radio button when its parent window is destroyed");
		}
	}
}
