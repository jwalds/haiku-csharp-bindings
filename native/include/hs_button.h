/*
 * hs_button.h -- C shim over BButton (headers/os/interface/Button.h),
 * layered on top of BControl (headers/os/interface/Control.h).
 *
 * Same trampoline shape as hs_view.h/hs_window.h -- read hs_view.h first
 * if you haven't. This comment covers only what's actually different
 * about BButton/BControl.
 *
 * NO BMessage/BInvoker/TARGET PLUMBING -- A DIRECT CLICK CALLBACK INSTEAD
 * --------------------------------------------------------------------------
 * Real BeAPI delivers a click by having BControl (via its second base,
 * BInvoker) post a BMessage to a target Handler/Looper (the owning
 * BWindow, by default) when Invoke() runs. This binding does not expose
 * any of that -- no BMessage is ever constructed, no target/Looper is
 * ever set. Instead, HSButton overrides Invoke() itself (below) to fire
 * a plain callback directly, synchronously, on whatever thread Invoke()
 * itself runs on (the same message-loop thread every other View/Control
 * hook in this binding fires on -- see hs_view.h's threading note).
 * hs_button_create() always constructs the underlying BButton with a
 * NULL BMessage*; Invoke()'s inherited BInvoker behavior (posting that
 * message somewhere) never actually has anything to post, since it's
 * overridden away entirely before BControl/BInvoker's own logic runs.
 * This was a deliberate scope decision (BeAPI-faithful message-passing
 * was considered and rejected as more ceremony than this binding's
 * C#-idiomatic hook style -- see OnMouseDown/OnDraw/etc. in hs_view.h --
 * calls for) -- see details.md's "Button: why a direct OnClick hook" for
 * the full writeup.
 *
 * A REAL BUG THIS OVERRIDE INTRODUCED, FOUND AND FIXED
 * --------------------------------------------------------------------------
 * Replacing BButton::Invoke() wholesale (rather than calling it, or
 * BControl::Invoke(), from inside this override) silently dropped a
 * piece of real BButton behavior this binding didn't know it depended
 * on: gdb disassembly of the real installed libbe.so shows real
 * BButton::Invoke() -- not BControl::Invoke(), which only ever builds
 * and posts a BMessage and never touches Value() -- is what resets a
 * pushed (non-B_TOGGLE_BEHAVIOR) button's Value() back to B_CONTROL_OFF
 * after a click, unpressing it visually. Real BButton::MouseDown()'s own
 * synchronous mouse-tracking loop (also disassembled to check) does NOT
 * reset Value() itself when the mouse is released while still over the
 * button -- it leaves Value() at B_CONTROL_ON and calls Invoke(),
 * trusting Invoke() to reset it. Since HSButton::Invoke() replaced the
 * whole method instead of extending it, that reset never happened, and
 * every non-toggle button in this binding stayed visually pressed
 * forever after being clicked -- reported by a real user, root-caused
 * on hardware rather than guessed, and now fixed: HSButton::Invoke()
 * reproduces the same Behavior()-gated Value() reset explicitly (see
 * hs_button.cpp). Real BButton::Invoke() also does a Sync()+snooze(50ms)
 * "flash" before resetting, to guarantee the pressed look is visible for
 * at least 50ms even on a very fast click -- deliberately not
 * replicated here, since the reported bug was the missing reset, not a
 * too-brief flash, and adding a blocking snooze to every click was
 * judged not worth the tradeoff for this binding's synchronous callback
 * design.
 *
 * WHY BUTTON DOESN'T GET ITS OWN DRAW/ATTACHED/MOUSE/KEY CALLBACKS
 * --------------------------------------------------------------------------
 * Unlike HSView, HSButton does NOT override Draw(), AttachedToWindow(),
 * DetachedFromWindow(), MouseDown()/MouseUp()/MouseMoved(), or
 * KeyDown()/KeyUp() -- it lets BButton's own real implementations run
 * untouched (a native button draws and handles input entirely on its
 * own; that's the whole point of wrapping the real BButton rather than
 * hand-rolling one out of a plain View). Only Invoke() (the click) and
 * the destructor (see hs_view.h's DESTROYED CALLBACK note -- identical
 * reasoning, identical contract) are overridden here.
 *
 * REUSING hs_view_get_frame()/hs_view_move_to()/hs_view_resize_to()
 * --------------------------------------------------------------------------
 * There is no hs_button_get_frame()/move_to()/resize_to() -- Button.cs
 * calls the existing hs_view_* geometry functions directly with its own
 * handle instead. Verified safe: HSButton's BView subobject sits at
 * offset 0 (a small native probe on real Haiku hardware confirmed this
 * empirically for the whole BView/BControl/BButton chain, with BInvoker
 * -- BControl's OTHER base -- at a nonzero offset instead; see
 * hs_view.cpp's hs_view_add_child() comment and details.md's Button
 * section for the probe itself), so casting the opaque handle straight
 * to BView*, exactly like hs_view_add_child()/hs_window_add_child()
 * already do, reaches the correct BView subobject regardless of whether
 * the real object behind the handle is an HSView or an HSButton. This
 * does NOT extend to hs_view_set_*_callback()/FillRect()/StrokeRect()/
 * StrokeLine()/DrawString() -- those either touch HSView-specific data
 * members that don't exist at that layout on an HSButton (genuinely
 * unsafe, not just meaningless) or are only meaningful inside a real
 * Draw() callback a Button never receives -- Control.cs/Button.cs never
 * call any of those, by convention, the same way this binding's
 * ownership rules are convention-enforced elsewhere rather than typed.
 *
 * BCONTROL-LEVEL STATE MOVED TO hs_control.h
 * --------------------------------------------------------------------------
 * Label/Value/Enabled used to be implemented directly here, back when
 * HSButton was the only concrete HSView-family control this binding
 * had (this section used to say "revisit this the day a second
 * BControl-derived widget is added" -- TextControl is that day). They
 * now live in hs_control.h/hs_control.cpp instead, shared by any
 * concrete control whose handle can be cast straight to BControl* --
 * see that file's own header comment for the ABI reasoning. Call
 * hs_control_set_label()/hs_control_label()/etc. directly against a
 * button handle; nothing button-specific is lost by doing so.
 */
#ifndef HS_BUTTON_H
#define HS_BUTTON_H

#include "hs_types.h"

#ifdef __cplusplus
extern "C" {
#endif

typedef void (*hs_button_click_callback)(void* user_data);
typedef void (*hs_button_destroyed_callback)(void* user_data);

/* Create a new HSButton (a BButton subclass) with the given frame, name,
 * label, resizing mode, and flags -- same shape as hs_view_create(). Has
 * no parent and is not attached to any window yet; safe to configure
 * from whatever thread created it, same threading rule as hs_view.h's. */
hs_handle hs_button_create(hs_rect frame, const char* name, const char* label,
	uint32_t resizing_mode, uint32_t flags);

/* ONLY safe on a button that is not currently attached to a parent --
 * same OWNERSHIP rule as hs_view_destroy(). Fires the destroyed
 * callback, same as any other path to this button's destruction. */
void hs_button_destroy(hs_handle button);

void hs_button_set_click_callback(hs_handle button,
	hs_button_click_callback callback, void* user_data);

void hs_button_set_destroyed_callback(hs_handle button,
	hs_button_destroyed_callback callback, void* user_data);

/* Calls Invoke(NULL) directly -- the same call real BButton's own
 * MouseDown() tracking loop (an actual click) and KeyDown() (Enter/
 * Return on a default button) both ultimately make. This is genuine,
 * intended BInvoker API usage, not a synthetic test hack: real
 * application code is meant to be able to call Invoke() directly to
 * simulate the same action as a real click, and that's exactly what
 * this exposes -- see this file's own "A REAL BUG THIS OVERRIDE
 * INTRODUCED" note above for why Invoke()'s own behavior matters.
 * Fires the click callback synchronously on the calling thread, same
 * threading rule as every other hook in this binding. */
void hs_button_invoke(hs_handle button);

/* BControl-level state (Label/Value/IsEnabled) is NOT declared here --
 * see hs_control.h, shared with every other concrete control this
 * binding wraps. Call hs_control_set_label()/hs_control_label()/etc.
 * directly against a button handle; safe, see hs_control.h's own
 * rationale for why.
 *
 * BButton-specific state. */
void hs_button_make_default(hs_handle button, bool is_default);
bool hs_button_is_default(hs_handle button);

void hs_button_set_flat(hs_handle button, bool flat);
bool hs_button_is_flat(hs_handle button);

/* `behavior` is the raw BButton::BBehavior value (B_BUTTON_BEHAVIOR=0,
 * B_TOGGLE_BEHAVIOR=1, B_POP_UP_BEHAVIOR=2 -- plain sequential enum
 * values, verified against the actual installed Button.h, not
 * macro-computed the way ViewResizingMode's B_FOLLOW_* constants are). */
void hs_button_set_behavior(hs_handle button, uint32_t behavior);
uint32_t hs_button_behavior(hs_handle button);

#ifdef __cplusplus
}
#endif

#endif /* HS_BUTTON_H */
