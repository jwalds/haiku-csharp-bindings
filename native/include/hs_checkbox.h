/*
 * hs_checkbox.h -- C shim over BCheckBox (headers/os/interface/CheckBox.h),
 * layered on top of BControl (headers/os/interface/Control.h).
 *
 * Same trampoline shape as hs_button.h -- read that file first if you
 * haven't. This comment covers only what's actually different about
 * BCheckBox, which turns out to be very little.
 *
 * NO BMessage/BInvoker/TARGET PLUMBING -- SAME DIRECT CLICK CALLBACK AS
 * BUTTON, AND SAFE TO REUSE FOR THE SAME REASON
 * --------------------------------------------------------------------------
 * HSCheckBox overrides Invoke() exactly the way HSButton does (see
 * hs_button.h's own note on this -- identical reasoning, not repeated
 * here): no BMessage is ever posted anywhere, hs_checkbox_create() always
 * constructs the underlying BCheckBox with a NULL BMessage*, and a plain
 * callback fires directly and synchronously on whatever thread Invoke()
 * itself runs on.
 *
 * ONE THING WORTH CALLING OUT: BCheckBox's own MouseUp()/KeyDown() (Space/
 * Enter) toggle Value() themselves, BEFORE calling Invoke() -- verified
 * against the real Haiku.org API docs for BCheckBox, not just assumed. That
 * means by the time this binding's click callback fires, Control.Value/
 * IsChecked already reflects the NEW state; there is no "before" value to
 * read separately, and no need to toggle anything from managed code. This
 * is also why HSCheckBox, like HSButton, does not override MouseDown/
 * MouseUp/KeyDown/KeyUp -- BCheckBox's own real implementations already do
 * exactly the right thing untouched.
 *
 * BAPPLICATION-AT-CONSTRUCTION: CONFIRMED **NOT** REQUIRED (verified on
 * real hardware, unlike BTextControl/BRadioButton -- see hs_radio_button.h)
 * --------------------------------------------------------------------------
 * Unlike BTextControl (see hs_text_control.h) and BRadioButton (see
 * hs_radio_button.h), constructing a BCheckBox with no live BApplication
 * anywhere in the process does NOT hang -- confirmed with a flush-per-step
 * native probe on real Haiku hardware: a BCheckBox was constructed and
 * deleted cleanly before any BApplication existed, matching BButton's and
 * BView's own behavior. This was checked empirically rather than assumed
 * from BCheckBox's structural similarity to BRadioButton (single,
 * BControl-derived inheritance, no owned sub-BTextView the way
 * BTextControl has) -- that similarity turned out NOT to predict this
 * behavior, since BRadioButton, despite looking just as simple, DOES hang
 * the same way BTextControl does. The root cause of that asymmetry is not
 * understood (see hs_radio_button.h).
 *
 * CAVEAT FOUND WHILE WIRING UP CheckBoxTests.cs: this "no BApplication
 * needed" guarantee holds only in a process where NO BApplication has
 * EVER existed yet -- not merely "none currently live." Running
 * Tests.exe's full suite (ButtonTests first, which constructs and
 * disposes several never-Run() Applications of its own, then
 * CheckBoxTests) reproducibly hung on CheckBoxTests' very first,
 * construction-only test, even though that identical test passes
 * instantly when CheckBoxTests is run alone. So CheckBoxTests.cs wraps
 * every test in `using (new Application(...))` anyway, same as
 * TextControlTests.cs/RadioButtonTests.cs -- not because construction
 * alone needs it in a vacuum, but because this suite can't guarantee
 * CheckBoxTests runs before anything else that touches Application. This
 * native probe's own "clean, no-BApplication-ever" finding above is still
 * accurate for a truly fresh process (e.g. a real app's very first
 * BCheckBox); it just isn't safe to rely on inside a longer-lived
 * process, like this test suite's, that may have already spun up and
 * torn down a BApplication earlier.
 *
 * REUSING hs_view_get_frame()/move_to()/resize_to() AND hs_control.h
 * --------------------------------------------------------------------------
 * Same ABI reasoning as hs_button.h: HSCheckBox's BView subobject sits at
 * offset 0 (BCheckBox : public BControl : public BView, public BInvoker --
 * single, non-virtual inheritance, no different in shape from BButton or
 * BRadioButton), so Geometry.cs-style calls go through the existing
 * hs_view_* functions directly, and Label/Value/IsEnabled are NOT declared
 * here -- reuse hs_control_set_label()/hs_control_label()/hs_control_
 * set_value()/hs_control_value()/hs_control_set_enabled()/hs_control_
 * is_enabled() exactly like Button and TextControl already do.
 */
#ifndef HS_CHECKBOX_H
#define HS_CHECKBOX_H

#include "hs_types.h"

#ifdef __cplusplus
extern "C" {
#endif

typedef void (*hs_checkbox_click_callback)(void* user_data);
typedef void (*hs_checkbox_destroyed_callback)(void* user_data);

/* Create a new HSCheckBox (a BCheckBox subclass) with the given frame,
 * name, label, resizing mode, and flags -- same shape as
 * hs_button_create(). Has no parent and is not attached to any window
 * yet; safe to construct with no live BApplication (see the note above),
 * unlike hs_radio_button_create(). */
hs_handle hs_checkbox_create(hs_rect frame, const char* name, const char* label,
	uint32_t resizing_mode, uint32_t flags);

/* ONLY safe on a checkbox that is not currently attached to a parent --
 * same OWNERSHIP rule as hs_view_destroy()/hs_button_destroy(). Fires the
 * destroyed callback, same as any other path to this checkbox's
 * destruction. */
void hs_checkbox_destroy(hs_handle checkbox);

/* Fires after BCheckBox's own MouseUp()/KeyDown() has already toggled
 * Value() -- see the note above. Read hs_control_value()/hs_control_
 * is_enabled() from inside the callback to see the new state; there is
 * nothing else to pass. */
void hs_checkbox_set_click_callback(hs_handle checkbox,
	hs_checkbox_click_callback callback, void* user_data);

void hs_checkbox_set_destroyed_callback(hs_handle checkbox,
	hs_checkbox_destroyed_callback callback, void* user_data);

/* BControl-level state (Label/Value/IsEnabled) is NOT declared here --
 * see hs_control.h, shared with every other concrete control this
 * binding wraps. Call hs_control_set_label()/hs_control_value()/etc.
 * directly against a checkbox handle; safe, see hs_control.h's own
 * rationale for why. BCheckBox has no state of its own beyond that --
 * unlike BButton (IsDefault/IsFlat/Behavior), there is nothing
 * checkbox-specific to declare here at all. */

#ifdef __cplusplus
}
#endif

#endif /* HS_CHECKBOX_H */
