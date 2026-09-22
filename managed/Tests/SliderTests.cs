using System;
using Haiku.App;
using Haiku.Interface;
using Haiku.Testing;

/// <summary>
/// Regression coverage for Haiku.Interface.Slider (see
/// managed/Haiku.Interface/Slider.cs and native/include/hs_slider.h) --
/// this binding's fifth BControl-derived widget, re-exercising the
/// shared Control/ViewBase surface every prior widget already proved
/// out, plus BSlider's own Position/Minimum-Maximum/Orientation/Style/
/// limit-label/KeyIncrementValue surface.
///
/// THERE IS NO AUTOMATED TEST HERE THAT ACTUALLY FIRES OnValueChanged OR
/// OnValueCommitted. Same reasoning as every other input hook in this
/// binding (Button's OnClick, TextControl's own two hooks, the
/// mouse/keyboard hooks on View): there is no supported way to
/// synthesize a real drag/mouse-release from inside the same process
/// without either driving actual hardware or hand-constructing and
/// posting raw BMessages, and this binding doesn't even expose the
/// BMessage/BInvoker machinery those would normally go through -- see
/// hs_slider.h's "TWO DIFFERENT 'CHANGED' EVENTS" note. What IS
/// automatable, and covered below, is everything that doesn't depend on
/// a real input event arriving. Real event-firing is verified visually
/// instead, via Sample.exe's DemoSlider -- same division of labor as
/// every prior widget's own input hooks.
///
/// EVERY TEST HERE OPENS AN Application FIRST, matching
/// TextControlTests.cs/RadioButtonTests.cs, not ButtonTests.cs/
/// CheckBoxTests.cs's original (pre-ordering-fix) pattern. This is
/// load-bearing, not stylistic: constructing a BSlider (and therefore an
/// HSSlider) with no BApplication object yet constructed in the process
/// hangs indefinitely -- hardware-verified via an isolated, flush-per-
/// step probe (see hs_slider.h's "BAPPLICATION-AT-CONSTRUCTION" note),
/// matching TextControl/RadioButton, unlike CheckBox.
///
/// The exact Position()/Value()/SetPosition()/SetValue() relationship
/// asserted below (PositionRoundTripsFromValue, ValueRoundTripsFromSetLimits)
/// was itself hardware-verified with a small isolated probe before being
/// relied on here, rather than assumed from "it's probably linear
/// mapping" -- SetPosition(0.0)/SetPosition(1.0) and SetValue() at the
/// exact midpoint of a range all round-trip through Position() with zero
/// floating-point error on real hardware, which is why this test only
/// exercises those exact values rather than an arbitrary interior
/// position that could be quantized differently.
/// </summary>
[Haiku.Testing.TestModule("BSlider")]
public class SliderTests
{
	private const string AppSignature = "application/x-vnd.HaikuSharp-Tests-Slider";

	private class ProbeSlider : Slider
	{
		public volatile bool ValueChangedFired;
		public volatile bool ValueCommittedFired;
		public volatile bool DestroyedFired;

		public ProbeSlider(Rect frame, string name, string label, int minValue, int maxValue)
			: base(frame, name, label, minValue, maxValue)
		{
		}

		protected override void OnValueChanged()
		{
			ValueChangedFired = true;
		}

		protected override void OnValueCommitted()
		{
			ValueCommittedFired = true;
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
			Rect frame = new Rect(10, 20, 260, 60);
			Slider slider = new Slider(frame, "geometry probe", "Label:", 0, 100);

			Rect afterConstruct = slider.Frame;
			Assert.AreEqual(frame.Left, afterConstruct.Left, "Frame.Left should read back exactly what the constructor was given");
			Assert.AreEqual(frame.Top, afterConstruct.Top, "Frame.Top should read back exactly what the constructor was given");
			Assert.AreEqual(frame.Right - frame.Left, afterConstruct.Right - afterConstruct.Left, "Frame width should read back exactly what the constructor was given");
			Assert.AreEqual(frame.Bottom - frame.Top, afterConstruct.Bottom - afterConstruct.Top, "Frame height should read back exactly what the constructor was given");

			slider.MoveTo(5, 7);
			Rect afterMove = slider.Frame;
			Assert.AreEqual(5f, afterMove.Left, "MoveTo should update Frame.Left");
			Assert.AreEqual(7f, afterMove.Top, "MoveTo should update Frame.Top");
			Assert.AreEqual(frame.Right - frame.Left, afterMove.Right - afterMove.Left, "MoveTo should not change the slider's width");
			Assert.AreEqual(frame.Bottom - frame.Top, afterMove.Bottom - afterMove.Top, "MoveTo should not change the slider's height");

			slider.ResizeTo(150, 30);
			Rect afterResize = slider.Frame;
			Assert.AreEqual(5f, afterResize.Left, "ResizeTo should not change Frame.Left");
			Assert.AreEqual(7f, afterResize.Top, "ResizeTo should not change Frame.Top");
			Assert.AreEqual(150f, afterResize.Right - afterResize.Left, "ResizeTo should set the new width");
			Assert.AreEqual(30f, afterResize.Bottom - afterResize.Top, "ResizeTo should set the new height");

			// Never added to any parent -- Dispose() should succeed and
			// fire OnDestroyed synchronously, right here.
			slider.Dispose();
		}
	}

	[Test]
	public void LabelRoundTrips()
	{
		using (new Application(AppSignature)) {
			// Re-verifies Control's shared Label property against a
			// fifth concrete control type -- exercises
			// hs_control_label()/hs_control_set_label() cast against a
			// real HSSlider handle for the first time.
			Slider slider = new Slider(new Rect(0, 0, 200, 40), "label probe", "Original:", 0, 100);
			Assert.AreEqual("Original:", slider.Label, "Label should read back what the constructor was given");

			slider.Label = "Changed:";
			Assert.AreEqual("Changed:", slider.Label, "Label should read back what the property setter was just given");

			slider.Dispose();
		}
	}

	[Test]
	public void IsEnabledRoundTrips()
	{
		using (new Application(AppSignature)) {
			Slider slider = new Slider(new Rect(0, 0, 200, 40), "enabled probe", "Label:", 0, 100);

			slider.IsEnabled = false;
			Assert.IsFalse(slider.IsEnabled, "IsEnabled should read back false after SetEnabled(false)");

			slider.IsEnabled = true;
			Assert.IsTrue(slider.IsEnabled, "IsEnabled should read back true after SetEnabled(true)");

			slider.Dispose();
		}
	}

	[Test]
	public void MinimumAndMaximumRoundTripFromConstructor()
	{
		using (new Application(AppSignature)) {
			Slider slider = new Slider(new Rect(0, 0, 200, 40), "limits probe", "Label:", 0, 100);
			Assert.AreEqual(0, slider.Minimum, "Minimum should read back the constructor's minValue argument");
			Assert.AreEqual(100, slider.Maximum, "Maximum should read back the constructor's maxValue argument");

			slider.Dispose();
		}
	}

	[Test]
	public void SetLimitsRoundTrips()
	{
		using (new Application(AppSignature)) {
			Slider slider = new Slider(new Rect(0, 0, 200, 40), "limits probe", "Label:", 0, 100);

			slider.SetLimits(10, 20);
			Assert.AreEqual(10, slider.Minimum, "Minimum should read back what SetLimits was just given");
			Assert.AreEqual(20, slider.Maximum, "Maximum should read back what SetLimits was just given");

			slider.Dispose();
		}
	}

	[Test]
	public void PositionRoundTripsAtExtremes()
	{
		// Only the exact values hardware-verified to round-trip through
		// Position() with zero floating-point error -- see this class's
		// own remarks above.
		using (new Application(AppSignature)) {
			Slider slider = new Slider(new Rect(0, 0, 200, 40), "position probe", "Label:", 0, 100);

			slider.Position = 0.0f;
			Assert.AreEqual(0.0f, slider.Position, "Position should read back 0.0 after being set to 0.0");
			Assert.AreEqual(0, slider.Value, "Value should be Minimum after Position is set to 0.0");

			slider.Position = 1.0f;
			Assert.AreEqual(1.0f, slider.Position, "Position should read back 1.0 after being set to 1.0");
			Assert.AreEqual(100, slider.Value, "Value should be Maximum after Position is set to 1.0");

			slider.Dispose();
		}
	}

	[Test]
	public void ValueRoundTripsFromSetLimits()
	{
		// Hardware-verified: at an exact midpoint of the configured
		// range, Position() comes back as exactly 0.5, both for the
		// default 0-100 range and after SetLimits changes it.
		using (new Application(AppSignature)) {
			Slider slider = new Slider(new Rect(0, 0, 200, 40), "value probe", "Label:", 0, 100);

			slider.Value = 50;
			Assert.AreEqual(50, slider.Value, "Value should read back what the property setter was just given");
			Assert.AreEqual(0.5f, slider.Position, "Position should be exactly 0.5 at the midpoint of a 0-100 range");

			slider.SetLimits(10, 20);
			slider.Value = 15;
			Assert.AreEqual(15, slider.Value, "Value should read back what the property setter was just given after SetLimits changed the range");
			Assert.AreEqual(0.5f, slider.Position, "Position should be exactly 0.5 at the midpoint of a 10-20 range");

			slider.Dispose();
		}
	}

	[Test]
	public void OrientationRoundTrips()
	{
		using (new Application(AppSignature)) {
			// Default overload -- convenience constructor always builds
			// a horizontal slider (see Slider.cs's own remarks).
			Slider horizontal = new Slider(new Rect(0, 0, 200, 40), "orientation probe", "Label:", 0, 100);
			Assert.AreEqual(SliderOrientation.Horizontal, horizontal.Orientation, "Default constructor overload should produce a horizontal slider");
			horizontal.Dispose();

			Slider vertical = new Slider(new Rect(0, 0, 40, 200), "orientation probe vertical", "Label:", 0, 100,
				SliderOrientation.Vertical, ThumbStyle.Block, ViewResizingMode.None,
				ViewFlags.WillDraw | ViewFlags.Navigable | ViewFlags.FrameEvents);
			Assert.AreEqual(SliderOrientation.Vertical, vertical.Orientation, "Full constructor overload should honor an explicit Vertical orientation");

			vertical.Orientation = SliderOrientation.Horizontal;
			Assert.AreEqual(SliderOrientation.Horizontal, vertical.Orientation, "Orientation should read back what the property setter was just given");

			vertical.Dispose();
		}
	}

	[Test]
	public void StyleRoundTrips()
	{
		using (new Application(AppSignature)) {
			Slider slider = new Slider(new Rect(0, 0, 200, 40), "style probe", "Label:", 0, 100);
			Assert.AreEqual(ThumbStyle.Block, slider.Style, "Default constructor overload should produce a block-thumb slider");

			slider.Style = ThumbStyle.Triangle;
			Assert.AreEqual(ThumbStyle.Triangle, slider.Style, "Style should read back what the property setter was just given");

			slider.Style = ThumbStyle.Block;
			Assert.AreEqual(ThumbStyle.Block, slider.Style, "Style should read back Block after being set back to it");

			slider.Dispose();
		}
	}

	[Test]
	public void LimitLabelsRoundTrip()
	{
		using (new Application(AppSignature)) {
			Slider slider = new Slider(new Rect(0, 0, 200, 40), "limit labels probe", "Label:", 0, 100);

			slider.SetLimitLabels("Low", "High");
			Assert.AreEqual("Low", slider.MinLimitLabel, "MinLimitLabel should read back what SetLimitLabels was just given");
			Assert.AreEqual("High", slider.MaxLimitLabel, "MaxLimitLabel should read back what SetLimitLabels was just given");

			slider.Dispose();
		}
	}

	[Test]
	public void KeyIncrementValueRoundTrips()
	{
		using (new Application(AppSignature)) {
			Slider slider = new Slider(new Rect(0, 0, 200, 40), "key increment probe", "Label:", 0, 100);

			slider.KeyIncrementValue = 5;
			Assert.AreEqual(5, slider.KeyIncrementValue, "KeyIncrementValue should read back what the property setter was just given");

			slider.Dispose();
		}
	}

	/* COMPLETENESS PASS ADDITIONS BELOW -- covers every property added in
	 * that pass. See Slider.cs's own doc comments (and hs_slider.h's
	 * updated SCOPE note) for the two genuine hardware-verified surprises
	 * these tests deliberately do NOT over-assert: FillColor's value
	 * after a disabling SetFillColor(false, ...) call, and the exact
	 * rounding rule BarThickness applies beyond the specific values a
	 * native probe actually confirmed. */

	[Test]
	public void SnoozeAmountRoundTrips()
	{
		using (new Application(AppSignature)) {
			Slider slider = new Slider(new Rect(0, 0, 200, 40), "snooze probe", "Label:", 0, 100);

			// Hardware-verified default -- see Slider.cs's own doc comment.
			Assert.AreEqual(20000, slider.SnoozeAmount, "SnoozeAmount should default to 20000 microseconds, matching real BeAPI's own constructor default");

			slider.SnoozeAmount = 50000;
			Assert.AreEqual(50000, slider.SnoozeAmount, "SnoozeAmount should read back what the property setter was just given");

			slider.Dispose();
		}
	}

	[Test]
	public void HashMarkCountRoundTrips()
	{
		using (new Application(AppSignature)) {
			Slider slider = new Slider(new Rect(0, 0, 200, 40), "hash count probe", "Label:", 0, 100);

			Assert.AreEqual(0, slider.HashMarkCount, "HashMarkCount should default to 0");

			slider.HashMarkCount = 5;
			Assert.AreEqual(5, slider.HashMarkCount, "HashMarkCount should read back what the property setter was just given");

			slider.Dispose();
		}
	}

	[Test]
	public void HashMarksRoundTrips()
	{
		using (new Application(AppSignature)) {
			Slider slider = new Slider(new Rect(0, 0, 200, 40), "hash marks probe", "Label:", 0, 100);

			Assert.AreEqual(HashMarkLocation.None, slider.HashMarks, "HashMarks should default to None");

			slider.HashMarks = HashMarkLocation.Both;
			Assert.AreEqual(HashMarkLocation.Both, slider.HashMarks, "HashMarks should read back what the property setter was just given");

			// Hardware-verified alias: Top and Left share the same raw
			// value (1) -- see HashMarkLocation.cs's own remarks. Setting
			// one and reading back should compare equal to the other.
			slider.HashMarks = HashMarkLocation.Top;
			Assert.AreEqual(HashMarkLocation.Left, slider.HashMarks, "HashMarkLocation.Top and .Left share the same underlying value and must compare equal");

			slider.Dispose();
		}
	}

	[Test]
	public void BarColorRoundTrips()
	{
		using (new Application(AppSignature)) {
			Slider slider = new Slider(new Rect(0, 0, 200, 40), "bar color probe", "Label:", 0, 100);

			// Hardware-verified default -- Haiku's standard control gray,
			// not a placeholder zero value. See Slider.cs's own doc comment.
			Assert.AreEqual(new RgbColor(184, 184, 184, 255), slider.BarColor, "BarColor should default to Haiku's standard control gray (184,184,184,255)");

			RgbColor newColor = new RgbColor(10, 20, 30, 255);
			slider.BarColor = newColor;
			Assert.AreEqual(newColor, slider.BarColor, "BarColor should read back what the property setter was just given");

			slider.Dispose();
		}
	}

	[Test]
	public void FillColorEnablingRoundTrips()
	{
		// Only asserts the RELIABLE path -- see Slider.cs's own FillColor
		// doc comment for why the value after a disabling SetFillColor
		// call is deliberately not asserted anywhere in this suite.
		using (new Application(AppSignature)) {
			Slider slider = new Slider(new Rect(0, 0, 200, 40), "fill color probe", "Label:", 0, 100);

			Assert.IsFalse(slider.UsesFillColor, "UsesFillColor should default to false");

			RgbColor fill = new RgbColor(200, 100, 50, 255);
			slider.SetFillColor(true, fill);
			Assert.IsTrue(slider.UsesFillColor, "UsesFillColor should read back true after SetFillColor(true, ...)");
			Assert.AreEqual(fill, slider.FillColor, "FillColor should read back exactly what SetFillColor(true, ...) was just given -- confirmed reliable on hardware across every scenario tested");

			slider.SetFillColor(false, fill);
			Assert.IsFalse(slider.UsesFillColor, "UsesFillColor should read back false after SetFillColor(false, ...) -- this half of the call IS reliable, unlike the resulting FillColor value");

			slider.Dispose();
		}
	}

	[Test]
	public void BarThicknessRoundTrips()
	{
		using (new Application(AppSignature)) {
			Slider slider = new Slider(new Rect(0, 0, 200, 40), "bar thickness probe", "Label:", 0, 100);

			// Hardware-verified default for a horizontal slider.
			Assert.AreEqual(6.0f, slider.BarThickness, "BarThickness should default to 6.0 for a horizontal slider");

			// Only integer values, and the specific fractional values a
			// native probe actually confirmed -- see Slider.cs's own
			// BarThickness doc comment for the full rounding write-up.
			slider.BarThickness = 20.0f;
			Assert.AreEqual(20.0f, slider.BarThickness, "BarThickness should read back an integer value exactly");

			slider.BarThickness = 12.5f;
			Assert.AreEqual(13.0f, slider.BarThickness, "BarThickness should round 12.5 up to 13.0, matching the hardware-verified rounding rule");

			slider.BarThickness = 0.0f;
			Assert.AreEqual(1.0f, slider.BarThickness, "BarThickness should clamp 0.0 to a minimum of 1.0, matching the hardware-verified rule");

			slider.Dispose();
		}
	}

	[Test]
	public void AddChildUnderWindowSucceeds()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 300, 100), "SliderTests probe window")) {
				Slider slider = new Slider(new Rect(10, 10, 280, 50), "probe", "Label:", 0, 100);

				// Should not throw -- exercises hs_window_add_child()
				// against a real HSSlider handle for the first time.
				window.AddChild(slider);

				// Leave slider attached -- window's own Dispose() below
				// cascades into it, exercised separately by
				// WindowDisposeCascadesDestroyToAttachedSlider.
			}
		}
	}

	[Test]
	public void AddChildUnderPlainViewSucceeds()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 300, 100), "SliderTests probe window")) {
				View view = new View(new Rect(0, 0, 300, 100), "container");
				window.AddChild(view);

				Slider slider = new Slider(new Rect(10, 10, 280, 50), "probe", "Label:", 0, 100);
				view.AddChild(slider);

				// Leave slider attached -- the window's cascading
				// Dispose() below tears down view, which tears down
				// slider in turn.
			}
		}
	}

	[Test]
	public void RemoveChildDetachesSlider()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 300, 100), "SliderTests probe window")) {
				ProbeSlider slider = new ProbeSlider(new Rect(10, 10, 280, 50), "probe", "Label:", 0, 100);
				window.AddChild(slider);

				bool removed = window.RemoveChild(slider);

				Assert.IsTrue(removed, "RemoveChild should return true for a slider that actually was a direct child");

				// No longer attached -- Dispose() should now succeed
				// rather than throw (see DisposeWhileAttachedThrows for
				// the still-attached case).
				slider.Dispose();
				Assert.IsTrue(slider.DestroyedFired, "Dispose() on a detached slider should fire OnDestroyed");
			}
		}
	}

	[Test]
	public void RemoveChildReturnsFalseForNonChild()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 300, 100), "SliderTests probe window"))
			using (Slider slider = new Slider(new Rect(10, 10, 280, 50), "never added", "Label:", 0, 100)) {
				bool removed = window.RemoveChild(slider);

				Assert.IsFalse(removed, "RemoveChild should return false for a slider that was never added to this window");
			}
		}
	}

	[Test]
	public void DisposeWhileAttachedThrows()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 300, 100), "SliderTests probe window")) {
				Slider slider = new Slider(new Rect(10, 10, 280, 50), "probe", "Label:", 0, 100);
				window.AddChild(slider);

				bool threw = false;
				try {
					slider.Dispose();
				} catch (InvalidOperationException) {
					threw = true;
				}

				Assert.IsTrue(threw,
					"Dispose() on a still-attached Slider should throw InvalidOperationException rather than risk corrupting its parent's child list");

				// Leave it attached -- window's own Dispose() below cleans
				// it up via the implicit cascade.
			}
		}
	}

	[Test]
	public void WindowDisposeCascadesDestroyToAttachedSlider()
	{
		using (new Application(AppSignature)) {
			ProbeSlider slider = new ProbeSlider(new Rect(10, 10, 280, 50), "probe", "Label:", 0, 100);

			using (Window window = new Window(new Rect(0, 0, 300, 100), "SliderTests probe window")) {
				window.AddChild(slider);
				// window.Dispose() fires here, at the end of this `using`
				// block -- never shown, so it deletes the native BWindow
				// synchronously, which recursively deletes its still-
				// attached children, including this slider, with no
				// explicit RemoveChild() or Dispose() call on it at all.
				// Exercises hs_window_destroy()'s cascade against a real
				// HSSlider child for the first time -- HSSlider's own
				// destructor (see hs_slider.cpp) fires its destroyed
				// callback unconditionally, same as every other control
				// in this binding.
			}

			Assert.IsTrue(slider.DestroyedFired,
				"OnDestroyed should fire on a still-attached child slider when its parent window is destroyed, even though nothing disposed it directly");
		}
	}
}
