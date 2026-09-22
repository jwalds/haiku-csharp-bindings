/*
 * hs_color_control.cpp -- implementation of the BColorControl C shim.
 * See hs_color_control.h for the design rationale before changing
 * anything here. Follows hs_button.cpp's trampoline shape; read that
 * file's own header comment too if this is your first kit file.
 */
#include "hs_color_control.h"

#include <cstddef>

#include <ColorControl.h>
#include <Control.h>
#include <GraphicsDefs.h>
#include <Message.h>
#include <Point.h>


class HSColorControl : public BColorControl {
public:
	HSColorControl(BPoint start, color_control_layout layout, float cellSize,
		const char* name, bool useOffscreen)
		:
		/* Always NULL -- see hs_color_control.h's "NO BMessage/BInvoker/
		 * TARGET PLUMBING" note. Invoke() is overridden below so this
		 * message is never actually posted anywhere. */
		BColorControl(start, layout, cellSize, name, NULL, useOffscreen),
		fValueChangedCallback(NULL),
		fValueChangedUserData(NULL),
		fDestroyedCallback(NULL),
		fDestroyedUserData(NULL)
	{
	}

	virtual ~HSColorControl()
	{
		/* Same placement rationale as HSButton's own destructor -- fires
		 * unconditionally, first thing in the destructor body, before
		 * any BColorControl/BControl/BView teardown still has to run. */
		if (fDestroyedCallback != NULL)
			fDestroyedCallback(fDestroyedUserData);
	}

	virtual status_t Invoke(BMessage* /* message */ = NULL)
	{
		/* Replaces (does not merely precede) BControl/BInvoker's own
		 * Invoke() -- no BMessage is ever posted to any target, since
		 * this binding exposes a color pick as a direct callback
		 * instead (see hs_color_control.h). The message parameter is
		 * always NULL in practice and unused -- left unnamed to satisfy
		 * -Wall -Wextra -- but still present since Invoke() is virtual
		 * and this has to match BInvoker::Invoke()'s exact signature to
		 * actually override it. */
		if (fValueChangedCallback != NULL)
			fValueChangedCallback(fValueChangedUserData);
		return B_OK;
	}

	void SetValueChangedCallback(hs_color_control_value_changed_callback callback,
		void* userData)
	{
		fValueChangedCallback = callback;
		fValueChangedUserData = userData;
	}

	void SetDestroyedCallback(hs_color_control_destroyed_callback callback,
		void* userData)
	{
		fDestroyedCallback = callback;
		fDestroyedUserData = userData;
	}

private:
	hs_color_control_value_changed_callback fValueChangedCallback;
	void* fValueChangedUserData;
	hs_color_control_destroyed_callback fDestroyedCallback;
	void* fDestroyedUserData;
};


hs_handle hs_color_control_create(hs_point start, uint32_t layout,
	float cell_size, const char* name, bool use_offscreen)
{
	BPoint nativeStart(start.x, start.y);
	return new HSColorControl(nativeStart,
		static_cast<color_control_layout>(layout), cell_size, name,
		use_offscreen);
}


void hs_color_control_destroy(hs_handle color_control)
{
	/* Only ever safe on a color control that isn't currently attached to
	 * a parent -- see hs_color_control.h's OWNERSHIP note (via
	 * hs_view.h's, which it points to). */
	delete static_cast<HSColorControl*>(color_control);
}


void hs_color_control_set_value_changed_callback(hs_handle color_control,
	hs_color_control_value_changed_callback callback, void* user_data)
{
	static_cast<HSColorControl*>(color_control)->SetValueChangedCallback(callback, user_data);
}


void hs_color_control_set_destroyed_callback(hs_handle color_control,
	hs_color_control_destroyed_callback callback, void* user_data)
{
	static_cast<HSColorControl*>(color_control)->SetDestroyedCallback(callback, user_data);
}


void hs_color_control_set_value_color(hs_handle color_control, uint8_t red,
	uint8_t green, uint8_t blue, uint8_t alpha)
{
	rgb_color color;
	color.red = red;
	color.green = green;
	color.blue = blue;
	color.alpha = alpha;
	static_cast<HSColorControl*>(color_control)->SetValue(color);
}


void hs_color_control_value_as_color(hs_handle color_control,
	uint8_t* out_red, uint8_t* out_green, uint8_t* out_blue,
	uint8_t* out_alpha)
{
	rgb_color color = static_cast<HSColorControl*>(color_control)->ValueAsColor();
	*out_red = color.red;
	*out_green = color.green;
	*out_blue = color.blue;
	*out_alpha = color.alpha;
}


void hs_color_control_set_cell_size(hs_handle color_control, float size)
{
	static_cast<HSColorControl*>(color_control)->SetCellSize(size);
}


float hs_color_control_cell_size(hs_handle color_control)
{
	return static_cast<HSColorControl*>(color_control)->CellSize();
}


void hs_color_control_set_layout(hs_handle color_control, uint32_t layout)
{
	static_cast<HSColorControl*>(color_control)->SetLayout(
		static_cast<color_control_layout>(layout));
}


uint32_t hs_color_control_layout(hs_handle color_control)
{
	return static_cast<uint32_t>(static_cast<HSColorControl*>(color_control)->Layout());
}
