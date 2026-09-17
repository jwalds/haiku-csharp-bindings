/*
 * hs_application.h -- C shim over BApplication/BLooper/BHandler
 * (headers/os/app/{Application,Looper,Handler}.h).
 *
 * THE CORE PROBLEM THIS FILE SOLVES
 * ----------------------------------
 * BApplication is meant to be subclassed: your app overrides virtuals like
 * MessageReceived(), QuitRequested(), ReadyToRun() and Haiku's own BLooper
 * machinery calls them for you from its message loop. But C# code can't
 * subclass a C++ class, and P/Invoke only lets C# call INTO native code --
 * not the reverse. So instead:
 *
 *   1. We provide one concrete C++ subclass, HSApplication, that overrides
 *      those virtuals ONCE, in native code.
 *   2. Each override's ENTIRE body is: "if a callback function pointer has
 *      been registered, call it; otherwise fall back to the base class
 *      behavior."
 *   3. From C#, you register a managed delegate as that callback (via
 *      Marshal.GetFunctionPointerForDelegate), and THAT is what actually
 *      runs when Haiku's message loop invokes MessageReceived().
 *
 * This is the same "trampoline" shape every native-callback binding uses
 * (Win32 window procs, GTK signal handlers, etc.) -- it's not specific to
 * Haiku, but Haiku's BLooper model adds one twist covered below.
 *
 * THE THREADING TWIST
 * --------------------
 * BLooper::Run() spawns a NEW NATIVE OS THREAD for the message loop and
 * returns immediately -- it does not block. That means every callback
 * registered here fires ON THAT SPAWNED THREAD, not on the thread that
 * created the BApplication. This has two consequences:
 *
 *   - That native thread did not exist when Mono started, so it isn't
 *     "attached" to the Mono runtime the way your main C# thread is. In
 *     practice, calling into a managed delegate through a marshaled native
 *     function pointer causes Mono's embedding layer to auto-attach an
 *     unknown calling thread on first entry -- but this is exactly the kind
 *     of interaction that's easy to get subtly wrong and needs to be proven
 *     empirically on THIS Mono-on-Haiku build, not assumed from how it
 *     works elsewhere. The Sample project's whole job is to be that proof.
 *   - Because callbacks fire on a different thread than your main C# code,
 *     don't assume ordinary (non-atomic) shared state is safe to touch from
 *     both places without synchronization -- the same rule as any other
 *     multithreaded C# code, just easy to forget when the "other thread"
 *     is invisible native code.
 *
 * hs_application_run_and_wait() below blocks the CALLING thread (via the
 * kernel's wait_for_thread(), not busy-polling) until the spawned looper
 * thread actually exits, so a simple console Main() can just call it and
 * fall through when the app quits -- but the callbacks themselves still run
 * on the OTHER (looper) thread while you're blocked in that call.
 *
 * OWNERSHIP AND THE BLooper::Quit() TRAP
 * ----------------------------------------
 * When QuitRequested() returns true, BLooper's own message loop deletes the
 * BApplication/HSApplication object itself right before its thread exits.
 * This is normal, correct Haiku behavior -- but it means that once
 * hs_application_run_and_wait() returns, the hs_handle you passed to it is
 * a DANGLING POINTER. This exact mistake (a managed wrapper trying to free
 * the native object a second time after Quit() already deleted it) is a
 * real bug the dotnet-haiku GSoC project hit and had to patch around. The
 * rule enforced by this API: after hs_application_run_and_wait() returns,
 * the handle is dead -- do not pass it to any other hs_application_* or
 * hs_message_* function, including hs_application_destroy(). The managed
 * Application class encodes this by nulling its own handle field the
 * instant the wait call returns.
 */
#ifndef HS_APPLICATION_H
#define HS_APPLICATION_H

#include "hs_types.h"

#ifdef __cplusplus
extern "C" {
#endif

/* Callback signatures. `user_data` is whatever opaque pointer you passed to
 * the matching hs_application_set_*_callback call (the managed side uses it
 * to carry a GCHandle back to the right Application instance).
 *
 * hs_message_received_callback's `message` handle is a BORROWED reference
 * owned by the looper's message queue -- do not destroy it (see
 * hs_message.h's ownership note). It is only valid for the duration of the
 * callback; do not retain the handle past that.
 *
 * hs_quit_requested_callback returns 1 to allow the quit, 0 to veto it,
 * mirroring QuitRequested()'s bool return.
 */
typedef void (*hs_message_received_callback)(void* user_data, hs_handle message);
typedef int  (*hs_quit_requested_callback)(void* user_data);
typedef void (*hs_ready_to_run_callback)(void* user_data);

/* Create a new HSApplication (a BApplication subclass). `signature` must be
 * a valid Haiku app signature MIME string (e.g. "application/x-vnd.Example-Demo"),
 * exactly as BApplication's own constructor requires. On failure, returns
 * NULL and, if out_error is non-NULL, writes BApplication's InitCheck()
 * result there. */
hs_handle hs_application_create(const char* signature, hs_status* out_error);

/* Only for the "creation failed / never ran" cleanup path -- see the
 * ownership note above. Never call this on a handle that has been passed to
 * hs_application_run_and_wait() and returned from it. */
void hs_application_destroy(hs_handle app);

/* Register callbacks. Registering NULL for any of these restores the base
 * BApplication/BLooper/BHandler behavior for that hook (e.g. a NULL
 * quit-requested callback means "always allow quit", matching
 * BHandler::QuitRequested()'s own default of returning true). Safe to call
 * before Run(); calling after Run() races with the looper thread and is not
 * supported yet. */
void hs_application_set_message_received_callback(hs_handle app,
	hs_message_received_callback callback, void* user_data);
void hs_application_set_quit_requested_callback(hs_handle app,
	hs_quit_requested_callback callback, void* user_data);
void hs_application_set_ready_to_run_callback(hs_handle app,
	hs_ready_to_run_callback callback, void* user_data);

/* Start the message loop and block the calling thread until it exits
 * (i.e. until something causes QuitRequested() to return true and the loop
 * to finish). See the OWNERSHIP note above: `app` is invalid the instant
 * this returns. Returns the exit status of the looper thread (from the
 * kernel's wait_for_thread()), not a Haiku-meaningful value beyond
 * "0/positive means it exited normally". */
hs_status hs_application_run_and_wait(hs_handle app);

/* Post a copy of `message` to this application's own message queue (thread-
 * and cross-callback-safe -- this is exactly BLooper::PostMessage(BMessage*),
 * Haiku's own supported way to get a message onto a looper without holding
 * its lock). You still own `message` afterward; BLooper::PostMessage copies
 * it. */
hs_status hs_application_post_message(hs_handle app, hs_handle message);

/* Convenience: post a message that carries only a "what" code and nothing
 * else, without needing to create+destroy an hs_message for it. */
hs_status hs_application_post_message_what(hs_handle app, uint32_t what);

#ifdef __cplusplus
}
#endif

#endif /* HS_APPLICATION_H */
