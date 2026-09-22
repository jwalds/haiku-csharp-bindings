using System;
using System.Runtime.InteropServices;
using Haiku.App;

namespace Haiku.Interface
{
	/*
	 * Managed wrapper over the native HSColorControl (see native/include/
	 * hs_color_control.h for the full design rationale -- read it before
	 * changing anything here). Full BColorControl parity for the state
	 * surface this binding covers: Label/the raw int32 Value/IsEnabled
	 * via Control, plus Color (the color-typed half of Value), CellSize,
	 * and Layout.
	 *
	 * NO BRect FRAME -- see hs_color_control.h's own note. This
	 * constructor takes a Point start position, not a Rect frame, unlike
	 * every other control in this binding -- real BColorControl computes
	 * its own width/height from layout+cellSize. Query the real,
	 * BeAPI-computed geometry afterward via the inherited Frame
	 * property (ViewBase) if needed.
	 *
	 * LABEL: SET HERE, NOT BY THE NATIVE CONSTRUCTOR -- real
	 * BColorControl's own constructor has no label parameter (see
	 * hs_color_control.h's hardware-verified finding that Label() reads
	 * back NULL from "name" alone). For parity with every other
	 * control's constructor shape (which all take a label directly),
	 * this constructor accepts one and applies it afterward via the
	 * inherited Control.Label setter -- a plain managed-side convenience,
	 * not something the native shim does.
	 *
	 * LIFECYCLE: same shape as Button/TextControl/Slider -- a freshly-
	 * constructed ColorControl has no parent and is safe to configure
	 * from whatever thread created it. Once added to a Window or View,
	 * it draws and handles input entirely on its own (see Control.cs's
	 * own remarks for why there's no OnDraw/OnMouseDown/... here).
	 * Constructing one before a BApplication exists in the process
	 * hangs forever -- hardware-verified, matching TextControl/
	 * RadioButton/Slider, not CheckBox.
	 */
	public class ColorControl : Control
	{
		private readonly ColorControlValueChangedCallback _valueChangedThunk;
		private readonly ColorControlDestroyedCallback _destroyedThunk;

		/// <summary>
		/// Convenience overload: a 32-column by 8-row grid with a 6px
		/// cell size and no offscreen double-buffering -- the layout
		/// this binding's own hardware probes and Sample.exe's
		/// DemoColorControl use, not a value BeAPI itself defaults to.
		/// </summary>
		public ColorControl(Point start, string name, string label)
			: this(start, ColorControlLayout.Cells32x8, 6.0f, name, label, false)
		{
		}

		public ColorControl(Point start, ColorControlLayout layout, float cellSize,
			string name, string label, bool useOffscreen)
			: base(CreateNativeColorControl(start, layout, cellSize, name, useOffscreen))
		{
			// See this class's own "LABEL" remarks above -- applied
			// after construction, not passed to the native constructor.
			Label = label;

			IntPtr userData = SelfHandleUserData;

			_valueChangedThunk = ValueChangedThunk;
			_destroyedThunk = DestroyedThunk;

			Native.hs_color_control_set_value_changed_callback(_handle, _valueChangedThunk, userData);
			Native.hs_color_control_set_destroyed_callback(_handle, _destroyedThunk, userData);
		}

		// See View.cs's CreateNativeView -- same "base(...) needs an
		// expression" reason for pulling hs_color_control_create() out
		// here.
		private static IntPtr CreateNativeColorControl(Point start, ColorControlLayout layout,
			float cellSize, string name, bool useOffscreen)
		{
			HsPoint nativeStart = new HsPoint {
				X = start.X,
				Y = start.Y,
			};
			return Native.hs_color_control_create(nativeStart, (uint)layout, cellSize,
				name, useOffscreen);
		}

		/// <summary>
		/// This control's current color. Get/set wrap real BeAPI's own
		/// BColorControl::ValueAsColor()/SetValue(rgb_color) directly
		/// (see hs_color_control.h) rather than reimplementing their
		/// packing formula in C# -- the inherited <see cref="Control.Value"/>
		/// still gives you the raw packed int32 (red&lt;&lt;24 +
		/// green&lt;&lt;16 + blue&lt;&lt;8, alpha not encoded, and
		/// subject to ordinary int32 sign wraparound once red &gt;= 128
		/// -- hardware-confirmed) directly if you need it, but
		/// <see cref="Color"/> is the one to use for anything
		/// color-shaped. Note real BColorControl's packing never encodes
		/// alpha -- <see cref="RgbColor.Alpha"/> always reads back 255
		/// here, regardless of what alpha was set, hardware-confirmed.
		/// </summary>
		public RgbColor Color
		{
			get
			{
				CheckNotConsumed();
				byte red, green, blue, alpha;
				Native.hs_color_control_value_as_color(_handle, out red, out green,
					out blue, out alpha);
				return new RgbColor(red, green, blue, alpha);
			}
			set
			{
				CheckNotConsumed();
				Native.hs_color_control_set_value_color(_handle, value.Red, value.Green,
					value.Blue, value.Alpha);
			}
		}

		/// <summary>The size, in pixels, of one color cell in the grid. Hardware-confirmed plain float round-trip, no rounding/clamping surprise the way Slider's BarThickness has.</summary>
		public float CellSize
		{
			get
			{
				CheckNotConsumed();
				return Native.hs_color_control_cell_size(_handle);
			}
			set
			{
				CheckNotConsumed();
				Native.hs_color_control_set_cell_size(_handle, value);
			}
		}

		/// <summary>This control's grid <see cref="ColorControlLayout"/> -- changing it does not change <see cref="CellSize"/>, and vice versa; both are independent, hardware-confirmed.</summary>
		public ColorControlLayout Layout
		{
			get
			{
				CheckNotConsumed();
				return (ColorControlLayout)Native.hs_color_control_layout(_handle);
			}
			set
			{
				CheckNotConsumed();
				Native.hs_color_control_set_layout(_handle, (uint)value);
			}
		}

		/// <summary>
		/// Called on the owning window's thread whenever the user picks
		/// a new color -- any click on the palette or a ramp. No
		/// BMessage/target is involved -- see hs_color_control.h's "NO
		/// BMessage/BInvoker/TARGET PLUMBING" note. Unlike Slider, there
		/// is no separate drag-vs-commit split: a color pick is a
		/// single, atomic value change, so this is the only hook.
		/// Default: does nothing.
		/// </summary>
		protected virtual void OnColorChanged() { }

		private static ColorControl FromUserData(IntPtr userData)
		{
			return (ColorControl)GCHandle.FromIntPtr(userData).Target;
		}

		private static void ValueChangedThunk(IntPtr userData)
		{
			FromUserData(userData).OnColorChanged();
		}

		private static void DestroyedThunk(IntPtr userData)
		{
			// Same "consumed-before-notified" ordering as every other
			// control's own DestroyedThunk -- see ViewBase.MarkDestroyed()'s
			// comment.
			FromUserData(userData).MarkDestroyed();
		}

		protected override void DestroyNativeHandle(IntPtr handle)
		{
			Native.hs_color_control_destroy(handle);
		}
	}
}
