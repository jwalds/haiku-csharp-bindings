/*
 * hs_checkbox.cpp -- implementation of the BCheckBox C shim. See
 * hs_checkbox.h for the design rationale before changing anything here.
 * Follows hs_button.cpp's trampoline shape almost exactly; read that file
 * first if you haven't.
 */
#include "hs_checkbox.h"

#include <cstddef>

#include <CheckBox.h>
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


class HSCheckBox : public BCheckBox {
public:
	HSCheckBox(BRect frame, const char* name, const char* label,
		uint32 resizingMode, uint32 flags)
		:
		/* Always NULL -- see hs_checkbox.h's "NO BMessage/BInvoker/TARGET
		 * PLUMBING" note. Invoke() is overridden below so this message
		 * is never actually posted anywhere. */
		BCheckBox(frame, name, label, NULL, resizingMode, flags),
		fClickCallback(NULL),
		fClickUserData(NULL),
		fDestroyedCallback(NULL),
		fDestroyedUserData(NULL)
	{
	}

	virtual ~HSCheckBox()
	{
		/* Same placement rationale as HSButton's own destructor -- fires
		 * unconditionally, first thing in the destructor body, before
		 * any BCheckBox/BControl/BView teardown still has to run. */
		if (fDestroyedCallback != NULL)
			fDestroyedCallback(fDestroyedUserData);
	}

	virtual status_t Invoke(BMessage* /* message */ = NULL)
	{
		/* Replaces (does not merely precede) BControl/BInvoker's own
		 * Invoke() -- see hs_checkbox.h. By the time this runs,
		 * BCheckBox's own MouseUp()/KeyDown() has already toggled
		 * Value(), so the callback sees the new state via
		 * hs_control_value(), same as hs_button.h's Invoke() override. */
		if (fClickCallback != NULL)
			fClickCallback(fClickUserData);
		return B_OK;
	}

	void SetClickCallback(hs_checkbox_click_callback callback, void* userData)
	{
		fClickCallback = callback;
		fClickUserData = userData;
	}

	void SetDestroyedCallback(hs_checkbox_destroyed_callback callback, void* userData)
	{
		fDestroyedCallback = callback;
		fDestroyedUserData = userData;
	}

private:
	hs_checkbox_click_callback fClickCallback;
	void* fClickUserData;
	hs_checkbox_destroyed_callback fDestroyedCallback;
	void* fDestroyedUserData;
};


hs_handle hs_checkbox_create(hs_rect frame, const char* name, const char* label,
	uint32_t resizing_mode, uint32_t flags)
{
	return new HSCheckBox(ToBRect(frame), name, label, resizing_mode, flags);
}


void hs_checkbox_destroy(hs_handle checkbox)
{
	/* Only ever safe on a checkbox that isn't currently attached to a
	 * parent -- see hs_checkbox.h's OWNERSHIP note (via hs_view.h's,
	 * which it points to). */
	delete static_cast<HSCheckBox*>(checkbox);
}


void hs_checkbox_set_click_callback(hs_handle checkbox,
	hs_checkbox_click_callback callback, void* user_data)
{
	static_cast<HSCheckBox*>(checkbox)->SetClickCallback(callback, user_data);
}


void hs_checkbox_set_destroyed_callback(hs_handle checkbox,
	hs_checkbox_destroyed_callback callback, void* user_data)
{
	static_cast<HSCheckBox*>(checkbox)->SetDestroyedCallback(callback, user_data);
}
