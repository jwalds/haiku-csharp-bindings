/*
 * hs_application.cpp -- implementation of the BApplication/BLooper/BHandler
 * C shim. See hs_application.h for the design rationale (trampoline
 * pattern, threading model, Quit() ownership trap) before changing anything
 * here.
 */
#include "hs_application.h"

#include <cstddef>
#include <cstdio>

#include <Application.h>
#include <Message.h>
#include <OS.h>

#include "hs_mono_thread_attach.h"
#include "hs_mono_thread_detach.h"


namespace {

/*
 * The one and only C++ subclass in this whole binding. Every future kit
 * (Interface, Storage, ...) that needs to let C# override a virtual will
 * follow this exact same shape: one native subclass per overridable base
 * class, each override forwarding to a registered C function pointer.
 */
class HSApplication : public BApplication {
public:
	HSApplication(const char* signature, status_t* outError)
		:
		BApplication(signature, outError),
		fMessageReceivedCallback(NULL),
		fMessageReceivedUserData(NULL),
		fQuitRequestedCallback(NULL),
		fQuitRequestedUserData(NULL),
		fReadyToRunCallback(NULL),
		fReadyToRunUserData(NULL),
		fRun(false)
	{
	}

	/* FIX (KNOWN_ISSUES.md #3/#5): HSApplication had no destructor at
	 * all before this -- its own message-loop thread independently
	 * produces the identical warning (see KNOWN_ISSUES.md #3's
	 * "not window-specific" note). Guarded to run only when Run()
	 * actually happened (fRun), since hs_application_destroy() is
	 * also reachable on a never-Run() handle, on a thread that is NOT
	 * exiting -- see hs_application.h's ownership note. */
	virtual ~HSApplication()
	{
		if (fRun) {
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
		/* FIX (KNOWN_ISSUES.md #3/#5): see hs_mono_thread_attach.h. */
		hs_internal::EnsureThreadAttached();

		if (fMessageReceivedCallback != NULL) {
			fMessageReceivedCallback(fMessageReceivedUserData,
				static_cast<hs_handle>(message));
		} else {
			BApplication::MessageReceived(message);
		}
	}

	virtual bool QuitRequested()
	{
		/* FIX (KNOWN_ISSUES.md #3/#5): see hs_mono_thread_attach.h. */
		hs_internal::EnsureThreadAttached();

		if (fQuitRequestedCallback != NULL)
			return fQuitRequestedCallback(fQuitRequestedUserData) != 0;
		return BApplication::QuitRequested();
	}

	virtual void ReadyToRun()
	{
		/* FIX (KNOWN_ISSUES.md #3/#5): see hs_mono_thread_attach.h. */
		hs_internal::EnsureThreadAttached();

		if (fReadyToRunCallback != NULL)
			fReadyToRunCallback(fReadyToRunUserData);
		else
			BApplication::ReadyToRun();
	}

	void SetMessageReceivedCallback(hs_message_received_callback callback,
		void* userData)
	{
		fMessageReceivedCallback = callback;
		fMessageReceivedUserData = userData;
	}

	void SetQuitRequestedCallback(hs_quit_requested_callback callback,
		void* userData)
	{
		fQuitRequestedCallback = callback;
		fQuitRequestedUserData = userData;
	}

	void SetReadyToRunCallback(hs_ready_to_run_callback callback,
		void* userData)
	{
		fReadyToRunCallback = callback;
		fReadyToRunUserData = userData;
	}

	/* FIX (KNOWN_ISSUES.md #3/#5): called from hs_application_run_
	 * and_wait() right after Run() succeeds, so the destructor (which
	 * fires from the looper thread's own shutdown, inside that same
	 * call's wait_for_thread()) knows this thread is the one exiting. */
	void MarkRun()
	{
		fRun = true;
	}

private:
	hs_message_received_callback	fMessageReceivedCallback;
	void*							fMessageReceivedUserData;
	hs_quit_requested_callback		fQuitRequestedCallback;
	void*							fQuitRequestedUserData;
	hs_ready_to_run_callback		fReadyToRunCallback;
	void*							fReadyToRunUserData;
	bool							fRun;
};

} // namespace


hs_handle hs_application_create(const char* signature, hs_status* out_error)
{
	status_t error = B_OK;
	HSApplication* app = new HSApplication(signature, &error);

	if (error != B_OK) {
		delete app;
		if (out_error != NULL)
			*out_error = error;
		return NULL;
	}

	/* FIX (KNOWN_ISSUES.md #3/#5): this runs on the real app's own
	 * thread, which Mono already has valid domain state for (it's
	 * running this very C call from managed code) -- see
	 * hs_mono_thread_attach.h. Cheap, and only needs to happen once
	 * per process, but hs_application_create() only ever runs once in
	 * practice anyway (see KNOWN_ISSUES.md #1). */
	hs_internal::CacheRootDomainForAttach();

	if (out_error != NULL)
		*out_error = HS_OK;
	return static_cast<hs_handle>(app);
}


void hs_application_destroy(hs_handle app)
{
	/* Only ever safe to call on a handle that was NOT already run to
	 * completion -- see the ownership note in hs_application.h. */
	delete static_cast<HSApplication*>(app);
}


void hs_application_set_message_received_callback(hs_handle app,
	hs_message_received_callback callback, void* user_data)
{
	static_cast<HSApplication*>(app)->SetMessageReceivedCallback(callback, user_data);
}


void hs_application_set_quit_requested_callback(hs_handle app,
	hs_quit_requested_callback callback, void* user_data)
{
	static_cast<HSApplication*>(app)->SetQuitRequestedCallback(callback, user_data);
}


void hs_application_set_ready_to_run_callback(hs_handle app,
	hs_ready_to_run_callback callback, void* user_data)
{
	static_cast<HSApplication*>(app)->SetReadyToRunCallback(callback, user_data);
}


hs_status hs_application_run_and_wait(hs_handle app)
{
	HSApplication* realApp = static_cast<HSApplication*>(app);

	/* BLooper::Run() spawns the message-loop thread and returns its
	 * thread_id immediately -- it does NOT block. See hs_application.h's
	 * threading note: every callback fires on that spawned thread, not on
	 * whatever thread called this function. */
	thread_id looperThread = realApp->Run();
	if (looperThread < 0)
		return static_cast<hs_status>(looperThread);
	realApp->MarkRun();

	/* Block THIS thread (not the looper thread) until the looper thread
	 * exits. This is a real kernel wait (wait_for_thread), not polling.
	 * Once this returns, HSApplication has already deleted itself as part
	 * of its own thread's shutdown -- `app`/`realApp` are dangling from
	 * this point on. Do not touch them again. */
	status_t exitStatus = B_OK;
	wait_for_thread(looperThread, &exitStatus);

	return static_cast<hs_status>(exitStatus);
}


hs_status hs_application_post_message(hs_handle app, hs_handle message)
{
	return static_cast<HSApplication*>(app)->PostMessage(
		static_cast<BMessage*>(message));
}


hs_status hs_application_post_message_what(hs_handle app, uint32_t what)
{
	return static_cast<HSApplication*>(app)->PostMessage(what);
}
