/*
 * hs_slider.h -- C shim over BSlider (headers/os/interface/Slider.h),
 * layered on top of BControl (headers/os/interface/Control.h).
 *
 * Same trampoline shape as hs_text_control.h -- read that file first if
 * you haven't (and hs_button.h before that, if this is your first kit
 * file). BSlider turns out to need almost exactly HSTextControl's own
 * two-event design, for the same underlying reason: real BSlider, like
 * real BTextControl, delivers two genuinely different notifications, one
 * repeated and one one-shot, both normally via BMessage/BInvoker
 * plumbing this binding replaces with direct callbacks.
 *
 * TWO DIFFERENT "CHANGED" EVENTS, SAME SHAPE AS TextControl
 * --------------------------------------------------------------------------
 * Real BSlider's own docs: a modification message is "sent repeatedly as
 * long as the mouse button is held down" (dragging the thumb), while the
 * model message passed to the constructor -- delivered via the inherited
 * Invoke() -- fires once, when the mouse button is released. This is
 * structurally identical to BTextControl's modification-message-vs-
 * Invoke() split (see hs_text_control.h's "TWO DIFFERENT 'CHANGED'
 * EVENTS" note), so HSSlider reuses the exact same mechanism: it sends
 * itself a private, internal-only BMessage (SetModificationMessage) for
 * every drag tick, intercepted in an overridden MessageReceived() to
 * fire OnValueChanged directly; Invoke() itself is overridden (matching
 * every other control in this binding) to fire OnValueCommitted directly
 * once, on release, instead of posting anywhere. hs_slider_create()
 * always constructs the underlying BSlider with a NULL model BMessage*,
 * same reasoning as every other control here.
 *
 * SetTarget(this) IS CALLED TWICE, SAME REASON AS TextControl
 * --------------------------------------------------------------------------
 * BInvoker::SetTarget() captures a BMessenger pointing at the given
 * target on the given (or inferred) BLooper. Called from the
 * constructor, before the control is attached to any window, there is
 * no BLooper yet to infer -- so HSSlider, like HSTextControl, also
 * overrides AttachedToWindow(), chains up to BSlider::AttachedToWindow()
 * (which does real work and must still run), and calls SetTarget(this)
 * again afterward once a valid Looper actually exists. This is not
 * something this project re-verified per-widget with its own probe --
 * it follows directly from the BMessenger/Looper timing fact
 * hs_text_control.h already established empirically, which applies to
 * any BInvoker-derived control constructed before attachment, not
 * something specific to BTextControl.
 *
 * ONE NATIVE CONSTRUCTOR FUNCTION, NOT TWO -- VERIFIED EQUIVALENT ON
 * HARDWARE
 * --------------------------------------------------------------------------
 * Real BSlider has two frame-based constructors: one without an
 * `orientation` parameter (implicitly horizontal) and one with it. A
 * native probe on real Haiku hardware confirmed the no-orientation
 * constructor's result reports Orientation() == B_HORIZONTAL (0) --
 * identical to explicitly passing B_HORIZONTAL to the orientation-taking
 * constructor -- so hs_slider_create() below always takes an explicit
 * `orientation` argument and always uses the orientation-taking real
 * BSlider constructor; nothing is lost by not having a second native
 * entry point. Slider.cs's own convenience constructor supplies
 * SliderOrientation.Horizontal by default, matching this project's
 * existing "convenience overload fills in the common default" pattern
 * (see Button.cs's own two-constructor shape).
 *
 * BAPPLICATION-AT-CONSTRUCTION: CONFIRMED **REQUIRED**, MATCHING
 * BTextControl/BRadioButton, NOT BCheckBox
 * --------------------------------------------------------------------------
 * Constructing a BSlider before any BApplication exists in the process
 * HANGS INDEFINITELY -- confirmed on real Haiku hardware with a flush-
 * per-step native probe, isolated (no other widget constructed earlier
 * in the same process, ruling out the kind of ordering artifact found
 * while verifying BRadioButton -- see hs_radio_button.h). This was
 * checked empirically rather than assumed from BSlider's structural
 * similarity to BCheckBox/BRadioButton -- and per those two widgets'
 * own now-documented asymmetry, structural similarity alone does not
 * predict this behavior either way. Practical consequence: every single
 * test in SliderTests.cs, including pure construction/geometry ones,
 * wraps its body in `using (new Application(...))`, matching
 * TextControlTests.cs/RadioButtonTests.cs, not CheckBoxTests.cs.
 *
 * REUSING hs_view_get_frame()/move_to()/resize_to() AND hs_control.h
 * --------------------------------------------------------------------------
 * Same ABI reasoning as every other control here: HSSlider's BView
 * subobject sits at offset 0 -- verified directly with a native probe
 * static_cast-ing a live BSlider* to BView* and to BControl* inside a running
 * BApplication and confirming an identical pointer value in both cases.
 * Geometry.cs-style calls go through the existing hs_view_* functions
 * directly, and Label/Value/IsEnabled are NOT declared here -- reuse
 * hs_control_set_label()/hs_control_label()/hs_control_set_value()/
 * hs_control_value()/hs_control_set_enabled()/hs_control_is_enabled()
 * exactly like every other control in this binding.
 *
 * SCOPE: not every BSlider feature is exposed here. Hash marks/tick
 * marks (SetHashMarks/SetHashMarkCount), bar/fill colors, a custom icon
 * (SetIcon), the snooze amount, and UpdateText()'s status-text override
 * are all deferred to a follow-up slice -- see README.md's "Not yet
 * covered" list. What IS exposed: Position, GetLimits/SetLimits,
 * Orientation, Style (thumb shape), SetLimitLabels/MinLimitLabel/
 * MaxLimitLabel, and KeyIncrementValue -- all simple, cheap property
 * pairs, similar in spirit to Button's IsDefault/IsFlat/Behavior
 * additions beyond raw Control state.
 */
#ifndef HS_SLIDER_H
#define HS_SLIDER_H

#include "hs_types.h"

#ifdef __cplusplus
extern "C" {
#endif

typedef void (*hs_slider_value_changed_callback)(void* user_data);
typedef void (*hs_slider_value_committed_callback)(void* user_data);
typedef void (*hs_slider_destroyed_callback)(void* user_data);

/* Create a new HSSlider (a BSlider subclass) with the given frame, name,
 * label, value range, orientation, and thumb style -- same shape as
 * hs_text_control_create(). `orientation` is the raw BeAPI `orientation`
 * value (B_HORIZONTAL=0, B_VERTICAL=1); `thumb_style` is the raw
 * `thumb_style` value (B_BLOCK_THUMB=0, B_TRIANGLE_THUMB=1) -- both
 * verified against the actual installed Slider.h/InterfaceDefs.h, not
 * assumed. Has no parent and is not attached to any window yet.
 *
 * REQUIRES A LIVE BApplication TO ALREADY EXIST IN THIS PROCESS -- see
 * the note above. Calling this with no BApplication constructed yet
 * hangs indefinitely; this is verified real BeAPI behavior on this
 * hardware, not a bug in this shim. */
hs_handle hs_slider_create(hs_rect frame, const char* name, const char* label,
	int32_t min_value, int32_t max_value, uint32_t orientation,
	uint32_t thumb_style, uint32_t resizing_mode, uint32_t flags);

/* ONLY safe on a slider that is not currently attached to a parent --
 * same OWNERSHIP rule as hs_view_destroy()/hs_text_control_destroy().
 * Fires the destroyed callback, same as any other path to this slider's
 * destruction. */
void hs_slider_destroy(hs_handle slider);

/* Fires repeatedly while the thumb is being dragged -- see the note
 * above. */
void hs_slider_set_value_changed_callback(hs_handle slider,
	hs_slider_value_changed_callback callback, void* user_data);

/* Fires once, when the mouse button is released -- see the note above. */
void hs_slider_set_value_committed_callback(hs_handle slider,
	hs_slider_value_committed_callback callback, void* user_data);

void hs_slider_set_destroyed_callback(hs_handle slider,
	hs_slider_destroyed_callback callback, void* user_data);

/* BControl-level state (Label/Value/IsEnabled) is NOT declared here --
 * see hs_control.h, shared with every other concrete control this
 * binding wraps. Call hs_control_set_label()/hs_control_value()/etc.
 * directly against a slider handle; safe, see hs_control.h's own
 * rationale for why.
 *
 * BSlider-specific state. */
void hs_slider_set_limits(hs_handle slider, int32_t minimum, int32_t maximum);
void hs_slider_get_limits(hs_handle slider, int32_t* out_minimum, int32_t* out_maximum);

void hs_slider_set_position(hs_handle slider, float position);
float hs_slider_position(hs_handle slider);

uint32_t hs_slider_orientation(hs_handle slider);
void hs_slider_set_orientation(hs_handle slider, uint32_t orientation);

uint32_t hs_slider_style(hs_handle slider);
void hs_slider_set_style(hs_handle slider, uint32_t style);

void hs_slider_set_limit_labels(hs_handle slider, const char* min_label,
	const char* max_label);
/* Both return borrowed pointers into BSlider's own internal storage,
 * same treatment as hs_control_label() -- Slider.cs copies them into
 * managed strings immediately via Marshal.PtrToStringAnsi. */
const char* hs_slider_min_limit_label(hs_handle slider);
const char* hs_slider_max_limit_label(hs_handle slider);

void hs_slider_set_key_increment_value(hs_handle slider, int32_t value);
int32_t hs_slider_key_increment_value(hs_handle slider);

#ifdef __cplusplus
}
#endif

#endif /* HS_SLIDER_H */
