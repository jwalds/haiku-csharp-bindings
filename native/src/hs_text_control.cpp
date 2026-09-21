/*
 * hs_text_control.cpp -- implementation of the BTextControl C shim. See
 * hs_text_control.h for the design rationale before changing anything
 * here. Follows hs_button.cpp's trampoline shape; read that file's
 * header comment too if this is your first kit file.
 */
#include "hs_text_control.h"

#include <cstddef>

#include <Control.h>
#include <Message.h>
#include <Messenger.h>
#include <Rect.h>
#include <TextControl.h>
#include <View.h>


namespace {

inline BRect ToBRect(hs_rect r)
{
	return BRect(r.left, r.top, r.right, r.bottom);
}

/* Private to this file -- never declared anywhere managed code can see.
 * Only used as the 'what' of the modification message HSTextControl
 * sends itself (see hs_text_control.h's "TWO DIFFERENT 'CHANGED' EVENTS"
 * note). Any four-character value that doesn't collide with a real
 * BeAPI message constant works; this one doesn't appear anywhere in
 * headers/. */
const uint32 kModificationWhat = 'HsTm';

} // namespace


class HSTextControl : public BTextControl {
public:
	HSTextControl(BRect frame, const char* name, const char* label,
		const char* text, uint32 resizingMode, uint32 flags)
		:
		/* Always NULL -- see hs_text_control.h's "TWO DIFFERENT
		 * 'CHANGED' EVENTS" note; Invoke() is overridden below so this
		 * message is never actually posted anywhere. */
		BTextControl(frame, name, label, text, NULL, resizingMode, flags),
		fTextChangedCallback(NULL),
		fTextChangedUserData(NULL),
		fTextCommittedCallback(NULL),
		fTextCommittedUserData(NULL),
		fDestroyedCallback(NULL),
		fDestroyedUserData(NULL)
	{
		/* Wired here too, not just in AttachedToWindow() below -- see
		 * hs_text_control.h's "SetTarget(this) IS CALLED TWICE" note
		 * for why both calls exist. */
		SetModificationMessage(new BMessage(kModificationWhat));
		SetTarget(this);
	}

	virtual ~HSTextControl()
	{
		/* Same placement rationale as HSButton's/HSView's own
		 * destructors -- fires unconditionally, first thing in the
		 * destructor body, before any BTextControl/BControl/BView
		 * teardown still has to run. */
		if (fDestroyedCallback != NULL)
			fDestroyedCallback(fDestroyedUserData);
	}

	virtual void AttachedToWindow()
	{
		/* Unlike HSButton, BTextControl's real AttachedToWindow() does
		 * meaningful layout work and must still run. SetTarget(this) is
		 * called again afterward because the constructor's own call
		 * captured a BMessenger with no valid Looper yet -- see
		 * hs_text_control.h's timing note. */
		BTextControl::AttachedToWindow();
		SetTarget(this);
	}

	virtual void MessageReceived(BMessage* message)
	{
		if (message->what == kModificationWhat) {
			if (fTextChangedCallback != NULL)
				fTextChangedCallback(fTextChangedUserData);
			return;
		}
		BTextControl::MessageReceived(message);
	}

	virtual status_t Invoke(BMessage* /* message */ = NULL)
	{
		/* Same "replaces, doesn't precede" contract as
		 * HSButton::Invoke() (see hs_button.cpp) -- fires on commit
		 * (Enter, or focus lost after an edit), never posts anywhere.
		 * The message parameter is always NULL in practice and unused
		 * -- left unnamed to satisfy -Wall -Wextra -- but still present
		 * since this has to match BInvoker::Invoke()'s exact signature
		 * to actually override it. */
		if (fTextCommittedCallback != NULL)
			fTextCommittedCallback(fTextCommittedUserData);
		return B_OK;
	}

	void SetTextChangedCallback(hs_text_control_text_changed_callback callback,
		void* userData)
	{
		fTextChangedCallback = callback;
		fTextChangedUserData = userData;
	}

	void SetTextCommittedCallback(hs_text_control_text_committed_callback callback,
		void* userData)
	{
		fTextCommittedCallback = callback;
		fTextCommittedUserData = userData;
	}

	void SetDestroyedCallback(hs_text_control_destroyed_callback callback,
		void* userData)
	{
		fDestroyedCallback = callback;
		fDestroyedUserData = userData;
	}

private:
	hs_text_control_text_changed_callback fTextChangedCallback;
	void* fTextChangedUserData;
	hs_text_control_text_committed_callback fTextCommittedCallback;
	void* fTextCommittedUserData;
	hs_text_control_destroyed_callback fDestroyedCallback;
	void* fDestroyedUserData;
};


hs_handle hs_text_control_create(hs_rect frame, const char* name,
	const char* label, const char* text, uint32_t resizing_mode,
	uint32_t flags)
{
	return new HSTextControl(ToBRect(frame), name, label, text,
		resizing_mode, flags);
}


void hs_text_control_destroy(hs_handle text_control)
{
	/* Only ever safe on a text control that isn't currently attached to
	 * a parent -- see hs_text_control.h's OWNERSHIP note (via
	 * hs_view.h's, which it points to). */
	delete static_cast<HSTextControl*>(text_control);
}


void hs_text_control_set_text_changed_callback(hs_handle text_control,
	hs_text_control_text_changed_callback callback, void* user_data)
{
	static_cast<HSTextControl*>(text_control)->SetTextChangedCallback(
		callback, user_data);
}


void hs_text_control_set_text_committed_callback(hs_handle text_control,
	hs_text_control_text_committed_callback callback, void* user_data)
{
	static_cast<HSTextControl*>(text_control)->SetTextCommittedCallback(
		callback, user_data);
}


void hs_text_control_set_destroyed_callback(hs_handle text_control,
	hs_text_control_destroyed_callback callback, void* user_data)
{
	static_cast<HSTextControl*>(text_control)->SetDestroyedCallback(
		callback, user_data);
}


void hs_text_control_set_text(hs_handle text_control, const char* text)
{
	static_cast<HSTextControl*>(text_control)->SetText(text);
}


const char* hs_text_control_text(hs_handle text_control)
{
	return static_cast<HSTextControl*>(text_control)->Text();
}
