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
