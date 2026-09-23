/*
 * hs_window.cpp -- implementation of the BWindow C shim. See hs_window.h
 * for the design rationale (why there's a destroyed callback, why quit()
 * posts a message instead of calling Quit(), the ownership rule) before
 * changing anything here. Follows hs_application.cpp's trampoline shape;
 * read that file's own header comment too if this is your first kit file.
 */
#include "hs_window.h"

#include <cstddef>
#include <cstdio>
#include <cstring>

#include <Window.h>
#include <Message.h>
#include <Rect.h>
#include <View.h>

#include "hs_mono_thread_attach.h"
#include "hs_mono_thread_detach.h"


namespace {

/*
 * Second C++ subclass in this binding (after HSApplication). Same
 * trampoline shape, plus one thing HSApplication doesn't need: a
 * destructor override, because a window can die at a moment nothing else
 * tells the managed side about (see hs_window.h's "DESTROYED CALLBACK"
 * note).
 */
class HSWindow : public BWindow {
public:
	HSWindow(BRect frame, const char* title, window_look look,
		window_feel feel, uint32 flags)
		:
		BWindow(frame, title, look, feel, flags),
		fMessageReceivedCallback(NULL),
		fMessageReceivedUserData(NULL),
		fQuitRequestedCallback(NULL),
		fQuitRequestedUserData(NULL),
		fDestroyedCallback(NULL),
		fDestroyedUserData(NULL),
		fShown(false)
	{
	}

	virtual ~HSWindow()
	{
		/* Fires unconditionally, no matter which of the two death paths
		 * got us here (see hs_window.h) -- this is the ONE hook that
		 * covers both. Deliberately the very first thing in the
		 * destructor body, before any BWindow/BLooper teardown that base
		 * destructors still have to run, so the managed side learns the
		 * native object is on its way out as early as possible. */
		if (fDestroyedCallback != NULL)
			fDestroyedCallback(fDestroyedUserData);

		/* FIX (KNOWN_ISSUES.md #3/#5): explicit mono_thread_attach()
		 * (hs_mono_thread_attach.h) means mono_domain_get() is no longer
		 * NULL here, so mono_thread_current() itself is safe to call --
		 * but calling a REAL mono_thread_detach() is NOT: two
		 * independent, 100%-reproducible failure modes were found on
		 * real hardware (both gdb-confirmed), depending on whether this
		 * window's thread ever ran BView::Draw(): a fatal STATE_BLOCKING
		 * abort for windows that drew (Draw()'s own app_server IPC
		 * leaves the thread in Mono's cooperative-GC blocking state,
		 * unbalanced, and mono_thread_detach() then tries to enter that
		 * same state again), and -- discovered only after that first
		 * failure mode was worked around -- a corrupted internal Mono
		 * hash table for windows that never drew (no crash at the call
		 * site, but mono-hash.c's "hash != NULL" assertion spins
		 * forever at process shutdown, hanging the whole process with
		 * exit code never returned). See KNOWN_ISSUES.md #3 for the
		 * full writeup of both. Only the safe, no-op-capable
		 * TLS-destructor-based detach (hs_mono_thread_detach.h) is used
		 * here as a result, unconditionally, regardless of drawing --
		 * it doesn't silence the benign "Failed aborting id" warning
		 * (mono_thread_detach_if_exiting() is always a no-op for these
		 * threads, also documented in KNOWN_ISSUES.md #3), but it is
		 * the only detach-adjacent call proven safe in every
		 * configuration tested. */
		if (fShown) {
			/* hs_mono_thread_attach.h's RealDetachIsSafe(): a real detach
			 * is only safe under MONO_THREADS_SUSPEND=preemptive (see that
			 * header for the full investigation); otherwise fall back to
			 * the always-safe no-op below. */
			if (hs_internal::RealDetachIsSafe())
				mono_thread_detach(mono_thread_current());
			else
				hs_internal::MarkThreadForMonoDetachOnExit();
		}
	}

	virtual void MessageReceived(BMessage* message)
	{
		/* FIX (KNOWN_ISSUES.md #3/#5): see hs_mono_thread_attach.h --
		 * must run before the first delegate invocation below, on every
		 * call, on whatever thread this is (usually a no-op after the
		 * first). */
		hs_internal::EnsureThreadAttached();

		if (fMessageReceivedCallback != NULL) {
			fMessageReceivedCallback(fMessageReceivedUserData,
				static_cast<hs_handle>(message));
		} else {
			BWindow::MessageReceived(message);
		}
	}

	virtual bool QuitRequested()
	{
		/* FIX (KNOWN_ISSUES.md #3/#5): see hs_mono_thread_attach.h. */
		hs_internal::EnsureThreadAttached();

		if (fQuitRequestedCallback != NULL)
			return fQuitRequestedCallback(fQuitRequestedUserData) != 0;
		return BWindow::QuitRequested();
	}

	void SetMessageReceivedCallback(hs_window_message_received_callback callback,
		void* userData)
	{
		fMessageReceivedCallback = callback;
		fMessageReceivedUserData = userData;
	}

	void SetQuitRequestedCallback(hs_window_quit_requested_callback callback,
		void* userData)
	{
		fQuitRequestedCallback = callback;
		fQuitRequestedUserData = userData;
	}

	void SetDestroyedCallback(hs_window_destroyed_callback callback,
		void* userData)
	{
		fDestroyedCallback = callback;
		fDestroyedUserData = userData;
	}

	/* FIX (KNOWN_ISSUES.md #3/#5): set from hs_window_show()'s first
	 * call, so the destructor knows whether this window's own thread
	 * ever actually ran. */
	void MarkShown()
	{
		fShown = true;
	}

private:
	hs_window_message_received_callback	fMessageReceivedCallback;
	void*									fMessageReceivedUserData;
	hs_window_quit_requested_callback		fQuitRequestedCallback;
	void*									fQuitRequestedUserData;
	hs_window_destroyed_callback			fDestroyedCallback;
	void*									fDestroyedUserData;
	bool									fShown;
};


inline BRect ToBRect(hs_rect r)
{
	return BRect(r.left, r.top, r.right, r.bottom);
}


inline hs_rect ToHsRect(BRect r)
{
	hs_rect out;
	out.left = r.left;
	out.top = r.top;
	out.right = r.right;
	out.bottom = r.bottom;
	return out;
}

} // namespace


hs_handle hs_window_create(hs_rect frame, const char* title,
	int32_t look, int32_t feel, uint32_t flags)
{
	HSWindow* window = new HSWindow(ToBRect(frame), title,
		static_cast<window_look>(look), static_cast<window_feel>(feel),
		static_cast<uint32>(flags));
	return static_cast<hs_handle>(window);
}


void hs_window_destroy(hs_handle window)
{
	/* Only ever safe on a window that has never been shown -- see the
	 * ownership rule in hs_window.h. Once Show() has been called, the
	 * window's own thread is live and hs_window_quit() is the only
	 * supported way to end its life; the managed Window wrapper is
	 * responsible for enforcing that, not this function. */
	delete static_cast<HSWindow*>(window);
}


void hs_window_set_message_received_callback(hs_handle window,
	hs_window_message_received_callback callback, void* user_data)
{
	static_cast<HSWindow*>(window)->SetMessageReceivedCallback(callback, user_data);
}


void hs_window_set_quit_requested_callback(hs_handle window,
	hs_window_quit_requested_callback callback, void* user_data)
{
	static_cast<HSWindow*>(window)->SetQuitRequestedCallback(callback, user_data);
}


void hs_window_set_destroyed_callback(hs_handle window,
	hs_window_destroyed_callback callback, void* user_data)
{
	static_cast<HSWindow*>(window)->SetDestroyedCallback(callback, user_data);
}


void hs_window_add_child(hs_handle window, hs_handle view)
{
	/* `view` is really an HSView* (see hs_view.cpp), a class this file
	 * has no definition for -- but HSView derives from BView with plain
	 * single, non-virtual inheritance and adds no data members ahead of
	 * that base, so an HSView* and its BView subobject share the same
	 * address (true for every real Haiku/GCC target this binding builds
	 * on). Casting the opaque handle straight to BView*, skipping HSView
	 * entirely, needs no more than that fact and BWindow::AddChild()'s
	 * own signature (which only ever wants a BView*). */
	static_cast<HSWindow*>(window)->AddChild(static_cast<BView*>(view));
}


bool hs_window_remove_child(hs_handle window, hs_handle view)
{
	return static_cast<HSWindow*>(window)->RemoveChild(static_cast<BView*>(view));
}


void hs_window_show(hs_handle window)
{
	/* The FIRST call to Show() is what starts the window's message-loop
	 * thread (see hs_window.h's "THE THREAD DOESN'T EXIST YET" note) --
	 * nothing special to do here, BWindow::Show() already handles both
	 * the first-call and subsequent-call cases correctly on its own. */
	static_cast<HSWindow*>(window)->MarkShown();
	static_cast<HSWindow*>(window)->Show();
}


void hs_window_hide(hs_handle window)
{
	static_cast<HSWindow*>(window)->Hide();
}


bool hs_window_is_hidden(hs_handle window)
{
	return static_cast<HSWindow*>(window)->IsHidden();
}


void hs_window_quit(hs_handle window)
{
	/* Posting B_QUIT_REQUESTED is exactly what a window's own close box
	 * does internally, and is documented thread-safe from any thread --
	 * unlike calling Quit() directly, which requires the caller to
	 * already hold the window's lock (see hs_window.h's "QUITTING" note
	 * for why this shim avoids that entirely). */
	static_cast<HSWindow*>(window)->PostMessage(B_QUIT_REQUESTED);
}


bool hs_window_lock(hs_handle window)
{
	return static_cast<HSWindow*>(window)->Lock();
}


void hs_window_unlock(hs_handle window)
{
	static_cast<HSWindow*>(window)->Unlock();
}


bool hs_window_is_locked(hs_handle window)
{
	return static_cast<HSWindow*>(window)->IsLocked();
}


void hs_window_set_title(hs_handle window, const char* title)
{
	static_cast<HSWindow*>(window)->SetTitle(title);
}


const char* hs_window_title(hs_handle window)
{
	return static_cast<HSWindow*>(window)->Title();
}


void hs_window_get_frame(hs_handle window, hs_rect* out_frame)
{
	if (out_frame == NULL)
		return;
	*out_frame = ToHsRect(static_cast<HSWindow*>(window)->Frame());
}


void hs_window_move_to(hs_handle window, float x, float y)
{
	static_cast<HSWindow*>(window)->MoveTo(x, y);
}


void hs_window_resize_to(hs_handle window, float width, float height)
{
	static_cast<HSWindow*>(window)->ResizeTo(width, height);
}
