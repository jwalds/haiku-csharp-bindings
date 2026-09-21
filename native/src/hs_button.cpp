/*
 * hs_button.cpp -- implementation of the BButton C shim. See hs_button.h
 * for the design rationale before changing anything here. Follows
 * hs_view.cpp's/hs_window.cpp's trampoline shape; read those files'
 * header comments too if this is your first kit file.
 */
#include "hs_button.h"

#include <cstddef>

#include <Button.h>
#include <Control.h>
#include <Message.h>
#include <Rect.h>
#include <View.h>


namespace {

inline BRect ToBRect(hs_rect r)
{
	return BRect(r.left, r.top, r.right, r.bottom);
}

} // namespace


class HSButton : public BButton {
public:
	HSButton(BRect frame, const char* name, const char* label,
		uint32 resizingMode, uint32 flags)
		:
		/* Always NULL -- see hs_button.h's "NO BMessage/BInvoker/TARGET
		 * PLUMBING" note. Invoke() is overridden below so this message
		 * is never actually posted anywhere. */
		BButton(frame, name, label, NULL, resizingMode, flags),
		fClickCallback(NULL),
		fClickUserData(NULL),
		fDestroyedCallback(NULL),
		fDestroyedUserData(NULL)
	{
	}

	virtual ~HSButton()
	{
		/* Same placement rationale as HSView's/HSWindow's own destructors
		 * -- fires unconditionally, first thing in the destructor body,
		 * before any BButton/BControl/BView teardown still has to run. */
		if (fDestroyedCallback != NULL)
			fDestroyedCallback(fDestroyedUserData);
	}

	virtual status_t Invoke(BMessage* /* message */ = NULL)
	{
		/* Replaces (does not merely precede) BControl/BInvoker's own
		 * Invoke() -- no BMessage is ever posted to any target, since
		 * this binding exposes clicks as a direct callback instead (see
		 * hs_button.h). The message parameter is always NULL in
		 * practice (nothing in this binding ever calls Invoke() with an
		 * explicit message) and unused -- left unnamed to satisfy -Wall
		 * -Wextra -- but still present since Invoke() is virtual and
		 * this has to match BInvoker::Invoke()'s exact signature to
		 * actually override it. */
		if (fClickCallback != NULL)
			fClickCallback(fClickUserData);
		return B_OK;
	}

	void SetClickCallback(hs_button_click_callback callback, void* userData)
	{
		fClickCallback = callback;
		fClickUserData = userData;
	}

	void SetDestroyedCallback(hs_button_destroyed_callback callback, void* userData)
	{
		fDestroyedCallback = callback;
		fDestroyedUserData = userData;
	}

private:
	hs_button_click_callback fClickCallback;
	void* fClickUserData;
	hs_button_destroyed_callback fDestroyedCallback;
	void* fDestroyedUserData;
};


hs_handle hs_button_create(hs_rect frame, const char* name, const char* label,
	uint32_t resizing_mode, uint32_t flags)
{
	return new HSButton(ToBRect(frame), name, label, resizing_mode, flags);
}


void hs_button_destroy(hs_handle button)
{
	/* Only ever safe on a button that isn't currently attached to a
	 * parent -- see hs_button.h's OWNERSHIP note (via hs_view.h's, which
	 * it points to). */
	delete static_cast<HSButton*>(button);
}


void hs_button_set_click_callback(hs_handle button,
	hs_button_click_callback callback, void* user_data)
{
	static_cast<HSButton*>(button)->SetClickCallback(callback, user_data);
}


void hs_button_set_destroyed_callback(hs_handle button,
	hs_button_destroyed_callback callback, void* user_data)
{
	static_cast<HSButton*>(button)->SetDestroyedCallback(callback, user_data);
}


void hs_button_make_default(hs_handle button, bool is_default)
{
	static_cast<HSButton*>(button)->MakeDefault(is_default);
}


bool hs_button_is_default(hs_handle button)
{
	return static_cast<HSButton*>(button)->IsDefault();
}


void hs_button_set_flat(hs_handle button, bool flat)
{
	static_cast<HSButton*>(button)->SetFlat(flat);
}


bool hs_button_is_flat(hs_handle button)
{
	return static_cast<HSButton*>(button)->IsFlat();
}


void hs_button_set_behavior(hs_handle button, uint32_t behavior)
{
	static_cast<HSButton*>(button)->SetBehavior(
		static_cast<BButton::BBehavior>(behavior));
}


uint32_t hs_button_behavior(hs_handle button)
{
	return static_cast<uint32_t>(static_cast<HSButton*>(button)->Behavior());
}
