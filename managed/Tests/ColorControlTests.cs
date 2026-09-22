using System;
using Haiku.App;
using Haiku.Interface;
using Haiku.Testing;

/// <summary>
/// Regression coverage for Haiku.Interface.ColorControl (see
/// managed/Haiku.Interface/ColorControl.cs and native/include/
/// hs_color_control.h) -- this binding's sixth BControl-derived widget,
/// re-exercising the shared Control/ViewBase surface every prior widget
/// already proved out, plus BColorControl's own Color/CellSize/Layout
/// surface and its BPoint-based (not BRect-based) construction.
///
/// THERE IS NO AUTOMATED TEST HERE THAT ACTUALLY FIRES OnColorChanged.
/// Same reasoning as every other input hook in this binding (Button's
/// OnClick, Slider's OnValueChanged/OnValueCommitted): there is no
/// supported way to synthesize a real palette/ramp click from inside the
/// same process. What IS automatable, and covered below, is everything
/// that doesn't depend on a real input event arriving. Real event-firing
/// is verified visually instead, via Sample.exe's DemoColorControl --
/// same division of labor as every prior widget's own input hooks.
///
/// EVERY TEST HERE OPENS AN Application FIRST, matching
/// TextControlTests.cs/RadioButtonTests.cs/SliderTests.cs. This is
/// load-bearing, not stylistic: constructing a BColorControl (and
/// therefore an HSColorControl) with no BApplication object yet
/// constructed in the process hangs indefinitely -- hardware-verified
/// via an isolated, flush-per-step probe (see hs_color_control.h's "A
/// SIXTH VERIFIED BApplication REQUIREMENT" note), matching
/// TextControl/RadioButton/Slider, unlike CheckBox.
///
/// The exact Frame() this binding's own construction parameters produce
/// (ConstructionAndGeometryRoundTrip) was itself hardware-verified with
/// a small isolated probe before being relied on here -- real
/// BColorControl computes its own width/height from layout+cellSize
/// (there is no frame parameter to pass, unlike every other control in
/// this binding), so this asserts the exact, hardware-confirmed result
/// rather than a generic "positive width/height" sanity check.
/// </summary>
[Haiku.Testing.TestModule("BColorControl")]
public class ColorControlTests
{
	private const string AppSignature = "application/x-vnd.HaikuSharp-Tests-ColorControl";

	private class ProbeColorControl : ColorControl
	{
		public volatile bool ColorChangedFired;
		public volatile bool DestroyedFired;

		public ProbeColorControl(Point start, string name, string label)
			: base(start, name, label)
		{
		}

		protected override void OnColorChanged()
		{
			ColorChangedFired = true;
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
			Point start = new Point(10, 10);
			ColorControl colorControl = new ColorControl(start, ColorControlLayout.Cells32x8,
				6.0f, "geometry probe", "Label:", false);

			Rect frame = colorControl.Frame;
			// Hardware-confirmed exact result for BPoint(10,10) +
			// B_CELLS_32x8 + cellSize=6.0 -- real BColorControl computes
			// this itself, not something this binding chose.
			Assert.AreEqual(10.0f, frame.Left, "Frame.Left should read back the given start.X");
			Assert.AreEqual(10.0f, frame.Top, "Frame.Top should read back the given start.Y");
			Assert.AreEqual(298.0f, frame.Right, "Frame.Right should match real BeAPI's own hardware-confirmed computed width for this layout/cellSize");
			Assert.AreEqual(82.0f, frame.Bottom, "Frame.Bottom should match real BeAPI's own hardware-confirmed computed height for this layout/cellSize");

			colorControl.Dispose();
		}
	}

	[Test]
	public void LabelRoundTrips()
	{
		using (new Application(AppSignature)) {
			// Unlike every other control in this binding, real
			// BColorControl's own native constructor has no label
			// parameter at all -- Label is applied afterward by
			// ColorControl.cs itself (see its own "LABEL" remarks).
			// This re-verifies Control's shared Label property still
			// works correctly for that managed-side-applied case.
			ColorControl colorControl = new ColorControl(new Point(0, 0), "label probe", "Original:");
			Assert.AreEqual("Original:", colorControl.Label, "Label should read back what the constructor was given, even though it's applied by ColorControl.cs, not the native constructor");

			colorControl.Label = "Changed:";
			Assert.AreEqual("Changed:", colorControl.Label, "Label should read back what the property setter was just given");

			colorControl.Dispose();
		}
	}

	[Test]
	public void NullLabelLeavesLabelUnset()
	{
		using (new Application(AppSignature)) {
			// Hardware-confirmed: Label() reads back NULL from a fresh
			// BColorControl when no label was ever set -- this
			// re-confirms passing null through ColorControl.cs's own
			// constructor convenience doesn't crash and leaves Label
			// null, exactly like the underlying native fact.
			ColorControl colorControl = new ColorControl(new Point(0, 0), "no label probe", null);
			Assert.IsNull(colorControl.Label, "Label should read back null when the constructor was given a null label, matching the hardware-verified native default");

			colorControl.Dispose();
		}
	}

	[Test]
	public void IsEnabledRoundTrips()
	{
		using (new Application(AppSignature)) {
			ColorControl colorControl = new ColorControl(new Point(0, 0), "enabled probe", "Label:");

			colorControl.IsEnabled = false;
			Assert.IsFalse(colorControl.IsEnabled, "IsEnabled should read back false after SetEnabled(false)");

			colorControl.IsEnabled = true;
			Assert.IsTrue(colorControl.IsEnabled, "IsEnabled should read back true after SetEnabled(true)");

			colorControl.Dispose();
		}
	}

	[Test]
	public void ColorRoundTrips()
	{
		using (new Application(AppSignature)) {
			ColorControl colorControl = new ColorControl(new Point(0, 0), "color probe", "Label:");

			// Hardware-confirmed default: opaque black, not an
			// unspecified/garbage value the way Slider's FillColor is
			// before SetFillColor has ever been called.
			Assert.AreEqual(new RgbColor(0, 0, 0, 255), colorControl.Color, "Color should default to opaque black (0,0,0,255)");

			RgbColor newColor = new RgbColor(10, 20, 30, 255);
			colorControl.Color = newColor;
			Assert.AreEqual(newColor, colorControl.Color, "Color should read back exactly what the property setter was just given");

			// Hardware-confirmed: real BColorControl's packing never
			// encodes alpha -- ValueAsColor() always returns 255
			// regardless of what alpha SetValue(rgb_color) was given.
			colorControl.Color = new RgbColor(50, 60, 70, 128);
			Assert.AreEqual((byte)255, colorControl.Color.Alpha, "Alpha should always read back 255 regardless of what alpha was set, matching the hardware-verified packing format that never encodes it");

			colorControl.Dispose();
		}
	}

	[Test]
	public void CellSizeRoundTrips()
	{
		using (new Application(AppSignature)) {
			ColorControl colorControl = new ColorControl(new Point(0, 0), ColorControlLayout.Cells32x8,
				6.0f, "cell size probe", "Label:", false);
			Assert.AreEqual(6.0f, colorControl.CellSize, "CellSize should read back exactly what the constructor was given");

			colorControl.CellSize = 12.0f;
			Assert.AreEqual(12.0f, colorControl.CellSize, "CellSize should read back exactly what the property setter was just given, a plain float round-trip with no rounding surprise");

			colorControl.Dispose();
		}
	}

	[Test]
	public void LayoutRoundTrips()
	{
		using (new Application(AppSignature)) {
			ColorControl colorControl = new ColorControl(new Point(0, 0), ColorControlLayout.Cells32x8,
				6.0f, "layout probe", "Label:", false);
			Assert.AreEqual(ColorControlLayout.Cells32x8, colorControl.Layout, "Layout should read back exactly what the constructor was given");

			colorControl.Layout = ColorControlLayout.Cells8x32;
			Assert.AreEqual(ColorControlLayout.Cells8x32, colorControl.Layout, "Layout should read back exactly what the property setter was just given");

			colorControl.Dispose();
		}
	}

	[Test]
	public void AddChildUnderWindowSucceeds()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 320, 120), "ColorControlTests probe window")) {
				ColorControl colorControl = new ColorControl(new Point(10, 10), "probe", "Label:");

				// Should not throw -- exercises hs_window_add_child()
				// against a real HSColorControl handle for the first
				// time.
				window.AddChild(colorControl);

				// Leave colorControl attached -- window's own Dispose()
				// below cascades into it, exercised separately by
				// WindowDisposeCascadesDestroyToAttachedColorControl.
			}
		}
	}

	[Test]
	public void AddChildUnderPlainViewSucceeds()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 320, 120), "ColorControlTests probe window")) {
				View view = new View(new Rect(0, 0, 320, 120), "container");
				window.AddChild(view);

				ColorControl colorControl = new ColorControl(new Point(10, 10), "probe", "Label:");
				view.AddChild(colorControl);

				// Leave colorControl attached -- the window's cascading
				// Dispose() below tears down view, which tears down
				// colorControl in turn.
			}
		}
	}

	[Test]
	public void RemoveChildDetachesColorControl()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 320, 120), "ColorControlTests probe window")) {
				ProbeColorControl colorControl = new ProbeColorControl(new Point(10, 10), "probe", "Label:");
				window.AddChild(colorControl);

				bool removed = window.RemoveChild(colorControl);

				Assert.IsTrue(removed, "RemoveChild should return true for a color control that actually was a direct child");

				// No longer attached -- Dispose() should now succeed
				// rather than throw (see DisposeWhileAttachedThrows for
				// the still-attached case).
				colorControl.Dispose();
				Assert.IsTrue(colorControl.DestroyedFired, "Dispose() on a detached color control should fire OnDestroyed");
			}
		}
	}

	[Test]
	public void RemoveChildReturnsFalseForNonChild()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 320, 120), "ColorControlTests probe window"))
			using (ColorControl colorControl = new ColorControl(new Point(10, 10), "never added", "Label:")) {
				bool removed = window.RemoveChild(colorControl);

				Assert.IsFalse(removed, "RemoveChild should return false for a color control that was never added to this window");
			}
		}
	}

	[Test]
	public void DisposeWhileAttachedThrows()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 320, 120), "ColorControlTests probe window")) {
				ColorControl colorControl = new ColorControl(new Point(10, 10), "probe", "Label:");
				window.AddChild(colorControl);

				bool threw = false;
				try {
					colorControl.Dispose();
				} catch (InvalidOperationException) {
					threw = true;
				}

				Assert.IsTrue(threw,
					"Dispose() on a still-attached ColorControl should throw InvalidOperationException rather than risk corrupting its parent's child list");

				// Leave it attached -- window's own Dispose() below
				// cleans it up via the implicit cascade.
			}
		}
	}

	[Test]
	public void WindowDisposeCascadesDestroyToAttachedColorControl()
	{
		using (new Application(AppSignature)) {
			ProbeColorControl colorControl = new ProbeColorControl(new Point(10, 10), "probe", "Label:");

			using (Window window = new Window(new Rect(0, 0, 320, 120), "ColorControlTests probe window")) {
				window.AddChild(colorControl);
				// window.Dispose() fires here, at the end of this `using`
				// block -- never shown, so it deletes the native BWindow
				// synchronously, which recursively deletes its still-
				// attached children, including this color control, with
				// no explicit RemoveChild() or Dispose() call on it at
				// all. Exercises hs_window_destroy()'s cascade against a
				// real HSColorControl child for the first time --
				// HSColorControl's own destructor (see
				// hs_color_control.cpp) fires its destroyed callback
				// unconditionally, same as every other control in this
				// binding.
			}

			Assert.IsTrue(colorControl.DestroyedFired,
				"OnDestroyed should fire on a still-attached child color control when its parent window is destroyed, even though nothing disposed it directly");
		}
	}
}
