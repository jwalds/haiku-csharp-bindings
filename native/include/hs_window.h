/*
 * hs_window.h -- C shim over BWindow (headers/os/interface/Window.h).
 *
 * BWindow follows the exact same "trampoline" subclassing shape as
 * HSApplication in hs_application.h -- read that file's design rationale
 * first if you haven't. This file's notes below cover what's actually
 * DIFFERENT about BWindow, and there's more than you'd guess -- all of it
 * verified against Haiku's own Window.cpp/Looper.cpp source and the Haiku
 * Book rather than assumed, after this project got burned twice before by
 * assuming instead of checking (see the top-level README).
 *
 * THE THREAD DOESN'T EXIST YET
 * -----------------------------
 * Unlike BApplication (whose Run() spawns its message-loop thread
 * immediately), a freshly-constructed BWindow has NO running thread at
 * all. Every BWindow constructor delegates to a private _InitData() that
 * never spawns a thread; the message loop only starts on the FIRST call to
 * Show() (Haiku Book: "windows are hidden by default, you must call Show()
 * to show the window, starting the message loop going"). This means it's
 * safe to do setup (SetTitle, future AddChild, ...) immediately after
 * hs_window_create() returns, on whatever thread created it, with no
 * locking needed -- there's no other thread yet to race with.
 * hs_window_show()'s FIRST call is the one and only moment that changes.
 *
 * THE DESTROYED CALLBACK: WHY THIS SHIM NEEDS ONE AND HSApplication DOESN'T
 * ---------------------------------------------------------------------------
 * HSApplication's shim gets away without a "the native object is gone"
 * notification because hs_application_run_and_wait() BLOCKS the calling
 * thread until the app's thread exits -- the instant that call returns,
 * the managed Application wrapper knows synchronously the native object is
 * dead (see Application.cs). There is no equivalent blocking call for a
 * window: real Haiku apps create windows and let them live or die
 * independently while the main thread is off doing something else (often
 * blocked in the application's own Run()). A window can be destroyed -- by
 * the user clicking its close box, by hs_window_quit(), by
 * B_QUIT_ON_WINDOW_CLOSE -- at a moment the managed wrapper has no other
 * way to learn about.
 *
 * The fix: HSWindow overrides its own C++ destructor (not a BWindow
 * virtual -- BWindow has no "about to be deleted" hook to override) and
 * fires a registered callback from there, unconditionally, right before
 * the object's memory actually goes away. This covers BOTH ways an
 * HSWindow can die with exactly one hook: the normal quit flow
 * (QuitRequested() returns true -> BLooper's own thread-exit machinery
 * eventually does `delete this` ON THE WINDOW'S OWN THREAD) and the
 * "never shown, changed my mind" cleanup path (hs_window_destroy() calls
 * `delete` directly, synchronously, on whatever thread called it). Once
 * this callback fires, the handle is dead -- the managed Window wrapper
 * nulls its own handle field from inside it, the same contract
 * Application.cs enforces after hs_application_run_and_wait() returns,
 * just triggered from a different place because BWindow's lifecycle is
 * shaped differently from BApplication's.
 *
 * QUITTING: WHY hs_window_quit() POSTS A MESSAGE INSTEAD OF CALLING Quit()
 * ---------------------------------------------------------------------------
 * BWindow::Quit() (overriding BLooper::Quit()) REQUIRES the window to
 * already be locked by the calling thread -- verified straight from
 * src/kits/interface/Window.cpp: an unlocked call logs "ERROR - you must
 * Lock a looper before calling Quit()" and only survives via a defensive
 * fallback Lock() that can still race. Getting that locking protocol
 * exactly right from arbitrary calling threads is real complexity this
 * shim doesn't need to take on, because there's a simpler and equally
 * correct path: posting B_QUIT_REQUESTED through the normal message queue
 * is exactly what a window's own close box does internally, is documented
 * thread-safe from any thread (including the window's own), and is the
 * same PostMessage-based pattern this binding already uses for
 * Application (see SystemMessages.QuitRequested in Haiku.App). So
 * hs_window_quit() posts, it doesn't call Quit() directly.
 *
 * OWNERSHIP: THE ONE RULE THAT MATTERS
 * --------------------------------------
 * hs_window_destroy() is ONLY safe to call on a window that has NEVER been
 * shown (hs_window_show() never called) -- at that point nothing but the
 * calling thread has ever touched it, so a direct synchronous delete is
 * fine. Once hs_window_show() has been called even once, the window's own
 * thread is live, and posting B_QUIT_REQUESTED via hs_window_quit() is the
 * only supported way to end its life -- calling hs_window_destroy() on a
 * shown window would race with its own thread and is undefined behavior.
 * This binding does not guard against that mistake in native code; the
 * managed Window wrapper tracks whether Show() has ever been called and
 * picks the right one (see Window.cs's Dispose()).
 */
#ifndef HS_WINDOW_H
#define HS_WINDOW_H

#include "hs_types.h"

#ifdef __cplusplus
extern "C" {
#endif

/* Callback signatures -- see hs_application.h's callback doc for the
 * general shape (user_data carries a GCHandle). hs_window_destroyed_callback
 * is the genuinely new kind here: see the "DESTROYED CALLBACK" note above.
 * It fires exactly once per window, unconditionally, from whatever thread
 * is doing the deleting -- there is nothing to veto and nothing to
 * return. */
typedef void (*hs_window_message_received_callback)(void* user_data, hs_handle message);
typedef int  (*hs_window_quit_requested_callback)(void* user_data);
typedef void (*hs_window_destroyed_callback)(void* user_data);

/* window_look values (headers/os/interface/Window.h) -- numeric values
 * copied verbatim from that header so this shim never has to be kept "in
 * sync" with it beyond this one copy. */
enum hs_window_look {
	HS_WINDOW_LOOK_BORDERED    = 20,
	HS_WINDOW_LOOK_NO_BORDER   = 19,
	HS_WINDOW_LOOK_TITLED      = 1,
	HS_WINDOW_LOOK_DOCUMENT    = 11,
	HS_WINDOW_LOOK_MODAL       = 3,
	HS_WINDOW_LOOK_FLOATING    = 7
};

/* window_feel values, same rationale. */
enum hs_window_feel {
	HS_WINDOW_FEEL_NORMAL           = 0,
	HS_WINDOW_FEEL_MODAL_SUBSET     = 2,
	HS_WINDOW_FEEL_MODAL_APP        = 1,
	HS_WINDOW_FEEL_MODAL_ALL        = 3,
	HS_WINDOW_FEEL_FLOATING_SUBSET  = 5,
	HS_WINDOW_FEEL_FLOATING_APP     = 4,
	HS_WINDOW_FEEL_FLOATING_ALL     = 6
};

/* Create a new HSWindow (a BWindow subclass) with the given frame (see
 * hs_types.h's hs_rect), title, look, feel, and flags (the raw window-flags
 * bitmask from Window.h -- e.g. B_QUIT_ON_WINDOW_CLOSE). The window is
 * hidden and has no running thread yet -- see the threading note above.
 * Do all one-time setup before the first hs_window_show(). */
hs_handle hs_window_create(hs_rect frame, const char* title,
	int32_t look, int32_t feel, uint32_t flags);

/* ONLY safe if hs_window_show() has never been called on this handle --
 * see the OWNERSHIP note above. Fires the destroyed callback, same as any
 * other path to this window's destruction. */
void hs_window_destroy(hs_handle window);

void hs_window_set_message_received_callback(hs_handle window,
	hs_window_message_received_callback callback, void* user_data);
void hs_window_set_quit_requested_callback(hs_handle window,
	hs_window_quit_requested_callback callback, void* user_data);
void hs_window_set_destroyed_callback(hs_handle window,
	hs_window_destroyed_callback callback, void* user_data);

/* Shows the window. The FIRST call on a given window starts its message
 * loop thread and unlocks it -- see the threading note above. Safe to
 * call more than once (matches BWindow::Show()'s own show/hide counter
 * semantics). */
/* Adds child to the end of the window's top-level child list -- a window
 * acts as the root of its own view hierarchy, exactly like a BView would
 * for a nested child (see hs_view.h's own hs_view_add_child(), and its
 * DRAWING note for why `before` isn't exposed). AttachedToWindow() fires
 * on child (and its descendants) immediately, on whatever thread calls
 * this -- before the window has necessarily been shown, if it hasn't
 * been yet (see the THREAD note above: with no thread running yet, there
 * is nothing to race with). */
void hs_window_add_child(hs_handle window, hs_handle view);

/* Detaches view from the window without deleting it -- see hs_view.h's
 * OWNERSHIP note. Returns false if view was not actually a child of this
 * window. */
bool hs_window_remove_child(hs_handle window, hs_handle view);

void hs_window_show(hs_handle window);
void hs_window_hide(hs_handle window);
bool hs_window_is_hidden(hs_handle window);

/* Requests the window close the same way clicking its close box does --
 * see the QUITTING note above. Safe to call from any thread, including
 * the window's own, but only takes effect once the window has actually
 * been shown (otherwise nothing is dispatching messages yet and this
 * would sit in the queue forever -- use hs_window_destroy() instead for a
 * never-shown window). Destruction (and the destroyed callback) happens
 * asynchronously, shortly after this returns. */
void hs_window_quit(hs_handle window);

/* BLooper::Lock()/Unlock()/IsLocked() -- required around any access to a
 * live (shown) window's state from a thread other than the window's own.
 * A freshly-created, not-yet-shown window needs none of this (see the
 * threading note above); it matters only once the window has actually
 * been shown and other code might be touching it concurrently. */
bool hs_window_lock(hs_handle window);
void hs_window_unlock(hs_handle window);
bool hs_window_is_locked(hs_handle window);

void hs_window_set_title(hs_handle window, const char* title);
/* Hands back a pointer into the window's own internal storage -- copy it
 * out immediately, exactly like hs_message_find_string. */
const char* hs_window_title(hs_handle window);

void hs_window_get_frame(hs_handle window, hs_rect* out_frame);
void hs_window_move_to(hs_handle window, float x, float y);
void hs_window_resize_to(hs_handle window, float width, float height);

#ifdef __cplusplus
}
#endif

#endif /* HS_WINDOW_H */
