using System;
using Haiku.App;
using Haiku.Interface;
using Haiku.Testing;

/// <summary>
/// Regression coverage for Haiku.Interface.CheckBox (see
/// managed/Haiku.Interface/CheckBox.cs and native/include/
/// hs_checkbox.h) -- this binding's third BControl-derived widget,
/// re-exercising the shared Control/ViewBase surface Button/TextControl
/// already proved out, plus the IsChecked bool convenience property.
///
/// THERE IS NO AUTOMATED TEST HERE THAT ACTUALLY FIRES OnClick. Same
/// reasoning as ButtonTests.cs's own remarks: there is no supported way
/// to synthesize a real click from inside the same process. What IS
/// automatable, and covered below, is everything that doesn't depend on
/// a real click arriving: construction/geometry, Label/IsEnabled
/// (re-verified against this third concrete control type), Value/
/// IsChecked round-tripping, and the same ownership/parenting rules
/// ButtonTests.cs/TextControlTests.cs already cover.
///
/// EVERY TEST HERE OPENS AN Application FIRST, DESPITE hs_checkbox.h's
/// OWN "NO BApplication NEEDED" FINDING -- A REAL, SUITE-ORDERING-
/// SPECIFIC SURPRISE FOUND WHILE WIRING UP THIS FILE. A standalone
/// native probe (see hs_checkbox.h) proved a BCheckBox constructs and
/// deletes cleanly with NO BApplication ever having existed anywhere in
/// the process. But Tests.exe runs every module in one shared process,
/// and running the full suite -- ButtonTests first (which constructs and
/// disposes several never-Run() Applications of its own), then
/// CheckBoxTests -- reproducibly HUNG on the very first, construction-
/// only CheckBox test, even though that exact same test passes instantly
/// when CheckBoxTests is run alone (`mono Tests.exe CheckBox`). So the
/// real, verified rule is narrower than hs_checkbox.h's construction-time
/// note first suggested: a BCheckBox needs no live BApplication only if
/// NONE has EVER existed in the process before -- once one has been
/// constructed and disposed, even without Run(), a later Application-less
/// BControl-family construction can hang the same way it would if
/// BApplication were required outright. Since this suite's own module
/// order can't be relied on to keep CheckBoxTests first, every test here
/// wraps in `using (new Application(AppSignature))` anyway, matching
/// TextControlTests.cs's/RadioButtonTests.cs's pattern -- safer than
/// depending on being run before anything else that touches Application.
/// </summary>
[Haiku.Testing.TestModule("BCheckBox")]
public class CheckBoxTests
{
	private const string AppSignature = "application/x-vnd.HaikuSharp-Tests-CheckBox";

	private class ProbeCheckBox : CheckBox
	{
		public volatile bool ClickFired;
		public volatile bool DestroyedFired;

		public ProbeCheckBox(Rect frame, string name, string label)
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
		// See this class's own remarks: wrapped in Application despite
		// hs_checkbox.h's own "no BApplication needed" finding, purely
		// for safety against this suite's own module run order.
		using (new Application(AppSignature)) {
			Rect frame = new Rect(10, 20, 210, 50);
			CheckBox checkBox = new CheckBox(frame, "geometry probe", "Enable feature");

			Assert.AreEqual(frame, checkBox.Frame, "Frame should read back exactly what the constructor was given");

			checkBox.MoveTo(5, 7);
			Rect afterMove = checkBox.Frame;
			Assert.AreEqual(5f, afterMove.Left, "MoveTo should update Frame.Left");
			Assert.AreEqual(7f, afterMove.Top, "MoveTo should update Frame.Top");
			Assert.AreEqual(frame.Right - frame.Left, afterMove.Right - afterMove.Left, "MoveTo should not change the checkbox's width");
			Assert.AreEqual(frame.Bottom - frame.Top, afterMove.Bottom - afterMove.Top, "MoveTo should not change the checkbox's height");

			checkBox.ResizeTo(160, 24);
			Rect afterResize = checkBox.Frame;
			Assert.AreEqual(160f, afterResize.Right - afterResize.Left, "ResizeTo should set the new width");
			Assert.AreEqual(24f, afterResize.Bottom - afterResize.Top, "ResizeTo should set the new height");

			// Never added to any parent -- Dispose() should succeed and
			// fire OnDestroyed synchronously, right here.
			checkBox.Dispose();
		}
	}

	[Test]
	public void LabelRoundTrips()
	{
		using (new Application(AppSignature)) {
			CheckBox checkBox = new CheckBox(new Rect(0, 0, 160, 20), "label probe", "Original");
			Assert.AreEqual("Original", checkBox.Label, "Label should read back what the constructor was given");

			checkBox.Label = "Changed";
			Assert.AreEqual("Changed", checkBox.Label, "Label should read back what the property setter was just given");

			checkBox.Dispose();
		}
	}

	[Test]
	public void ValueRoundTrips()
	{
		using (new Application(AppSignature)) {
			CheckBox checkBox = new CheckBox(new Rect(0, 0, 160, 20), "value probe", "Value");
			Assert.AreEqual(0, checkBox.Value, "A freshly-constructed checkbox should default to B_CONTROL_OFF (0)");

			checkBox.Value = 1;
			Assert.AreEqual(1, checkBox.Value, "Value should read back what was just set (B_CONTROL_ON)");

			checkBox.Value = 0;
			Assert.AreEqual(0, checkBox.Value, "Value should read back what was just set (B_CONTROL_OFF)");

			checkBox.Dispose();
		}
	}

	[Test]
	public void IsCheckedRoundTripsAndMirrorsValue()
	{
		using (new Application(AppSignature)) {
			CheckBox checkBox = new CheckBox(new Rect(0, 0, 160, 20), "ischecked probe", "Checked");
			Assert.IsFalse(checkBox.IsChecked, "A freshly-constructed checkbox should not be checked by default");

			checkBox.IsChecked = true;
			Assert.IsTrue(checkBox.IsChecked, "IsChecked should read back true after being set");
			Assert.AreEqual(1, checkBox.Value, "IsChecked=true should be backed by Value=B_CONTROL_ON (1)");

			checkBox.Value = 0;
			Assert.IsFalse(checkBox.IsChecked, "IsChecked should read back false when the underlying Value is set directly to B_CONTROL_OFF");

			checkBox.Dispose();
		}
	}

	[Test]
	public void IsEnabledRoundTrips()
	{
		using (new Application(AppSignature)) {
			CheckBox checkBox = new CheckBox(new Rect(0, 0, 160, 20), "enabled probe", "Enabled");

			checkBox.IsEnabled = false;
			Assert.IsFalse(checkBox.IsEnabled, "IsEnabled should read back false after being set");

			checkBox.IsEnabled = true;
			Assert.IsTrue(checkBox.IsEnabled, "IsEnabled should read back true after being set");

			checkBox.Dispose();
		}
	}

	[Test]
	public void AddChildUnderWindowSucceeds()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 200, 100), "CheckBoxTests probe window")) {
				CheckBox checkBox = new CheckBox(new Rect(10, 10, 190, 30), "probe", "Enable feature");

				window.AddChild(checkBox);
				// Leave attached -- window's Dispose() cascades into it,
				// exercised separately below.
			}
		}
	}

	[Test]
	public void AddChildUnderPlainViewSucceeds()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 200, 100), "CheckBoxTests probe window")) {
				View view = new View(new Rect(0, 0, 200, 100), "container");
				window.AddChild(view);

				CheckBox checkBox = new CheckBox(new Rect(10, 10, 190, 30), "probe", "Nested");
				view.AddChild(checkBox);
			}
		}
	}

	[Test]
	public void RemoveChildDetachesCheckBox()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 200, 100), "CheckBoxTests probe window")) {
				ProbeCheckBox checkBox = new ProbeCheckBox(new Rect(10, 10, 190, 30), "probe", "Enable feature");
				window.AddChild(checkBox);

				bool removed = window.RemoveChild(checkBox);
				Assert.IsTrue(removed, "RemoveChild should return true for a checkbox that actually was a direct child");

				checkBox.Dispose();
				Assert.IsTrue(checkBox.DestroyedFired, "Dispose() on a detached checkbox should fire OnDestroyed");
			}
		}
	}

	[Test]
	public void RemoveChildReturnsFalseForNonChild()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 200, 100), "CheckBoxTests probe window"))
			using (CheckBox checkBox = new CheckBox(new Rect(10, 10, 190, 30), "never added", "Enable feature")) {
				bool removed = window.RemoveChild(checkBox);
				Assert.IsFalse(removed, "RemoveChild should return false for a checkbox that was never added to this window");
			}
		}
	}

	[Test]
	public void DisposeWhileAttachedThrows()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 200, 100), "CheckBoxTests probe window")) {
				CheckBox checkBox = new CheckBox(new Rect(10, 10, 190, 30), "probe", "Enable feature");
				window.AddChild(checkBox);

				bool threw = false;
				try {
					checkBox.Dispose();
				} catch (InvalidOperationException) {
					threw = true;
				}

				Assert.IsTrue(threw,
					"Dispose() on a still-attached CheckBox should throw InvalidOperationException rather than risk corrupting its parent's child list");
				// Leave attached -- window's own Dispose() cleans it up.
			}
		}
	}

	[Test]
	public void WindowDisposeCascadesDestroyToAttachedCheckBox()
	{
		using (new Application(AppSignature)) {
			ProbeCheckBox checkBox = new ProbeCheckBox(new Rect(10, 10, 190, 30), "probe", "Enable feature");

			using (Window window = new Window(new Rect(0, 0, 200, 100), "CheckBoxTests probe window")) {
				window.AddChild(checkBox);
				// window.Dispose() cascades into checkBox here, never
				// shown, no explicit RemoveChild()/Dispose() on checkBox.
			}

			Assert.IsTrue(checkBox.DestroyedFired,
				"OnDestroyed should fire on a still-attached child checkbox when its parent window is destroyed");
		}
	}
}
