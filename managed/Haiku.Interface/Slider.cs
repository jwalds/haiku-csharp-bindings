using System;
using System.Runtime.InteropServices;
using Haiku.App;

namespace Haiku.Interface
{
	/*
	 * Managed wrapper over the native HSSlider (see native/include/
	 * hs_slider.h for the full design rationale -- read it before
	 * changing anything here, especially its "BAPPLICATION-AT-
	 * CONSTRUCTION" note on BSlider requiring a live BApplication before
	 * construction, matching TextControl/RadioButton, not CheckBox).
	 *
	 * SCOPE: Position/GetLimits-SetLimits/Orientation/Style/
	 * SetLimitLabels/KeyIncrementValue plus the shared Label/Value/
	 * IsEnabled (via Control) -- hash marks/tick marks, bar/fill colors,
	 * a custom icon, and the snooze amount are real BSlider API this
	 * binding does not expose yet, a deliberate scope decision (see
	 * hs_slider.h's own "SCOPE" note), not an oversight.
	 *
	 * TWO EVENTS, NOT ONE, SAME SHAPE AS TextControl: OnValueChanged
	 * fires repeatedly while the thumb is being dragged (BeAPI's
	 * modification message); OnValueCommitted fires once, when the mouse
	 * button is released (BeAPI's Invoke()). See hs_slider.h's "TWO
	 * DIFFERENT 'CHANGED' EVENTS" note for the exact native mechanism --
	 * neither exposes any BMessage/BInvoker/target plumbing to managed
	 * code, matching Button's OnClick and TextControl's own two hooks.
	 *
	 * LIFECYCLE: same shape as TextControl -- a freshly-constructed
	 * Slider has no parent and is safe to configure (MoveTo, ...) from
	 * whatever thread created it, PROVIDED a BApplication already exists
	 * in the process (see the note above). Once added to a Window or
	 * View, it draws and handles input entirely on its own. The most
	 * common real usage never calls Dispose() at all: once added, its
	 * parent owns it and deletes it automatically, recursively, whenever
	 * that parent itself is destroyed -- OnDestroyed still fires when
	 * that happens.
	 */
	public class Slider : Control
	{
		private readonly SliderValueChangedCallback _valueChangedThunk;
		private readonly SliderValueCommittedCallback _valueCommittedThunk;
		private readonly SliderDestroyedCallback _destroyedThunk;

		/// <summary>
		/// Convenience overload for the common case: a plain horizontal
		/// slider with a block thumb and real BeAPI's own default flags
		/// (B_WILL_DRAW | B_NAVIGABLE | B_FRAME_EVENTS, matching
		/// TextControl's own convenience overload) and no special
		/// resizing mode.
		/// </summary>
		public Slider(Rect frame, string name, string label, int minValue, int maxValue)
			: this(frame, name, label, minValue, maxValue, SliderOrientation.Horizontal,
				ThumbStyle.Block, ViewResizingMode.None,
				ViewFlags.WillDraw | ViewFlags.Navigable | ViewFlags.FrameEvents)
		{
		}

		public Slider(Rect frame, string name, string label, int minValue, int maxValue,
			SliderOrientation orientation, ThumbStyle style,
			ViewResizingMode resizingMode, ViewFlags flags)
			: base(CreateNativeSlider(frame, name, label, minValue, maxValue,
				orientation, style, resizingMode, flags))
		{
			IntPtr userData = SelfHandleUserData;

			_valueChangedThunk = ValueChangedThunk;
			_valueCommittedThunk = ValueCommittedThunk;
			_destroyedThunk = DestroyedThunk;

			Native.hs_slider_set_value_changed_callback(_handle, _valueChangedThunk, userData);
			Native.hs_slider_set_value_committed_callback(_handle, _valueCommittedThunk, userData);
			Native.hs_slider_set_destroyed_callback(_handle, _destroyedThunk, userData);
		}

		// See View.cs's CreateNativeView / TextControl.cs's
		// CreateNativeTextControl -- same "base(...) needs an expression"
		// reason for pulling hs_slider_create() out here. Callers must
		// have a live BApplication already constructed -- see
		// hs_slider.h's note; this call hangs forever otherwise, it does
		// not throw or fail fast.
		private static IntPtr CreateNativeSlider(Rect frame, string name, string label,
			int minValue, int maxValue, SliderOrientation orientation, ThumbStyle style,
			ViewResizingMode resizingMode, ViewFlags flags)
		{
			HsRect nativeFrame = new HsRect {
				Left = frame.Left,
				Top = frame.Top,
				Right = frame.Right,
				Bottom = frame.Bottom,
			};
			return Native.hs_slider_create(nativeFrame, name, label, minValue, maxValue,
				(uint)orientation, (uint)style, (uint)resizingMode, (uint)flags);
		}

		/// <summary>
		/// This slider's current value scaled to a 0.0-1.0 range, where
		/// 0.0 is <see cref="Minimum"/> and 1.0 is <see cref="Maximum"/>
		/// -- a float view of the same underlying state as the inherited
		/// <see cref="Control.Value"/>.
		/// </summary>
		public float Position
		{
			get
			{
				CheckNotConsumed();
				return Native.hs_slider_position(_handle);
			}
			set
			{
				CheckNotConsumed();
				Native.hs_slider_set_position(_handle, value);
			}
		}

		/// <summary>The lower bound of this slider's value range. Set together with <see cref="Maximum"/> via <see cref="SetLimits"/>.</summary>
		public int Minimum
		{
			get
			{
				CheckNotConsumed();
				int minimum, maximum;
				Native.hs_slider_get_limits(_handle, out minimum, out maximum);
				return minimum;
			}
		}

		/// <summary>The upper bound of this slider's value range. Set together with <see cref="Minimum"/> via <see cref="SetLimits"/>.</summary>
		public int Maximum
		{
			get
			{
				CheckNotConsumed();
				int minimum, maximum;
				Native.hs_slider_get_limits(_handle, out minimum, out maximum);
				return maximum;
			}
		}

		/// <summary>
		/// Sets both bounds of this slider's value range at once --
		/// matches real BeAPI's own SetLimits(minimum, maximum), which
		/// has no separate single-ended setter either.
		/// </summary>
		public void SetLimits(int minimum, int maximum)
		{
			CheckNotConsumed();
			Native.hs_slider_set_limits(_handle, minimum, maximum);
		}

		/// <summary>This slider's <see cref="SliderOrientation"/> -- horizontal (the default) or vertical.</summary>
		public SliderOrientation Orientation
		{
			get
			{
				CheckNotConsumed();
				return (SliderOrientation)Native.hs_slider_orientation(_handle);
			}
			set
			{
				CheckNotConsumed();
				Native.hs_slider_set_orientation(_handle, (uint)value);
			}
		}

		/// <summary>This slider's <see cref="ThumbStyle"/> -- a block (the default) or a triangle.</summary>
		public ThumbStyle Style
		{
			get
			{
				CheckNotConsumed();
				return (ThumbStyle)Native.hs_slider_style(_handle);
			}
			set
			{
				CheckNotConsumed();
				Native.hs_slider_set_style(_handle, (uint)value);
			}
		}

		/// <summary>Text drawn at the minimum end of the slider. Set together with <see cref="MaxLimitLabel"/> via <see cref="SetLimitLabels"/>.</summary>
		public string MinLimitLabel
		{
			get
			{
				CheckNotConsumed();
				return Marshal.PtrToStringAnsi(Native.hs_slider_min_limit_label(_handle));
			}
		}

		/// <summary>Text drawn at the maximum end of the slider. Set together with <see cref="MinLimitLabel"/> via <see cref="SetLimitLabels"/>.</summary>
		public string MaxLimitLabel
		{
			get
			{
				CheckNotConsumed();
				return Marshal.PtrToStringAnsi(Native.hs_slider_max_limit_label(_handle));
			}
		}

		/// <summary>
		/// Sets both end labels at once -- matches real BeAPI's own
		/// SetLimitLabels(minLabel, maxLabel), which has no separate
		/// single-ended setter either.
		/// </summary>
		public void SetLimitLabels(string minLabel, string maxLabel)
		{
			CheckNotConsumed();
			Native.hs_slider_set_limit_labels(_handle, minLabel, maxLabel);
		}

		/// <summary>How much the value changes per arrow-key press while this slider has keyboard focus (up/right increment, down/left decrement).</summary>
		public int KeyIncrementValue
		{
			get
			{
				CheckNotConsumed();
				return Native.hs_slider_key_increment_value(_handle);
			}
			set
			{
				CheckNotConsumed();
				Native.hs_slider_set_key_increment_value(_handle, value);
			}
		}

		/* COMPLETENESS PASS ADDITIONS BELOW -- see hs_slider.h's own
		 * updated SCOPE note for the hardware-verified defaults and the
		 * two genuine surprises documented there before any of this was
		 * written (BarThickness's rounding, and FillColor's unreliable
		 * value after a disabling SetFillColor(false, ...) call). */

		/// <summary>
		/// Microseconds between mouse-position samples while the thumb
		/// is being dragged. Hardware-verified default: 20000 (20ms),
		/// matching real BeAPI's own constructor default.
		/// </summary>
		public int SnoozeAmount
		{
			get
			{
				CheckNotConsumed();
				return Native.hs_slider_snooze_amount(_handle);
			}
			set
			{
				CheckNotConsumed();
				Native.hs_slider_set_snooze_amount(_handle, value);
			}
		}

		/// <summary>The number of hash marks (tick marks) drawn alongside the bar. Hardware-verified default: 0. Only visible when <see cref="HashMarks"/> is not <see cref="HashMarkLocation.None"/>.</summary>
		public int HashMarkCount
		{
			get
			{
				CheckNotConsumed();
				return Native.hs_slider_hash_mark_count(_handle);
			}
			set
			{
				CheckNotConsumed();
				Native.hs_slider_set_hash_mark_count(_handle, value);
			}
		}

		/// <summary>Where hash marks are drawn relative to the bar. Hardware-verified default: <see cref="HashMarkLocation.None"/>.</summary>
		public HashMarkLocation HashMarks
		{
			get
			{
				CheckNotConsumed();
				return (HashMarkLocation)Native.hs_slider_hash_marks(_handle);
			}
			set
			{
				CheckNotConsumed();
				Native.hs_slider_set_hash_marks(_handle, (uint)value);
			}
		}

		/// <summary>
		/// The color the bar itself is drawn with. Hardware-verified
		/// default: (184, 184, 184, 255), Haiku's standard control gray
		/// -- not a placeholder zero value.
		/// </summary>
		public RgbColor BarColor
		{
			get
			{
				CheckNotConsumed();
				byte r, g, b, a;
				Native.hs_slider_bar_color(_handle, out r, out g, out b, out a);
				return new RgbColor(r, g, b, a);
			}
			set
			{
				CheckNotConsumed();
				Native.hs_slider_set_bar_color(_handle, value.Red, value.Green, value.Blue, value.Alpha);
			}
		}

		/// <summary>
		/// Whether the portion of the bar from the minimum up to the
		/// current value is drawn with <see cref="FillColor"/> instead
		/// of <see cref="BarColor"/>. Set together with the fill color
		/// itself via <see cref="SetFillColor"/>.
		/// </summary>
		public bool UsesFillColor
		{
			get
			{
				CheckNotConsumed();
				return Native.hs_slider_uses_fill_color(_handle);
			}
		}

		/// <summary>
		/// The fill color set via <see cref="SetFillColor"/>. GENUINE,
		/// HARDWARE-VERIFIED QUIRK, see hs_slider.h's own note on
		/// hs_slider_use_fill_color()/hs_slider_fill_color() for the
		/// full write-up: after a call to <c>SetFillColor(false, ...)</c>
		/// (disabling fill color), the value this property reads back is
		/// NOT reliably predictable from the color that was passed to
		/// that call -- it has been observed as both (0,0,0,0) and
		/// whatever the color was immediately before that call, never
		/// the color actually passed. This property is therefore only
		/// meaningful while <see cref="UsesFillColor"/> is true, which
		/// IS reliable -- <c>SetFillColor(true, color)</c> was confirmed
		/// on hardware to set both properties to exactly what was
		/// passed, every time, across every scenario tested.
		/// </summary>
		public RgbColor FillColor
		{
			get
			{
				CheckNotConsumed();
				byte r, g, b, a;
				Native.hs_slider_fill_color(_handle, out r, out g, out b, out a);
				return new RgbColor(r, g, b, a);
			}
		}

		/// <summary>
		/// Sets both <see cref="UsesFillColor"/> and <see cref="FillColor"/>
		/// at once -- matches real BeAPI's own combined
		/// UseFillColor(useFill, color) setter, which has no separate
		/// single-ended setter either (same shape as
		/// <see cref="SetLimits"/>/<see cref="SetLimitLabels"/> above).
		/// Reliable when <paramref name="useFill"/> is true -- see
		/// <see cref="FillColor"/>'s own doc comment for the
		/// hardware-verified quirk when disabling instead.
		/// </summary>
		public void SetFillColor(bool useFill, RgbColor color)
		{
			CheckNotConsumed();
			Native.hs_slider_use_fill_color(_handle, useFill, color.Red, color.Green, color.Blue, color.Alpha);
		}

		/// <summary>
		/// The bar's thickness in pixels. GENUINE, HARDWARE-VERIFIED
		/// SURPRISE: real BeAPI rounds this to the nearest integer pixel
		/// (setting 12.5 reads back as 13.0, 12.4 as 12.0) and clamps to
		/// a minimum of 1 (setting 0.0 reads back as 1.0) -- confirmed
		/// with a dedicated native probe across eight values before this
		/// property was written, not assumed from "it's just a float".
		/// Hardware-verified default: 6.0 for a default-constructed
		/// horizontal slider.
		/// </summary>
		public float BarThickness
		{
			get
			{
				CheckNotConsumed();
				return Native.hs_slider_bar_thickness(_handle);
			}
			set
			{
				CheckNotConsumed();
				Native.hs_slider_set_bar_thickness(_handle, value);
			}
		}

		/// <summary>
		/// Called on the owning window's thread repeatedly while the
		/// thumb is being dragged -- fires on every drag tick, not just
		/// once (see <see cref="OnValueCommitted"/> for that). No
		/// BMessage/target is involved -- see hs_slider.h's "TWO
		/// DIFFERENT 'CHANGED' EVENTS" note. Default: does nothing.
		/// </summary>
		protected virtual void OnValueChanged() { }

		/// <summary>
		/// Called on the owning window's thread once, when the mouse
		/// button is released after dragging the thumb. Fires once per
		/// drag, unlike <see cref="OnValueChanged"/>. No BMessage/target
		/// is involved -- see hs_slider.h's "TWO DIFFERENT 'CHANGED'
		/// EVENTS" note. Default: does nothing.
		/// </summary>
		protected virtual void OnValueCommitted() { }

		private static Slider FromUserData(IntPtr userData)
		{
			return (Slider)GCHandle.FromIntPtr(userData).Target;
		}

		private static void ValueChangedThunk(IntPtr userData)
		{
			FromUserData(userData).OnValueChanged();
		}

		private static void ValueCommittedThunk(IntPtr userData)
		{
			FromUserData(userData).OnValueCommitted();
		}

		private static void DestroyedThunk(IntPtr userData)
		{
			// Same "consumed-before-notified" ordering as View.cs's/
			// TextControl.cs's own DestroyedThunk -- see
			// ViewBase.MarkDestroyed()'s comment.
			FromUserData(userData).MarkDestroyed();
		}

		protected override void DestroyNativeHandle(IntPtr handle)
		{
			Native.hs_slider_destroy(handle);
		}
	}
}
