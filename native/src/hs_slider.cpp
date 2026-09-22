/*
 * hs_slider.cpp -- implementation of the BSlider C shim. See hs_slider.h
 * for the design rationale before changing anything here. Follows
 * hs_text_control.cpp's trampoline shape almost exactly; read that file
 * first if you haven't.
 */
#include "hs_slider.h"

#include <cstddef>

#include <Control.h>
#include <GraphicsDefs.h>
#include <Message.h>
#include <Messenger.h>
#include <Rect.h>
#include <Slider.h>
#include <View.h>


namespace {

inline BRect ToBRect(hs_rect r)
{
	return BRect(r.left, r.top, r.right, r.bottom);
}

/* Private to this file -- never declared anywhere managed code can see.
 * Only used as the 'what' of the modification message HSSlider sends
 * itself (see hs_slider.h's "TWO DIFFERENT 'CHANGED' EVENTS" note). Any
 * four-character value that doesn't collide with a real BeAPI message
 * constant works; this one doesn't appear anywhere in headers/. */
const uint32 kModificationWhat = 'HsSm';

} // namespace


class HSSlider : public BSlider {
public:
	HSSlider(BRect frame, const char* name, const char* label,
		int32 minValue, int32 maxValue, orientation posture,
		thumb_style thumbType, uint32 resizingMode, uint32 flags)
		:
		/* Always NULL -- see hs_slider.h's "TWO DIFFERENT 'CHANGED'
		 * EVENTS" note; Invoke() is overridden below so this message is
		 * never actually posted anywhere. */
		BSlider(frame, name, label, NULL, minValue, maxValue, posture,
			thumbType, resizingMode, flags),
		fValueChangedCallback(NULL),
		fValueChangedUserData(NULL),
		fValueCommittedCallback(NULL),
		fValueCommittedUserData(NULL),
		fDestroyedCallback(NULL),
		fDestroyedUserData(NULL)
	{
		/* Wired here too, not just in AttachedToWindow() below -- same
		 * "SetTarget(this) IS CALLED TWICE" reasoning as
		 * HSTextControl's own constructor -- see hs_slider.h. */
		SetModificationMessage(new BMessage(kModificationWhat));
		SetTarget(this);
	}

	virtual ~HSSlider()
	{
		/* Same placement rationale as every other control in this
		 * binding's own destructor -- fires unconditionally, first
		 * thing in the destructor body, before any BSlider/BControl/
		 * BView teardown still has to run. */
		if (fDestroyedCallback != NULL)
			fDestroyedCallback(fDestroyedUserData);
	}

	virtual void AttachedToWindow()
	{
		/* BSlider::AttachedToWindow() does real work and must still
		 * run; SetTarget(this) is called again afterward because the
		 * constructor's own call captured a BMessenger with no valid
		 * Looper yet -- see hs_slider.h's timing note. */
		BSlider::AttachedToWindow();
		SetTarget(this);
	}

	virtual void MessageReceived(BMessage* message)
	{
		if (message->what == kModificationWhat) {
			if (fValueChangedCallback != NULL)
				fValueChangedCallback(fValueChangedUserData);
			return;
		}
		BSlider::MessageReceived(message);
	}

	virtual status_t Invoke(BMessage* /* message */ = NULL)
	{
		/* Same "replaces, doesn't precede" contract as every other
		 * control's Invoke() override in this binding -- fires once,
		 * on release, never posts anywhere. The message parameter is
		 * always NULL in practice and unused -- left unnamed to satisfy
		 * -Wall -Wextra -- but still present since this has to match
		 * BInvoker::Invoke()'s exact signature to actually override it. */
		if (fValueCommittedCallback != NULL)
			fValueCommittedCallback(fValueCommittedUserData);
		return B_OK;
	}

	void SetValueChangedCallback(hs_slider_value_changed_callback callback, void* userData)
	{
		fValueChangedCallback = callback;
		fValueChangedUserData = userData;
	}

	void SetValueCommittedCallback(hs_slider_value_committed_callback callback, void* userData)
	{
		fValueCommittedCallback = callback;
		fValueCommittedUserData = userData;
	}

	void SetDestroyedCallback(hs_slider_destroyed_callback callback, void* userData)
	{
		fDestroyedCallback = callback;
		fDestroyedUserData = userData;
	}

private:
	hs_slider_value_changed_callback fValueChangedCallback;
	void* fValueChangedUserData;
	hs_slider_value_committed_callback fValueCommittedCallback;
	void* fValueCommittedUserData;
	hs_slider_destroyed_callback fDestroyedCallback;
	void* fDestroyedUserData;
};


hs_handle hs_slider_create(hs_rect frame, const char* name, const char* label,
	int32_t min_value, int32_t max_value, uint32_t orientation,
	uint32_t thumb_style, uint32_t resizing_mode, uint32_t flags)
{
	/* Requires a live BApplication to already exist in this process --
	 * see hs_slider.h's note. This is real BeAPI behavior verified on
	 * hardware, not something this shim can or should work around. */
	return new HSSlider(ToBRect(frame), name, label, min_value, max_value,
		static_cast<::orientation>(orientation),
		static_cast<::thumb_style>(thumb_style), resizing_mode, flags);
}


void hs_slider_destroy(hs_handle slider)
{
	/* Only ever safe on a slider that isn't currently attached to a
	 * parent -- see hs_slider.h's OWNERSHIP note (via hs_view.h's,
	 * which it points to). */
	delete static_cast<HSSlider*>(slider);
}


void hs_slider_set_value_changed_callback(hs_handle slider,
	hs_slider_value_changed_callback callback, void* user_data)
{
	static_cast<HSSlider*>(slider)->SetValueChangedCallback(callback, user_data);
}


void hs_slider_set_value_committed_callback(hs_handle slider,
	hs_slider_value_committed_callback callback, void* user_data)
{
	static_cast<HSSlider*>(slider)->SetValueCommittedCallback(callback, user_data);
}


void hs_slider_set_destroyed_callback(hs_handle slider,
	hs_slider_destroyed_callback callback, void* user_data)
{
	static_cast<HSSlider*>(slider)->SetDestroyedCallback(callback, user_data);
}


void hs_slider_set_limits(hs_handle slider, int32_t minimum, int32_t maximum)
{
	static_cast<HSSlider*>(slider)->SetLimits(minimum, maximum);
}


void hs_slider_get_limits(hs_handle slider, int32_t* out_minimum, int32_t* out_maximum)
{
	int32 minimum = 0, maximum = 0;
	static_cast<HSSlider*>(slider)->GetLimits(&minimum, &maximum);
	*out_minimum = minimum;
	*out_maximum = maximum;
}


void hs_slider_set_position(hs_handle slider, float position)
{
	static_cast<HSSlider*>(slider)->SetPosition(position);
}


float hs_slider_position(hs_handle slider)
{
	return static_cast<HSSlider*>(slider)->Position();
}


uint32_t hs_slider_orientation(hs_handle slider)
{
	return static_cast<uint32_t>(static_cast<HSSlider*>(slider)->Orientation());
}


void hs_slider_set_orientation(hs_handle slider, uint32_t orientation)
{
	static_cast<HSSlider*>(slider)->SetOrientation(static_cast<::orientation>(orientation));
}


uint32_t hs_slider_style(hs_handle slider)
{
	return static_cast<uint32_t>(static_cast<HSSlider*>(slider)->Style());
}


void hs_slider_set_style(hs_handle slider, uint32_t style)
{
	static_cast<HSSlider*>(slider)->SetStyle(static_cast<thumb_style>(style));
}


void hs_slider_set_limit_labels(hs_handle slider, const char* min_label,
	const char* max_label)
{
	static_cast<HSSlider*>(slider)->SetLimitLabels(min_label, max_label);
}


const char* hs_slider_min_limit_label(hs_handle slider)
{
	return static_cast<HSSlider*>(slider)->MinLimitLabel();
}


const char* hs_slider_max_limit_label(hs_handle slider)
{
	return static_cast<HSSlider*>(slider)->MaxLimitLabel();
}


void hs_slider_set_key_increment_value(hs_handle slider, int32_t value)
{
	static_cast<HSSlider*>(slider)->SetKeyIncrementValue(value);
}


int32_t hs_slider_key_increment_value(hs_handle slider)
{
	return static_cast<HSSlider*>(slider)->KeyIncrementValue();
}


/* Completeness pass additions below -- see hs_slider.h's updated SCOPE
 * note for the hardware-verified defaults and the two genuine surprises
 * documented there before any of this was written. */

void hs_slider_set_snooze_amount(hs_handle slider, int32_t microseconds)
{
	static_cast<HSSlider*>(slider)->SetSnoozeAmount(microseconds);
}


int32_t hs_slider_snooze_amount(hs_handle slider)
{
	return static_cast<HSSlider*>(slider)->SnoozeAmount();
}


void hs_slider_set_hash_mark_count(hs_handle slider, int32_t count)
{
	static_cast<HSSlider*>(slider)->SetHashMarkCount(count);
}


int32_t hs_slider_hash_mark_count(hs_handle slider)
{
	return static_cast<HSSlider*>(slider)->HashMarkCount();
}


void hs_slider_set_hash_marks(hs_handle slider, uint32_t where)
{
	static_cast<HSSlider*>(slider)->SetHashMarks(static_cast<::hash_mark_location>(where));
}


uint32_t hs_slider_hash_marks(hs_handle slider)
{
	return static_cast<uint32_t>(static_cast<HSSlider*>(slider)->HashMarks());
}


void hs_slider_set_bar_color(hs_handle slider, uint8_t red, uint8_t green,
	uint8_t blue, uint8_t alpha)
{
	rgb_color color;
	color.red = red;
	color.green = green;
	color.blue = blue;
	color.alpha = alpha;
	static_cast<HSSlider*>(slider)->SetBarColor(color);
}


void hs_slider_bar_color(hs_handle slider, uint8_t* out_red,
	uint8_t* out_green, uint8_t* out_blue, uint8_t* out_alpha)
{
	rgb_color color = static_cast<HSSlider*>(slider)->BarColor();
	*out_red = color.red;
	*out_green = color.green;
	*out_blue = color.blue;
	*out_alpha = color.alpha;
}


void hs_slider_use_fill_color(hs_handle slider, bool use_fill, uint8_t red,
	uint8_t green, uint8_t blue, uint8_t alpha)
{
	/* Always a real, non-NULL pointer -- see hs_slider.h's own note on
	 * why this shim never needs to pass NULL here, and the surprise
	 * documented there about what use_fill=false actually does with it. */
	rgb_color color;
	color.red = red;
	color.green = green;
	color.blue = blue;
	color.alpha = alpha;
	static_cast<HSSlider*>(slider)->UseFillColor(use_fill, &color);
}


bool hs_slider_uses_fill_color(hs_handle slider)
{
	/* FillColor(NULL) -- hardware-confirmed null-safe, see hs_slider.h. */
	return static_cast<HSSlider*>(slider)->FillColor(NULL);
}


void hs_slider_fill_color(hs_handle slider, uint8_t* out_red,
	uint8_t* out_green, uint8_t* out_blue, uint8_t* out_alpha)
{
	rgb_color color;
	static_cast<HSSlider*>(slider)->FillColor(&color);
	*out_red = color.red;
	*out_green = color.green;
	*out_blue = color.blue;
	*out_alpha = color.alpha;
}


void hs_slider_set_bar_thickness(hs_handle slider, float thickness)
{
	static_cast<HSSlider*>(slider)->SetBarThickness(thickness);
}


float hs_slider_bar_thickness(hs_handle slider)
{
	return static_cast<HSSlider*>(slider)->BarThickness();
}
