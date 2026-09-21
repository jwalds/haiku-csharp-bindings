/*
 * hs_text_control.h -- C shim over BTextControl
 * (headers/os/interface/TextControl.h), layered on top of BControl
 * (headers/os/interface/Control.h).
 *
 * Same trampoline shape as hs_button.h -- read that file first if you
 * haven't, especially its "NO BMessage/BInvoker/TARGET PLUMBING" note,
 * which this file follows for OnTextCommitted below. This comment covers
 * only what's actually different about BTextControl.
 *
 * BTextControl OWNS A PRIVATE BTextView -- WE DON'T SUBCLASS IT
 * --------------------------------------------------------------------------
 * Unlike BButton (whose click behavior is entirely its own), BTextControl
 * internally creates and owns a private BPrivate::_BTextInput_ (a BTextView
 * subclass this binding cannot see the declaration of, let alone subclass)
 * to actually hold and edit the text, reachable read-only via the public
 * `BTextView* TextView() const` accessor. This binding does not wrap that
 * accessor -- everything below goes through BTextControl's own public API
 * (SetText/Text, SetModificationMessage/Invoke) instead of reaching into
 * the child BTextView directly.
 *
 * CRITICAL: BTextControl CONSTRUCTION HANGS WITHOUT A LIVE BApplication
 * --------------------------------------------------------------------------
 * Verified empirically on real Haiku hardware, not assumed: constructing a
 * BTextControl (and therefore an HSTextControl) with no BApplication object
 * yet constructed in the process blocks forever -- likely an app_server
 * round-trip for font metrics needed to lay out the initial text/label/
 * divider, which never returns without a live app_server connection (that
 * connection is established at BApplication construction, not Run()).
 * This is a genuine asymmetry from BView/BButton, both of which construct
 * fine with zero BApplication anywhere in the process. Practical
 * consequence: hs_text_control_create() must only ever be called after a
 * BApplication has been constructed (Run() need not have been called) --
 * see TextControlTests.cs, where every single test, including pure
 * construction/geometry ones, wraps its body in `using (new
 * Application(AppSignature))`, unlike ViewTests'/ButtonTests' construction-
 * only tests, which deliberately use no Application at all.
 *
 * TWO DIFFERENT "CHANGED" EVENTS, BOTH WITHOUT EXPOSING BMessage/BInvoker
 * --------------------------------------------------------------------------
 * Real BeAPI gives BTextControl two distinct notifications, both normally
 * delivered as a BMessage to a BInvoker target -- neither of which this
 * binding exposes as BMessage/BInvoker plumbing to managed code, matching
 * the Button slice's scope decision:
 *
 *   - A "modification" message (SetModificationMessage()/
 *     ModificationMessage()), which the Be Book documents as firing
 *     "whenever the user modifies the text" while the child BTextView has
 *     focus -- i.e. on every keystroke that changes the text. HSTextControl
 *     sends itself a private, internal-only BMessage for this (a `what`
 *     constant never declared here or anywhere managed code can see) and
 *     intercepts it in an overridden MessageReceived(), firing
 *     hs_text_control_text_changed_callback directly instead of leaving it
 *     as a message another Handler would have to go pick up.
 *
 *   - The control's own Invoke() (inherited from BInvoker via BControl),
 *     which the Be Book documents as firing "when the text changes after
 *     focus is lost from the BTextView" -- i.e. on commit: Enter, or
 *     focus-out after an edit. HSTextControl overrides Invoke() exactly
 *     like HSButton::Invoke() (see hs_button.h) to fire
 *     hs_text_control_text_committed_callback directly and return B_OK,
 *     never posting anywhere.
 *
 * SetTarget(this) IS CALLED TWICE: CONSTRUCTOR, THEN AttachedToWindow()
 * --------------------------------------------------------------------------
 * BInvoker's SetTarget() captures a BMessenger pointing at the given
 * BHandler on the given (or inferred) BLooper. Called from the
 * constructor, before this control is attached to any BWindow, there is
 * no BLooper yet to infer -- so HSTextControl calls SetTarget(this) again
 * from an overridden AttachedToWindow(), after chaining up to
 * BTextControl::AttachedToWindow() (which must still run -- unlike
 * HSButton, this binding does NOT override away BTextControl's real
 * AttachedToWindow() behavior, since it does real layout work). The
 * constructor's own SetTarget(this) call is kept too, harmlessly
 * redundant when the control is later attached, but occasionally
 * meaningful if something calls Invoke() on a still-unattached control
 * directly (defensive, not load-bearing for the normal attached case).
 *
 * BCONTROL-LEVEL STATE (Label/Value/IsEnabled): SHARED WITH BUTTON
 * --------------------------------------------------------------------------
 * Not declared here -- see hs_control.h, shared with HSButton and any
 * other concrete control this binding wraps. Call
 * hs_control_set_label()/hs_control_label()/etc. directly against a text
 * control handle; verified safe by a native ABI probe on real hardware
 * confirming BView/BControl/BTextControl all sit at offset 0 from an
 * HSTextControl*, the same fact hs_control.h's own header comment
 * documents for the whole shared-control family.
 *
 * SCOPE: TEXT/SetText ONLY, NOT Divider/Alignment
 * --------------------------------------------------------------------------
 * This slice deliberately stops at Text/SetText plus the shared Label/
 * Value/IsEnabled -- SetDivider()/Divider() (the label/field split
 * position) and SetAlignment() are real BTextControl API this binding
 * does not expose yet, a scope decision made up front, not an oversight.
 *
 * CONSTRUCTION SILENTLY OVERRIDES THE REQUESTED HEIGHT, NEVER THE WIDTH
 * --------------------------------------------------------------------------
 * Verified empirically on real Haiku hardware, not assumed: a freshly
 * constructed BTextControl's Frame() always comes back with its own
 * fixed, font-derived height -- 24px for the default plain font on the
 * hardware this was checked on -- REGARDLESS of the height given in the
 * frame passed to the constructor. A small scratch probe constructed
 * three BTextControls with requested heights of 10, 30, and 100 (nothing
 * else different); all three came back with height exactly 24. The
 * requested width (left/right) is honored exactly, only the height is
 * overridden. This is construction-time-only behavior, not a standing
 * clamp: the same probe then called plain ResizeTo() on one of them
 * (250, 30) and (250, 5) afterward, and both were honored exactly --
 * hs_view_resize_to() (reused for a text control's own handle, same as
 * for a button's) is NOT overridden or clamped the way construction is.
 * Practical consequence: callers who need a specific height should
 * ResizeTo() it after construction, not rely on the constructor's frame
 * argument -- and TextControlTests.cs's construction/geometry test
 * checks Left/Top/width against what was given but does NOT assert an
 * exact height straight out of the constructor, for exactly this reason.
 */
#ifndef HS_TEXT_CONTROL_H
#define HS_TEXT_CONTROL_H

#include "hs_types.h"

#ifdef __cplusplus
extern "C" {
#endif

typedef void (*hs_text_control_text_changed_callback)(void* user_data);
typedef void (*hs_text_control_text_committed_callback)(void* user_data);
typedef void (*hs_text_control_destroyed_callback)(void* user_data);

/* Create a new HSTextControl (a BTextControl subclass) with the given
 * frame, name, label, initial text, resizing mode, and flags -- same
 * shape as hs_button_create(). MUST NOT be called before a BApplication
 * has been constructed in this process -- see this header's "CRITICAL"
 * note above; doing so hangs forever, it does not fail fast. Has no
 * parent and is not attached to any window yet; safe to configure from
 * whatever thread created it, same threading rule as hs_view.h's. */
hs_handle hs_text_control_create(hs_rect frame, const char* name,
	const char* label, const char* text, uint32_t resizing_mode,
	uint32_t flags);

/* ONLY safe on a text control that is not currently attached to a parent
 * -- same OWNERSHIP rule as hs_button_destroy()/hs_view_destroy(). Fires
 * the destroyed callback, same as any other path to this control's
 * destruction. */
void hs_text_control_destroy(hs_handle text_control);

void hs_text_control_set_text_changed_callback(hs_handle text_control,
	hs_text_control_text_changed_callback callback, void* user_data);

void hs_text_control_set_text_committed_callback(hs_handle text_control,
	hs_text_control_text_committed_callback callback, void* user_data);

void hs_text_control_set_destroyed_callback(hs_handle text_control,
	hs_text_control_destroyed_callback callback, void* user_data);

void hs_text_control_set_text(hs_handle text_control, const char* text);

/* Returns a pointer into BTextControl's own internal storage, same
 * borrowed-pointer situation as hs_control_label()/hs_window_title() --
 * copy it into a managed string immediately, do not hold onto it or
 * free it. */
const char* hs_text_control_text(hs_handle text_control);

#ifdef __cplusplus
}
#endif

#endif /* HS_TEXT_CONTROL_H */
