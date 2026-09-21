/*
 * hs_view.h -- C shim over BView (headers/os/interface/View.h).
 *
 * Same trampoline shape as hs_application.h/hs_window.h -- read
 * hs_window.h first if you haven't, especially its "DESTROYED CALLBACK"
 * note, which this file leans on directly below. This comment covers only
 * what's actually different about BView.
 *
 * NO THREAD OF ITS OWN
 * ---------------------
 * Unlike BWindow, a BView never spawns anything. Every hook below
 * (AttachedToWindow, DetachedFromWindow, Draw) fires on whichever thread
 * is running the owning BWindow's message loop -- the same thread
 * hs_window.h's own callbacks already fire on -- automatically locked by
 * the app_server dispatch mechanism before the call arrives (Haiku Book:
 * "Whenever App Server calls a hook method it automatically locks the
 * BWindow for you"). A view that hasn't been added to a window yet, or
 * whose window has never been shown, has no thread delivering anything to
 * it at all -- exactly like a freshly-created, not-yet-shown BWindow (see
 * hs_window.h's own threading note), so it's safe to configure
 * (MoveTo/ResizeTo/SetHighColor/...) right after hs_view_create()
 * returns, on whatever thread created it.
 *
 * THE DESTROYED CALLBACK: SAME RATIONALE AS hs_window.h's, ONE MORE DEATH PATH
 * -----------------------------------------------------------------------------
 * A BView can die at a moment nothing else tells the managed side about,
 * for the same reason a BWindow can (see hs_window.h) -- plus one BView-
 * specific path BWindow doesn't have: a still-attached view is deleted
 * automatically, recursively, whenever its parent (a BWindow or another
 * BView) is destroyed, with no notice to the view itself beyond its own
 * C++ destructor running. HSView overrides that destructor (again: BView
 * has no "about to be deleted" virtual to hook instead) and fires a
 * registered callback unconditionally, right before the object's memory
 * goes away -- covering all three ways an HSView can die with one hook:
 * an explicit hs_view_destroy(), an explicit hs_window_remove_child() /
 * hs_view_remove_child() followed by the caller deleting it, and the
 * implicit cascade when a still-attached parent is destroyed out from
 * under it. Once this callback fires, the handle is dead -- same contract
 * as hs_window.h's destroyed callback.
 *
 * OWNERSHIP
 * ---------
 * hs_view_destroy() is only safe on a view that is NOT currently attached
 * to a parent -- either never added, or already detached via
 * hs_window_remove_child()/hs_view_remove_child(). Deleting a still-
 * attached view directly (rather than through its parent) would leave a
 * dangling pointer in that parent's child list; this shim does not guard
 * against that mistake natively, same as hs_window_destroy()'s own
 * ownership rule -- the managed View wrapper is responsible for tracking
 * whether a view is currently attached and picking the right call.
 * Removing a view (hs_window_remove_child()/hs_view_remove_child()) only
 * detaches it, exactly matching BeAPI's own RemoveChild() semantics --
 * ownership passes back to the caller, who must eventually either
 * re-attach it elsewhere or hs_view_destroy() it. Do not do both to the
 * same still-attached view: destroying it directly AND leaving it
 * attached is the dangling-pointer mistake above.
 *
 * DRAWING: WHY ONLY THE DEFAULT PATTERN, FOR NOW
 * ------------------------------------------------
 * FillRect()/StrokeRect()/StrokeLine() all take an optional `pattern`
 * argument upstream (an 8-byte dither mask, default B_SOLID_HIGH). This
 * slice only ever uses B_SOLID_HIGH -- exposing custom patterns is easy
 * to add later (same shape, one more parameter) but isn't needed to prove
 * Draw() round-trips correctly, which is what this slice is actually
 * about. Likewise, AddChild()'s optional `before` parameter (insert
 * position) isn't exposed yet -- hs_view_add_child()/hs_window_add_child()
 * always append to the end of the child list.
 *
 * MOUSE AND KEYBOARD INPUT: WHAT'S IN THE MESSAGE, VERIFIED, NOT ASSUMED
 * -------------------------------------------------------------------------
 * MouseDown()/MouseUp()/MouseMoved() all fire on the same thread as
 * AttachedToWindow()/Draw() above (the owning BWindow's message loop), for
 * the same reason. KeyDown()/KeyUp() only ever fire on whichever View
 * currently has focus (see MakeFocus()/IsFocus() below) -- app_server
 * never delivers them anywhere else.
 *
 * MouseDown()/MouseMoved()'s BView virtuals don't take a `buttons`
 * parameter directly -- it's pulled from Window()->CurrentMessage()
 * instead, which is safe here for the same reason CurrentMessage() is
 * safe to call from inside any BLooper-dispatched hook: this hook IS that
 * dispatch, on this same thread, for this same message. Checked directly
 * against the Be Book's B_MOUSE_DOWN/B_MOUSE_MOVED/B_MOUSE_UP field
 * listings before writing this, not assumed: B_MOUSE_DOWN and
 * B_MOUSE_MOVED both carry a "buttons" int32 field, but **B_MOUSE_UP does
 * not carry one at all** -- real BeAPI genuinely gives you no way to learn
 * which button was released from inside MouseUp() itself (only GetMouse()
 * polling, or remembering what MouseDown() told you, would -- both out of
 * scope for this slice). hs_view_mouse_up_callback() reflects that
 * honestly with no buttons parameter, rather than inventing one that
 * would always read 0 or be misleading.
 *
 * KeyDown()/KeyUp()'s `bytes`/`num_bytes` are passed straight through --
 * for ordinary character input this is a single UTF-8-encoded character
 * (1-4 bytes); for control keys (Tab, Enter, arrows, ...) it's the single
 * ASCII control byte from headers/os/interface/InterfaceDefs.h (see the
 * managed KeyBytes class, which copies the ones meaningful here verbatim
 * from that header). Distinguishing function keys (F1-F12) needs the raw
 * `key`/`states` fields from CurrentMessage() instead, since they carry no
 * meaningful `bytes` -- out of scope for this slice.
 *
 * MakeFocus()/IsFocus() work regardless of the view's flags -- B_NAVIGABLE
 * (see ViewFlags) only affects whether Tab-key cycling lands on a view
 * automatically, not whether MakeFocus(true) works when called directly.
 * Calling MakeFocus() is safe from within any hook below (the window is
 * already locked for you, same as the DRAWING note above) -- calling it
 * from an unrelated foreign thread needs the same LockLooper()/
 * UnlockLooper() this slice doesn't cover yet.
 *
 * MODIFIERS: A THIRD ASYMMETRY, THE MIRROR IMAGE OF THE buttons ONE ABOVE
 * ---------------------------------------------------------------------------
 * Same "checked the Be Book field listings, not assumed" rigor as the
 * buttons asymmetry above turned up a second one, in the opposite
 * direction: B_MOUSE_DOWN, B_MOUSE_UP, B_KEY_DOWN, and B_KEY_UP all carry
 * a "modifiers" int32 field (the current Shift/Control/Option/Command/...
 * state), but **B_MOUSE_MOVED does not carry one at all** -- unlike the
 * buttons field, which MouseMoved DOES get. This isn't an oversight in
 * BeOS's original message design: MouseMoved fires continuously while the
 * pointer moves, at real hardware-event volume, and modifier state is
 * cheaply available anytime via the standalone modifiers() global
 * function (see hs_modifiers() below) -- there was no need to fatten
 * every single MouseMoved message with a field that's already trivial to
 * query directly. So hs_view_mouse_down_callback/mouse_up/key_down/key_up
 * all gain a `modifiers` parameter (pulled from Window()->CurrentMessage(),
 * same mechanism as CurrentButtons()), but
 * hs_view_mouse_moved_callback deliberately does not -- callers who need
 * modifier state during a mouse-moved handler call hs_modifiers()
 * instead, same as any other code that isn't inside one of the four hooks
 * that get it for free.
 */
#ifndef HS_VIEW_H
#define HS_VIEW_H

#include "hs_types.h"

#ifdef __cplusplus
extern "C" {
#endif

/* Callback signatures -- see hs_window.h's callback doc for the general
 * shape (user_data carries a GCHandle). hs_view_destroyed_callback is the
 * same "no veto, nothing to return" shape as hs_window_destroyed_callback,
 * for the same reason. */
typedef void (*hs_view_attached_to_window_callback)(void* user_data);
typedef void (*hs_view_detached_from_window_callback)(void* user_data);
typedef void (*hs_view_draw_callback)(void* user_data, hs_rect update_rect);
typedef void (*hs_view_destroyed_callback)(void* user_data);

/* Mouse/keyboard callbacks -- see the MOUSE AND KEYBOARD INPUT note above
 * for exactly what `buttons`/`bytes`/`num_bytes` do and don't tell you.
 * `bytes` is a borrowed pointer valid only for the duration of the call --
 * copy it immediately if you need it afterward, same rule as any other
 * borrowed pointer in this binding (e.g. Message.FindString). */
typedef void (*hs_view_mouse_down_callback)(void* user_data, hs_point where,
	uint32_t buttons, uint32_t modifiers);
typedef void (*hs_view_mouse_up_callback)(void* user_data, hs_point where,
	uint32_t modifiers);
typedef void (*hs_view_mouse_moved_callback)(void* user_data, hs_point where,
	uint32_t transit, uint32_t buttons);
typedef void (*hs_view_key_down_callback)(void* user_data, const char* bytes,
	int32_t num_bytes, uint32_t modifiers);
typedef void (*hs_view_key_up_callback)(void* user_data, const char* bytes,
	int32_t num_bytes, uint32_t modifiers);

/* Create a new HSView (a BView subclass) with the given frame, name,
 * resizing mode (the raw B_FOLLOW_* bitmask from View.h), and flags (the
 * raw view-flags bitmask, e.g. B_WILL_DRAW). The view has no parent and
 * is not attached to any window yet -- see the threading note above. Do
 * all one-time setup before adding it to a window. */
hs_handle hs_view_create(hs_rect frame, const char* name,
	uint32_t resizing_mode, uint32_t flags);

/* ONLY safe on a view that is not currently attached to a parent -- see
 * the OWNERSHIP note above. Fires the destroyed callback, same as any
 * other path to this view's destruction. */
void hs_view_destroy(hs_handle view);

void hs_view_set_attached_to_window_callback(hs_handle view,
	hs_view_attached_to_window_callback callback, void* user_data);
void hs_view_set_detached_from_window_callback(hs_handle view,
	hs_view_detached_from_window_callback callback, void* user_data);
void hs_view_set_draw_callback(hs_handle view,
	hs_view_draw_callback callback, void* user_data);
void hs_view_set_destroyed_callback(hs_handle view,
	hs_view_destroyed_callback callback, void* user_data);
void hs_view_set_mouse_down_callback(hs_handle view,
	hs_view_mouse_down_callback callback, void* user_data);
void hs_view_set_mouse_up_callback(hs_handle view,
	hs_view_mouse_up_callback callback, void* user_data);
void hs_view_set_mouse_moved_callback(hs_handle view,
	hs_view_mouse_moved_callback callback, void* user_data);
void hs_view_set_key_down_callback(hs_handle view,
	hs_view_key_down_callback callback, void* user_data);
void hs_view_set_key_up_callback(hs_handle view,
	hs_view_key_up_callback callback, void* user_data);

/* Adds child to the end of view's child list (see the DRAWING note above
 * for why `before` isn't exposed yet). AttachedToWindow() fires on child
 * (and its own descendants, if any) immediately if view is itself already
 * attached to a window -- otherwise it fires later, when view is. `child`
 * may be an HSView* or any other HSView-family handle (e.g. HSButton* --
 * see hs_button.h) -- see hs_view_add_child()'s own comment in
 * hs_view.cpp for why that's safe. */
void hs_view_add_child(hs_handle view, hs_handle child);

/* Detaches child from view's child list without deleting it -- see the
 * OWNERSHIP note above. Returns false if child was not actually a child
 * of view. Same "any HSView-family handle" note as hs_view_add_child()
 * applies to child here too. */
bool hs_view_remove_child(hs_handle view, hs_handle child);

/* hs_view_get_frame()/hs_view_move_to()/hs_view_resize_to() are also
 * called directly against HSButton handles (see hs_button.h) -- reused
 * rather than duplicated, since they only ever touch plain inherited
 * BView state. hs_view_get_bounds() is NOT reused this way (nothing in
 * the Button/Control slice needs a control's own Bounds() yet). */
void hs_view_get_frame(hs_handle view, hs_rect* out_frame);
void hs_view_get_bounds(hs_handle view, hs_rect* out_bounds);
void hs_view_move_to(hs_handle view, float x, float y);
void hs_view_resize_to(hs_handle view, float width, float height);

/* Colors cross as four bytes rather than an hs_rgb_color struct -- one
 * fewer type to marshal for three functions that are otherwise identical,
 * and rgb_color's own layout (see hs_types.h) is trivial enough that
 * nothing is lost by not routing it through the struct here. */
void hs_view_set_high_color(hs_handle view, uint8_t red, uint8_t green,
	uint8_t blue, uint8_t alpha);
void hs_view_set_low_color(hs_handle view, uint8_t red, uint8_t green,
	uint8_t blue, uint8_t alpha);
void hs_view_set_view_color(hs_handle view, uint8_t red, uint8_t green,
	uint8_t blue, uint8_t alpha);

/* Only ever called from within a Draw() callback in this slice (drawing
 * outside of Draw() -- e.g. in response to a button click -- needs
 * LockLooper()/UnlockLooper() around it upstream, which this slice
 * doesn't cover yet). Uses the default B_SOLID_HIGH pattern -- see the
 * DRAWING note above. */
void hs_view_fill_rect(hs_handle view, hs_rect rect);
void hs_view_stroke_rect(hs_handle view, hs_rect rect);
void hs_view_stroke_line(hs_handle view, hs_point start, hs_point end);
void hs_view_draw_string(hs_handle view, const char* text, hs_point location);

/* Marks the view's whole visible area dirty, so app_server schedules a
 * Draw() call for it -- the only way, in this slice, to get something new
 * on screen in response to a mouse/keyboard event rather than only the
 * view's very first expose. Safe to call from within any hook below (see
 * the MOUSE AND KEYBOARD INPUT note above); from a foreign thread it needs
 * the same locking that note flags as not yet covered. */
void hs_view_invalidate(hs_handle view);

/* MakeFocus(true) requests keyboard focus for this view (stealing it from
 * whichever view had it); MakeFocus(false) gives it up. IsFocus() reports
 * whether this view currently has it. Only the view that IsFocus() is true
 * for will ever receive hs_view_key_down_callback/hs_view_key_up_callback
 * -- see the MOUSE AND KEYBOARD INPUT note above for why B_NAVIGABLE
 * doesn't gate this. */
void hs_view_make_focus(hs_handle view, bool focus);
bool hs_view_is_focus(hs_handle view);

/* Wraps the global modifiers() function from InterfaceDefs.h -- the
 * current Shift/Control/Option/Command/CapsLock/... state, queryable at
 * any time, not just from inside one of the four hooks above that get it
 * for free (see the MODIFIERS note above, especially for
 * hs_view_mouse_moved_callback, which doesn't). Not really "about" any
 * particular view -- it lives here rather than in a dedicated
 * hs_interface_defs.h because this is the only place it's needed so far
 * and there's no such file yet. Safe to call from any thread: it's a
 * simple read of shared input-server state, not something that needs a
 * locked BWindow the way drawing calls do. */
uint32_t hs_modifiers(void);

#ifdef __cplusplus
}
#endif

#endif /* HS_VIEW_H */
