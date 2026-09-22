using System;
using System.Runtime.InteropServices;

namespace Haiku.Interface
{
	/* Delegate shapes matching hs_window.h's callback typedefs exactly.
	 * Declared here at namespace level (not nested in Native), matching
	 * Haiku.App.Native's own convention -- that project already got bitten
	 * once by a delegate nested inside a static class colliding with a
	 * same-named type elsewhere (see the top-level README). Prefixed with
	 * "Window" so a future BView (or anything else in this namespace) can
	 * have its own MessageReceived-shaped delegate without a name clash. */
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void WindowMessageReceivedCallback(IntPtr userData, IntPtr message);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate int WindowQuitRequestedCallback(IntPtr userData);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void WindowDestroyedCallback(IntPtr userData);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void ViewAttachedToWindowCallback(IntPtr userData);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void ViewDetachedFromWindowCallback(IntPtr userData);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void ViewDrawCallback(IntPtr userData, HsRect updateRect);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void ViewDestroyedCallback(IntPtr userData);

	/* Mouse/keyboard delegate shapes matching hs_view.h's callback typedefs
	 * exactly. MouseUp deliberately has no buttons parameter -- B_MOUSE_UP
	 * messages do not carry a "buttons" field at all (verified against the
	 * Be Book's message-constants documentation, not assumed; see the
	 * MOUSE AND KEYBOARD INPUT note in hs_view.h), unlike MouseDown and
	 * MouseMoved which both do. `bytes` is a borrowed pointer -- View.cs
	 * must copy it into a managed byte[] before returning from the
	 * callback, same rule as any other borrowed pointer in this binding. */
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void ViewMouseDownCallback(IntPtr userData, HsPoint where, uint buttons, uint modifiers);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void ViewMouseUpCallback(IntPtr userData, HsPoint where, uint modifiers);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void ViewMouseMovedCallback(IntPtr userData, HsPoint where, uint transit, uint buttons);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void ViewKeyDownCallback(IntPtr userData, IntPtr bytes, int numBytes, uint modifiers);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void ViewKeyUpCallback(IntPtr userData, IntPtr bytes, int numBytes, uint modifiers);

	/* Button delegate shapes matching hs_button.h's callback typedefs
	 * exactly. No BMessage/target involved in ButtonClickCallback -- see
	 * hs_button.h's "NO BMessage/BInvoker/TARGET PLUMBING" note. */
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void ButtonClickCallback(IntPtr userData);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void ButtonDestroyedCallback(IntPtr userData);

	/* ColorControl delegate shapes matching hs_color_control.h's
	 * callback typedefs exactly. Same "no BMessage/target involved"
	 * shape as ButtonClickCallback above -- see hs_color_control.h's
	 * own "NO BMessage/BInvoker/TARGET PLUMBING" note. */
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void ColorControlValueChangedCallback(IntPtr userData);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void ColorControlDestroyedCallback(IntPtr userData);

	/* TextControl delegate shapes matching hs_text_control.h's callback
	 * typedefs exactly. No BMessage/target involved in either callback --
	 * see hs_text_control.h's "TWO DIFFERENT 'CHANGED' EVENTS" note. */
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void TextControlTextChangedCallback(IntPtr userData);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void TextControlTextCommittedCallback(IntPtr userData);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void TextControlDestroyedCallback(IntPtr userData);

	/* CheckBox delegate shapes matching hs_checkbox.h's callback typedefs
	 * exactly. No BMessage/target involved -- see hs_checkbox.h's "NO
	 * BMessage/BInvoker/TARGET PLUMBING" note. */
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void CheckBoxClickCallback(IntPtr userData);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void CheckBoxDestroyedCallback(IntPtr userData);

	/* RadioButton delegate shapes matching hs_radio_button.h's callback
	 * typedefs exactly. Same "no BMessage/target" shape as CheckBox. */
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void RadioButtonClickCallback(IntPtr userData);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void RadioButtonDestroyedCallback(IntPtr userData);

	/* Slider delegate shapes matching hs_slider.h's callback typedefs
	 * exactly. No BMessage/target involved in either -- same "TWO
	 * DIFFERENT 'CHANGED' EVENTS" shape as TextControl's own two hooks,
	 * see hs_slider.h's note of that name. */
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void SliderValueChangedCallback(IntPtr userData);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void SliderValueCommittedCallback(IntPtr userData);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void SliderDestroyedCallback(IntPtr userData);

	/* Mirrors hs_rect (see native/include/hs_types.h) exactly. Haiku.App.Native
	 * already has its own internal HsRect for the same purpose, but that one
	 * is `internal` to the Haiku.App assembly and invisible here. Rather than
	 * reach for InternalsVisibleTo to share a six-line plain-data struct
	 * (whose field layout can only ever change if hs_types.h itself does),
	 * this duplicates it locally -- simpler than taking on an assembly-
	 * linking dependency for something this small. (Message, in Window.cs's
	 * borrowed-handle case below, is the one place in this kit where that
	 * tradeoff comes out the other way -- see Haiku.App's AssemblyInfo.cs.) */
	[StructLayout(LayoutKind.Sequential)]
	internal struct HsRect
	{
		internal float Left;
		internal float Top;
		internal float Right;
		internal float Bottom;
	}

	/* Mirrors hs_point (see native/include/hs_types.h) exactly. Same
	 * duplicate-rather-than-InternalsVisibleTo rationale as HsRect above. */
	[StructLayout(LayoutKind.Sequential)]
	internal struct HsPoint
	{
		internal float X;
		internal float Y;
	}

	/* Raw P/Invoke surface for hs_window.h. Nothing here is meant to be
	 * called directly by binding consumers -- Window.cs wraps every one of
	 * these in a safe, idiomatic method/property. See Haiku.App.Native's own
	 * header comment for why every DllImport for a kit lives in one file. */
	internal static class Native
	{
		private const string Lib = "haikusharp";

		[DllImport(Lib)]
		internal static extern IntPtr hs_window_create(HsRect frame, string title,
			int look, int feel, uint flags);

		[DllImport(Lib)]
		internal static extern void hs_window_destroy(IntPtr window);

		[DllImport(Lib)]
		internal static extern void hs_window_set_message_received_callback(IntPtr window,
			WindowMessageReceivedCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_window_set_quit_requested_callback(IntPtr window,
			WindowQuitRequestedCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_window_set_destroyed_callback(IntPtr window,
			WindowDestroyedCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_window_show(IntPtr window);

		[DllImport(Lib)]
		internal static extern void hs_window_hide(IntPtr window);

		[DllImport(Lib)]
		[return: MarshalAs(UnmanagedType.I1)]
		internal static extern bool hs_window_is_hidden(IntPtr window);

		[DllImport(Lib)]
		internal static extern void hs_window_quit(IntPtr window);

		[DllImport(Lib)]
		[return: MarshalAs(UnmanagedType.I1)]
		internal static extern bool hs_window_lock(IntPtr window);

		[DllImport(Lib)]
		internal static extern void hs_window_unlock(IntPtr window);

		[DllImport(Lib)]
		[return: MarshalAs(UnmanagedType.I1)]
		internal static extern bool hs_window_is_locked(IntPtr window);

		[DllImport(Lib)]
		internal static extern void hs_window_set_title(IntPtr window, string title);

		/* Returns a pointer into BWindow's own internal storage (its BLooper
		 * name), same borrowed-pointer situation as hs_message_find_string --
		 * declared as IntPtr (not string) so Window.cs can copy it into a
		 * managed string immediately with Marshal.PtrToStringAnsi, rather
		 * than let the marshaler treat it as an owned allocation it should
		 * free (which it is not). */
		[DllImport(Lib)]
		internal static extern IntPtr hs_window_title(IntPtr window);

		[DllImport(Lib)]
		internal static extern void hs_window_get_frame(IntPtr window, out HsRect outFrame);

		[DllImport(Lib)]
		internal static extern void hs_window_move_to(IntPtr window, float x, float y);

		[DllImport(Lib)]
		internal static extern void hs_window_resize_to(IntPtr window, float width, float height);

		[DllImport(Lib)]
		internal static extern void hs_window_add_child(IntPtr window, IntPtr view);

		[DllImport(Lib)]
		[return: MarshalAs(UnmanagedType.I1)]
		internal static extern bool hs_window_remove_child(IntPtr window, IntPtr view);

		/* Raw P/Invoke surface for hs_view.h -- View.cs wraps every one of
		 * these, same as Window.cs does for the functions above. */
		[DllImport(Lib)]
		internal static extern IntPtr hs_view_create(HsRect frame, string name,
			uint resizingMode, uint flags);

		[DllImport(Lib)]
		internal static extern void hs_view_destroy(IntPtr view);

		[DllImport(Lib)]
		internal static extern void hs_view_set_attached_to_window_callback(IntPtr view,
			ViewAttachedToWindowCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_view_set_detached_from_window_callback(IntPtr view,
			ViewDetachedFromWindowCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_view_set_draw_callback(IntPtr view,
			ViewDrawCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_view_set_destroyed_callback(IntPtr view,
			ViewDestroyedCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_view_add_child(IntPtr view, IntPtr child);

		[DllImport(Lib)]
		[return: MarshalAs(UnmanagedType.I1)]
		internal static extern bool hs_view_remove_child(IntPtr view, IntPtr child);

		[DllImport(Lib)]
		internal static extern void hs_view_get_frame(IntPtr view, out HsRect outFrame);

		[DllImport(Lib)]
		internal static extern void hs_view_get_bounds(IntPtr view, out HsRect outBounds);

		[DllImport(Lib)]
		internal static extern void hs_view_move_to(IntPtr view, float x, float y);

		[DllImport(Lib)]
		internal static extern void hs_view_resize_to(IntPtr view, float width, float height);

		[DllImport(Lib)]
		internal static extern void hs_view_set_high_color(IntPtr view,
			byte red, byte green, byte blue, byte alpha);

		[DllImport(Lib)]
		internal static extern void hs_view_set_low_color(IntPtr view,
			byte red, byte green, byte blue, byte alpha);

		[DllImport(Lib)]
		internal static extern void hs_view_set_view_color(IntPtr view,
			byte red, byte green, byte blue, byte alpha);

		[DllImport(Lib)]
		internal static extern void hs_view_fill_rect(IntPtr view, HsRect rect);

		[DllImport(Lib)]
		internal static extern void hs_view_stroke_rect(IntPtr view, HsRect rect);

		[DllImport(Lib)]
		internal static extern void hs_view_stroke_line(IntPtr view, HsPoint start, HsPoint end);

		[DllImport(Lib)]
		internal static extern void hs_view_draw_string(IntPtr view, string text, HsPoint location);

		/* Mouse/keyboard/focus/invalidate P/Invoke surface -- see hs_view.h's
		 * MOUSE AND KEYBOARD INPUT note for the verified semantics behind
		 * each of these (buttons bitmask values, transit codes, the
		 * MouseUp buttons asymmetry, KeyDown/KeyUp bytes semantics, and
		 * MakeFocus/B_NAVIGABLE independence). */
		[DllImport(Lib)]
		internal static extern void hs_view_set_mouse_down_callback(IntPtr view,
			ViewMouseDownCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_view_set_mouse_up_callback(IntPtr view,
			ViewMouseUpCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_view_set_mouse_moved_callback(IntPtr view,
			ViewMouseMovedCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_view_set_key_down_callback(IntPtr view,
			ViewKeyDownCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_view_set_key_up_callback(IntPtr view,
			ViewKeyUpCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_view_invalidate(IntPtr view);

		[DllImport(Lib)]
		internal static extern void hs_view_make_focus(IntPtr view,
			[MarshalAs(UnmanagedType.I1)] bool focus);

		[DllImport(Lib)]
		[return: MarshalAs(UnmanagedType.I1)]
		internal static extern bool hs_view_is_focus(IntPtr view);

		/* Wraps the global modifiers() BeAPI function -- see hs_view.h's
		 * own doc comment on hs_modifiers() and Modifiers.cs's Current
		 * property, which is what actually calls this. */
		[DllImport(Lib)]
		internal static extern uint hs_modifiers();

		/* Control -- shared BControl-level state (Label/Value/IsEnabled),
		 * see hs_control.h for the full design rationale. Used against
		 * any concrete control's handle (Button, TextControl, ...), cast
		 * straight to BControl* on the native side -- verified safe by
		 * an ABI probe on real hardware. hs_view_get_frame/move_to/
		 * resize_to above are reused directly the same way for
		 * geometry; there is no separate hs_control_get_frame/move_to/
		 * resize_to. */
		[DllImport(Lib)]
		internal static extern void hs_control_set_label(IntPtr control, string label);

		/* Returns a pointer into BControl's own internal storage, same
		 * borrowed-pointer situation as hs_window_title -- Control.cs's
		 * Label getter copies it into a managed string immediately via
		 * Marshal.PtrToStringAnsi, same treatment as Window.Title. This
		 * is why the return type here is IntPtr, not string -- see
		 * hs_window_title's own comment just above for why. */
		[DllImport(Lib)]
		internal static extern IntPtr hs_control_label(IntPtr control);

		[DllImport(Lib)]
		internal static extern void hs_control_set_value(IntPtr control, int value);

		[DllImport(Lib)]
		internal static extern int hs_control_value(IntPtr control);

		[DllImport(Lib)]
		internal static extern void hs_control_set_enabled(IntPtr control,
			[MarshalAs(UnmanagedType.I1)] bool enabled);

		[DllImport(Lib)]
		[return: MarshalAs(UnmanagedType.I1)]
		internal static extern bool hs_control_is_enabled(IntPtr control);

		/* Button -- see hs_button.h for the full design rationale.
		 * Label/Value/IsEnabled are NOT declared here -- see the shared
		 * hs_control_* block just above. */
		[DllImport(Lib)]
		internal static extern IntPtr hs_button_create(HsRect frame, string name,
			string label, uint resizingMode, uint flags);

		[DllImport(Lib)]
		internal static extern void hs_button_destroy(IntPtr button);

		[DllImport(Lib)]
		internal static extern void hs_button_set_click_callback(IntPtr button,
			ButtonClickCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_button_set_destroyed_callback(IntPtr button,
			ButtonDestroyedCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_button_make_default(IntPtr button,
			[MarshalAs(UnmanagedType.I1)] bool isDefault);

		[DllImport(Lib)]
		[return: MarshalAs(UnmanagedType.I1)]
		internal static extern bool hs_button_is_default(IntPtr button);

		[DllImport(Lib)]
		internal static extern void hs_button_set_flat(IntPtr button,
			[MarshalAs(UnmanagedType.I1)] bool flat);

		[DllImport(Lib)]
		[return: MarshalAs(UnmanagedType.I1)]
		internal static extern bool hs_button_is_flat(IntPtr button);

		[DllImport(Lib)]
		internal static extern void hs_button_set_behavior(IntPtr button, uint behavior);

		[DllImport(Lib)]
		internal static extern uint hs_button_behavior(IntPtr button);

		/* TextControl -- see hs_text_control.h for the full design
		 * rationale, including the "MUST NOT be called before a
		 * BApplication has been constructed" note on hs_text_control_create.
		 * Label/Value/IsEnabled are NOT declared here either -- same
		 * shared hs_control_* block above, reused for a text control's
		 * handle exactly as it is for a button's. */
		[DllImport(Lib)]
		internal static extern IntPtr hs_text_control_create(HsRect frame,
			string name, string label, string text, uint resizingMode,
			uint flags);

		[DllImport(Lib)]
		internal static extern void hs_text_control_destroy(IntPtr textControl);

		[DllImport(Lib)]
		internal static extern void hs_text_control_set_text_changed_callback(
			IntPtr textControl, TextControlTextChangedCallback callback,
			IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_text_control_set_text_committed_callback(
			IntPtr textControl, TextControlTextCommittedCallback callback,
			IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_text_control_set_destroyed_callback(
			IntPtr textControl, TextControlDestroyedCallback callback,
			IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_text_control_set_text(IntPtr textControl,
			string text);

		/* Returns a pointer into BTextControl's own internal storage,
		 * same borrowed-pointer situation as hs_control_label above --
		 * TextControl.cs's Text getter copies it into a managed string
		 * immediately via Marshal.PtrToStringAnsi. */
		[DllImport(Lib)]
		internal static extern IntPtr hs_text_control_text(IntPtr textControl);

		/* CheckBox -- see hs_checkbox.h for the full design rationale.
		 * Label/Value/IsEnabled are NOT declared here -- see the shared
		 * hs_control_* block above, reused for a checkbox handle exactly
		 * as it is for a button's/text control's. */
		[DllImport(Lib)]
		internal static extern IntPtr hs_checkbox_create(HsRect frame, string name,
			string label, uint resizingMode, uint flags);

		[DllImport(Lib)]
		internal static extern void hs_checkbox_destroy(IntPtr checkbox);

		[DllImport(Lib)]
		internal static extern void hs_checkbox_set_click_callback(IntPtr checkbox,
			CheckBoxClickCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_checkbox_set_destroyed_callback(IntPtr checkbox,
			CheckBoxDestroyedCallback callback, IntPtr userData);

		/* RadioButton -- see hs_radio_button.h for the full design
		 * rationale, including the "REQUIRES A LIVE BApplication TO
		 * ALREADY EXIST" note on hs_radio_button_create -- matching
		 * hs_text_control_create's own such note, but NOT shared by
		 * hs_checkbox_create above. Label/Value/IsEnabled are NOT
		 * declared here either -- same shared hs_control_* block. */
		[DllImport(Lib)]
		internal static extern IntPtr hs_radio_button_create(HsRect frame, string name,
			string label, uint resizingMode, uint flags);

		[DllImport(Lib)]
		internal static extern void hs_radio_button_destroy(IntPtr radioButton);

		[DllImport(Lib)]
		internal static extern void hs_radio_button_set_click_callback(IntPtr radioButton,
			RadioButtonClickCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_radio_button_set_destroyed_callback(IntPtr radioButton,
			RadioButtonDestroyedCallback callback, IntPtr userData);

		/* Slider -- see hs_slider.h for the full design rationale,
		 * including the "BAPPLICATION-AT-CONSTRUCTION: CONFIRMED
		 * REQUIRED" note on hs_slider_create (matching TextControl/
		 * RadioButton, not CheckBox), and the "ONE NATIVE CONSTRUCTOR
		 * FUNCTION, NOT TWO" note explaining why there is only one
		 * create function despite real BSlider having three frame-based
		 * constructor overloads. Label/Value/IsEnabled are NOT declared
		 * here either -- same shared hs_control_* block used by every
		 * other control's handle. */
		[DllImport(Lib)]
		internal static extern IntPtr hs_slider_create(HsRect frame, string name,
			string label, int minValue, int maxValue, uint orientation,
			uint thumbStyle, uint resizingMode, uint flags);

		[DllImport(Lib)]
		internal static extern void hs_slider_destroy(IntPtr slider);

		[DllImport(Lib)]
		internal static extern void hs_slider_set_value_changed_callback(IntPtr slider,
			SliderValueChangedCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_slider_set_value_committed_callback(IntPtr slider,
			SliderValueCommittedCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_slider_set_destroyed_callback(IntPtr slider,
			SliderDestroyedCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_slider_set_limits(IntPtr slider, int minimum, int maximum);

		[DllImport(Lib)]
		internal static extern void hs_slider_get_limits(IntPtr slider, out int minimum, out int maximum);

		[DllImport(Lib)]
		internal static extern void hs_slider_set_position(IntPtr slider, float position);

		[DllImport(Lib)]
		internal static extern float hs_slider_position(IntPtr slider);

		[DllImport(Lib)]
		internal static extern uint hs_slider_orientation(IntPtr slider);

		[DllImport(Lib)]
		internal static extern void hs_slider_set_orientation(IntPtr slider, uint orientation);

		[DllImport(Lib)]
		internal static extern uint hs_slider_style(IntPtr slider);

		[DllImport(Lib)]
		internal static extern void hs_slider_set_style(IntPtr slider, uint style);

		[DllImport(Lib)]
		internal static extern void hs_slider_set_limit_labels(IntPtr slider,
			string minLabel, string maxLabel);

		/* Both return a pointer into BSlider's own internal storage, same
		 * borrowed-pointer situation as hs_text_control_text above --
		 * Slider.cs's MinLimitLabel/MaxLimitLabel getters copy them into
		 * managed strings immediately via Marshal.PtrToStringAnsi. */
		[DllImport(Lib)]
		internal static extern IntPtr hs_slider_min_limit_label(IntPtr slider);

		[DllImport(Lib)]
		internal static extern IntPtr hs_slider_max_limit_label(IntPtr slider);

		[DllImport(Lib)]
		internal static extern int hs_slider_key_increment_value(IntPtr slider);

		[DllImport(Lib)]
		internal static extern void hs_slider_set_key_increment_value(IntPtr slider, int value);

		/* Completeness pass additions -- see hs_slider.h's own updated
		 * SCOPE note for the hardware-verified defaults and the two
		 * genuine surprises documented there (BarThickness's rounding,
		 * and hs_slider_fill_color()'s unreliable value after a
		 * disabling hs_slider_use_fill_color(false, ...) call). */
		[DllImport(Lib)]
		internal static extern void hs_slider_set_snooze_amount(IntPtr slider, int microseconds);

		[DllImport(Lib)]
		internal static extern int hs_slider_snooze_amount(IntPtr slider);

		[DllImport(Lib)]
		internal static extern void hs_slider_set_hash_mark_count(IntPtr slider, int count);

		[DllImport(Lib)]
		internal static extern int hs_slider_hash_mark_count(IntPtr slider);

		[DllImport(Lib)]
		internal static extern void hs_slider_set_hash_marks(IntPtr slider, uint where);

		[DllImport(Lib)]
		internal static extern uint hs_slider_hash_marks(IntPtr slider);

		[DllImport(Lib)]
		internal static extern void hs_slider_set_bar_color(IntPtr slider,
			byte red, byte green, byte blue, byte alpha);

		[DllImport(Lib)]
		internal static extern void hs_slider_bar_color(IntPtr slider,
			out byte outRed, out byte outGreen, out byte outBlue, out byte outAlpha);

		[DllImport(Lib)]
		internal static extern void hs_slider_use_fill_color(IntPtr slider,
			[MarshalAs(UnmanagedType.I1)] bool useFill,
			byte red, byte green, byte blue, byte alpha);

		[DllImport(Lib)]
		[return: MarshalAs(UnmanagedType.I1)]
		internal static extern bool hs_slider_uses_fill_color(IntPtr slider);

		[DllImport(Lib)]
		internal static extern void hs_slider_fill_color(IntPtr slider,
			out byte outRed, out byte outGreen, out byte outBlue, out byte outAlpha);

		[DllImport(Lib)]
		internal static extern void hs_slider_set_bar_thickness(IntPtr slider, float thickness);

		[DllImport(Lib)]
		internal static extern float hs_slider_bar_thickness(IntPtr slider);

		/* Raw P/Invoke surface for hs_color_control.h. ColorControl.cs
		 * wraps every one of these in a safe, idiomatic method/property. */
		[DllImport(Lib)]
		internal static extern IntPtr hs_color_control_create(HsPoint start, uint layout,
			float cellSize, string name, [MarshalAs(UnmanagedType.I1)] bool useOffscreen);

		[DllImport(Lib)]
		internal static extern void hs_color_control_destroy(IntPtr colorControl);

		[DllImport(Lib)]
		internal static extern void hs_color_control_set_value_changed_callback(IntPtr colorControl,
			ColorControlValueChangedCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_color_control_set_destroyed_callback(IntPtr colorControl,
			ColorControlDestroyedCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_color_control_set_value_color(IntPtr colorControl,
			byte red, byte green, byte blue, byte alpha);

		[DllImport(Lib)]
		internal static extern void hs_color_control_value_as_color(IntPtr colorControl,
			out byte outRed, out byte outGreen, out byte outBlue, out byte outAlpha);

		[DllImport(Lib)]
		internal static extern void hs_color_control_set_cell_size(IntPtr colorControl, float size);

		[DllImport(Lib)]
		internal static extern float hs_color_control_cell_size(IntPtr colorControl);

		[DllImport(Lib)]
		internal static extern void hs_color_control_set_layout(IntPtr colorControl, uint layout);

		[DllImport(Lib)]
		internal static extern uint hs_color_control_layout(IntPtr colorControl);
	}
}
