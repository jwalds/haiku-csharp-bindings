/*
 * hs_radio_button.h -- C shim over BRadioButton
 * (headers/os/interface/RadioButton.h), layered on top of BControl
 * (headers/os/interface/Control.h).
 *
 * Same trampoline shape as hs_checkbox.h -- read that file first (and
 * hs_button.h before that, if this is your first kit file). This comment
 * covers only what's genuinely different about BRadioButton: a
 * construction-time BApplication requirement it turns out NOT to share
 * with BCheckBox despite otherwise near-identical structure, and
 * automatic mutual-exclusivity grouping that needs zero code here at all.
 *
 * NO BMessage/BInvoker/TARGET PLUMBING -- SAME AS CHECKBOX/BUTTON
 * --------------------------------------------------------------------------
 * HSRadioButton overrides Invoke() exactly the way HSCheckBox/HSButton do
 * -- identical reasoning, not repeated here. BRadioButton's own MouseUp()/
 * KeyDown() (Space/Enter) toggle Value() (and deactivate sibling radio
 * buttons -- see the grouping note below) BEFORE calling Invoke(), so the
 * callback sees the fully-settled new state, same as BCheckBox.
 *
 * BAPPLICATION-AT-CONSTRUCTION: CONFIRMED **REQUIRED**, UNLIKE BCHECKBOX --
 * A GENUINE, UNEXPLAINED ASYMMETRY
 * --------------------------------------------------------------------------
 * Constructing a BRadioButton before any BApplication exists in the
 * process HANGS INDEFINITELY -- confirmed on real Haiku hardware with a
 * flush-per-step native probe, and then re-confirmed with a SECOND,
 * fully isolated probe that constructed ONLY a BRadioButton (no preceding
 * BCheckBox construct/delete anywhere in the same process), to rule out
 * any same-process ordering artifact. Both runs hung at the exact same
 * point -- immediately upon entering `new BRadioButton(...)`, never
 * reaching the line after it -- and had to be killed with SIGKILL.
 *
 * This matches BTextControl's own construction-time BApplication
 * requirement (see hs_text_control.h) but is a genuine surprise given
 * BRadioButton's structure: it is, like BCheckBox, a simple, single-
 * inheritance BControl subclass with just a label and no owned
 * sub-BTextView or font-metrics-driven layout step that would obviously
 * explain a dependency on live app_server/font-server state. BCheckBox,
 * despite looking almost identical in shape, was separately confirmed
 * (see hs_checkbox.h) to construct and delete cleanly with NO
 * BApplication at all. The root cause of this asymmetry between two
 * structurally near-identical widgets is NOT understood -- this is
 * recorded as a verified hardware fact to design around, not a solved
 * mystery, per this project's standing rule to verify rather than assume
 * and to document surprises even when unexplained.
 *
 * Practical consequence: like TextControlTests.cs, every single test in
 * RadioButtonTests.cs -- including pure construction/geometry ones --
 * must wrap its body in `using (new Application(...))`, unlike
 * ButtonTests.cs's and CheckBoxTests.cs's construction-only tests, which
 * need no Application at all.
 *
 * AUTOMATIC GROUPING: FULLY HANDLED BY REAL BEAPI, ZERO CODE HERE --
 * CONFIRMED ON HARDWARE, INCLUDING A CROSS-THREAD CAVEAT
 * --------------------------------------------------------------------------
 * Per BRadioButton's own real API docs: "Radio buttons, unlike check
 * boxes, are always used as part of a group. Only one radio button in a
 * group can be on at a time, when one is turned on all sibling radio
 * buttons are turned off," and grouping is based purely on sharing the
 * same parent View -- "each group must be attached to a different
 * parent." Since this binding's existing hs_view_add_child()/
 * hs_view_remove_child() already just delegate to native BView parenting
 * (see hs_view.h), grouping needs no dedicated API on this binding's side
 * at all -- no "RadioGroup" handle, no explicit "add to group" call.
 *
 * Verified on real Haiku hardware, not just trusted from the docs: three
 * BRadioButtons added as children of one shared BView (itself NOT added
 * to any Window), with hs_control_set_value()/BRadioButton::SetValue()
 * driving the state directly (see RadioButtonTests.cs) -- turning one on
 * automatically and immediately turned the previously-on sibling off,
 * with no BWindow, no Show(), and no BApplication::Run() involved at all
 * beyond the live BApplication construction already requires. This is
 * the same pattern RadioButtonTests.cs's own grouping regression test
 * uses, and it is fast and hang-free.
 *
 * CAVEAT, found while isolating the above: calling SetValue() (or
 * presumably Invoke()) on a radio button that IS a child of a Window that
 * has been Show()n, from a thread other than the one that would run
 * BApplication::Run() (which this binding's tests never call -- see
 * ApplicationTests.cs's one-shot-per-process caveat), can hang -- verified
 * with a pure native, non-Mono probe, so this is not specific to this
 * binding's Mono embedding layer. This is the SAME underlying hazard
 * KNOWN_ISSUES.md issue #4 already documents for Draw() (root-caused
 * there to "Show() from a thread other than the one running
 * Application.Run(), with Application.Run() never called at all"), now
 * confirmed to extend to at least one more operation (SetValue()) and to
 * be reproducible with zero managed code involved. It is NOT a new
 * restriction -- ViewTests.cs's/WindowTests' existing convention of never
 * calling Show() on a test window already avoids it -- but it is the
 * reason RadioButtonTests.cs's grouping test deliberately uses an
 * unshown parent View rather than a real, shown Window.
 *
 * REUSING hs_view_get_frame()/move_to()/resize_to() AND hs_control.h
 * --------------------------------------------------------------------------
 * Same ABI reasoning as hs_button.h/hs_checkbox.h: HSRadioButton's BView
 * subobject sits at offset 0 -- verified directly (not just inferred from
 * BCheckBox's own separately-verified offset) with a native probe
 * static_cast-ing a live BRadioButton* to BView* and to BControl* inside a
 * running BApplication and confirming an identical pointer value in both
 * cases. Geometry.cs-style calls go through the existing hs_view_*
 * functions directly, and Label/Value/IsEnabled are NOT declared here --
 * reuse hs_control_set_label()/hs_control_label()/hs_control_set_value()/
 * hs_control_value()/hs_control_set_enabled()/hs_control_is_enabled()
 * exactly like Button/TextControl/CheckBox already do.
 */
#ifndef HS_RADIO_BUTTON_H
#define HS_RADIO_BUTTON_H

#include "hs_types.h"

#ifdef __cplusplus
extern "C" {
#endif

typedef void (*hs_radio_button_click_callback)(void* user_data);
typedef void (*hs_radio_button_destroyed_callback)(void* user_data);

/* Create a new HSRadioButton (a BRadioButton subclass) with the given
 * frame, name, label, resizing mode, and flags -- same shape as
 * hs_checkbox_create(). Has no parent and is not attached to any window
 * yet.
 *
 * REQUIRES A LIVE BApplication TO ALREADY EXIST IN THIS PROCESS -- see
 * the note above. Calling this with no BApplication constructed yet
 * hangs indefinitely; this is verified real BeAPI behavior on this
 * hardware, not a bug in this shim. */
hs_handle hs_radio_button_create(hs_rect frame, const char* name, const char* label,
	uint32_t resizing_mode, uint32_t flags);

/* ONLY safe on a radio button that is not currently attached to a parent
 * -- same OWNERSHIP rule as hs_view_destroy()/hs_button_destroy(). Fires
 * the destroyed callback, same as any other path to this radio button's
 * destruction. */
void hs_radio_button_destroy(hs_handle radio_button);

/* Fires after BRadioButton's own MouseUp()/KeyDown() has already toggled
 * Value() and deactivated any sibling radio buttons under the same
 * parent View -- see the grouping note above. Read hs_control_value()
 * from inside the callback to see the new state. */
void hs_radio_button_set_click_callback(hs_handle radio_button,
	hs_radio_button_click_callback callback, void* user_data);

void hs_radio_button_set_destroyed_callback(hs_handle radio_button,
	hs_radio_button_destroyed_callback callback, void* user_data);

/* BControl-level state (Label/Value/IsEnabled) is NOT declared here --
 * see hs_control.h, shared with every other concrete control this
 * binding wraps. Call hs_control_set_label()/hs_control_value()/etc.
 * directly against a radio button handle; safe, see hs_control.h's own
 * rationale for why. There is no dedicated grouping API -- see the note
 * above; grouping is an emergent property of hs_view_add_child(), not
 * something this file declares anything for. */

#ifdef __cplusplus
}
#endif

#endif /* HS_RADIO_BUTTON_H */
