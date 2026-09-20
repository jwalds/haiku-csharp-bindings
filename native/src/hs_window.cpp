/*
 * hs_window.cpp -- implementation of the BWindow C shim. See hs_window.h
 * for the design rationale (why there's a destroyed callback, why quit()
 * posts a message instead of calling Quit(), the ownership rule) before
 * changing anything here. Follows hs_application.cpp's trampoline shape;
 * read that file's own header comment too if this is your first kit file.
 */
#include "hs_window.h"

#include <cstddef>
#include <cstring>

#include <Window.h>
#include <Message.h>
#include <Rect.h>


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
		fDestroyedUserData(NULL)
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
	}

	virtual void MessageReceived(BMessage* message)
	{
		if (fMessageReceivedCallback != NULL) {
			fMessageReceivedCallback(fMessageReceivedUserData,
				static_cast<hs_handle>(message));
		} else {
			BWindow::MessageReceived(message);
		}
	}

	virtual bool QuitRequested()
	{
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

private:
	hs_window_message_received_callback	fMessageReceivedCallback;
	void*									fMessageReceivedUserData;
	hs_window_quit_requested_callback		fQuitRequestedCallback;
	void*									fQuitRequestedUserData;
	hs_window_destroyed_callback			fDestroyedCallback;
	void*									fDestroyedUserData;
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


void hs_window_show(hs_handle window)
{
	/* The FIRST call to Show() is what starts the window's message-loop
	 * thread (see hs_window.h's "THE THREAD DOESN'T EXIST YET" note) --
	 * nothing special to do here, BWindow::Show() already handles both
	 * the first-call and subsequent-call cases correctly on its own. */
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
