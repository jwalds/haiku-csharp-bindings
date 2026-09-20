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

/* Adds child to the end of view's child list (see the DRAWING note above
 * for why `before` isn't exposed yet). AttachedToWindow() fires on child
 * (and its own descendants, if any) immediately if view is itself already
 * attached to a window -- otherwise it fires later, when view is. */
void hs_view_add_child(hs_handle view, hs_handle child);

/* Detaches child from view's child list without deleting it -- see the
 * OWNERSHIP note above. Returns false if child was not actually a child
 * of view. */
bool hs_view_remove_child(hs_handle view, hs_handle child);

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

#ifdef __cplusplus
}
#endif

#endif /* HS_VIEW_H */
