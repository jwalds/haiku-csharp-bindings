/*
 * hs_radio_button.cpp -- implementation of the BRadioButton C shim. See
 * hs_radio_button.h for the design rationale before changing anything
 * here. Follows hs_checkbox.cpp's trampoline shape exactly; read that
 * file first if you haven't. There is deliberately no grouping logic
 * anywhere in this file -- see hs_radio_button.h's grouping note for why.
 */
#include "hs_radio_button.h"

#include <cstddef>

#include <RadioButton.h>
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


class HSRadioButton : public BRadioButton {
public:
	HSRadioButton(BRect frame, const char* name, const char* label,
		uint32 resizingMode, uint32 flags)
		:
		/* Always NULL -- see hs_radio_button.h's "NO BMessage/BInvoker/
		 * TARGET PLUMBING" note. Invoke() is overridden below so this
		 * message is never actually posted anywhere. */
		BRadioButton(frame, name, label, NULL, resizingMode, flags),
		fClickCallback(NULL),
		fClickUserData(NULL),
		fDestroyedCallback(NULL),
		fDestroyedUserData(NULL)
	{
	}

	virtual ~HSRadioButton()
	{
		/* Same placement rationale as HSCheckBox's/HSButton's own
		 * destructors -- fires unconditionally, first thing in the
		 * destructor body, before any BRadioButton/BControl/BView
		 * teardown still has to run. */
		if (fDestroyedCallback != NULL)
			fDestroyedCallback(fDestroyedUserData);
	}

	virtual status_t Invoke(BMessage* /* message */ = NULL)
	{
		/* Replaces (does not merely precede) BControl/BInvoker's own
		 * Invoke() -- see hs_radio_button.h. By the time this runs,
		 * BRadioButton's own MouseUp()/KeyDown() has already toggled
		 * Value() and deactivated any sibling radio buttons under the
		 * same parent View, so the callback sees the fully-settled new
		 * state via hs_control_value(). */
		if (fClickCallback != NULL)
			fClickCallback(fClickUserData);
		return B_OK;
	}

	void SetClickCallback(hs_radio_button_click_callback callback, void* userData)
	{
		fClickCallback = callback;
		fClickUserData = userData;
	}

	void SetDestroyedCallback(hs_radio_button_destroyed_callback callback, void* userData)
	{
		fDestroyedCallback = callback;
		fDestroyedUserData = userData;
	}

private:
	hs_radio_button_click_callback fClickCallback;
	void* fClickUserData;
	hs_radio_button_destroyed_callback fDestroyedCallback;
	void* fDestroyedUserData;
};


hs_handle hs_radio_button_create(hs_rect frame, const char* name, const char* label,
	uint32_t resizing_mode, uint32_t flags)
{
	/* Requires a live BApplication to already exist in this process --
	 * see hs_radio_button.h's note. This is real BeAPI behavior verified
	 * on hardware, not something this shim can or should work around. */
	return new HSRadioButton(ToBRect(frame), name, label, resizing_mode, flags);
}


void hs_radio_button_destroy(hs_handle radio_button)
{
	/* Only ever safe on a radio button that isn't currently attached to
	 * a parent -- see hs_radio_button.h's OWNERSHIP note (via
	 * hs_view.h's, which it points to). */
	delete static_cast<HSRadioButton*>(radio_button);
}


void hs_radio_button_set_click_callback(hs_handle radio_button,
	hs_radio_button_click_callback callback, void* user_data)
{
	static_cast<HSRadioButton*>(radio_button)->SetClickCallback(callback, user_data);
}


void hs_radio_button_set_destroyed_callback(hs_handle radio_button,
	hs_radio_button_destroyed_callback callback, void* user_data)
{
	static_cast<HSRadioButton*>(radio_button)->SetDestroyedCallback(callback, user_data);
}
