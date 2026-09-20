/*
 * hs_view.cpp -- implementation of the BView C shim. See hs_view.h for the
 * design rationale before changing anything here. Follows
 * hs_window.cpp's trampoline shape; read that file's own header comment
 * too if this is your first kit file.
 */
#include "hs_view.h"

#include <cstddef>
#include <cstring>

#include <GraphicsDefs.h>
#include <InterfaceDefs.h>
#include <Point.h>
#include <Rect.h>
#include <View.h>


namespace {

/*
 * Third C++ subclass in this binding (after HSApplication and HSWindow).
 * Same trampoline shape, plus the same destructor override HSWindow has
 * and for the same reason (see hs_view.h's "DESTROYED CALLBACK" note).
 */
class HSView : public BView {
public:
	HSView(BRect frame, const char* name, uint32 resizingMode, uint32 flags)
		:
		BView(frame, name, resizingMode, flags),
		fAttachedToWindowCallback(NULL),
		fAttachedToWindowUserData(NULL),
		fDetachedFromWindowCallback(NULL),
		fDetachedFromWindowUserData(NULL),
		fDrawCallback(NULL),
		fDrawUserData(NULL),
		fDestroyedCallback(NULL),
		fDestroyedUserData(NULL)
	{
	}

	virtual ~HSView()
	{
		/* Fires unconditionally, no matter which death path got us here
		 * (see hs_view.h) -- deliberately the very first thing in the
		 * destructor body, before any BView teardown that base
		 * destructors still have to run, so the managed side learns the
		 * native object is on its way out as early as possible. Same
		 * placement rationale as HSWindow's destructor. */
		if (fDestroyedCallback != NULL)
			fDestroyedCallback(fDestroyedUserData);
	}

	virtual void AttachedToWindow()
	{
		if (fAttachedToWindowCallback != NULL)
			fAttachedToWindowCallback(fAttachedToWindowUserData);
		else
			BView::AttachedToWindow();
	}

	virtual void DetachedFromWindow()
	{
		if (fDetachedFromWindowCallback != NULL)
			fDetachedFromWindowCallback(fDetachedFromWindowUserData);
		else
			BView::DetachedFromWindow();
	}

	virtual void Draw(BRect updateRect)
	{
		if (fDrawCallback != NULL) {
			hs_rect r;
			r.left = updateRect.left;
			r.top = updateRect.top;
			r.right = updateRect.right;
			r.bottom = updateRect.bottom;
			fDrawCallback(fDrawUserData, r);
		} else {
			BView::Draw(updateRect);
		}
	}

	void SetAttachedToWindowCallback(hs_view_attached_to_window_callback callback,
		void* userData)
	{
		fAttachedToWindowCallback = callback;
		fAttachedToWindowUserData = userData;
	}

	void SetDetachedFromWindowCallback(hs_view_detached_from_window_callback callback,
		void* userData)
	{
		fDetachedFromWindowCallback = callback;
		fDetachedFromWindowUserData = userData;
	}

	void SetDrawCallback(hs_view_draw_callback callback, void* userData)
	{
		fDrawCallback = callback;
		fDrawUserData = userData;
	}

	void SetDestroyedCallback(hs_view_destroyed_callback callback, void* userData)
	{
		fDestroyedCallback = callback;
		fDestroyedUserData = userData;
	}

private:
	hs_view_attached_to_window_callback	fAttachedToWindowCallback;
	void*									fAttachedToWindowUserData;
	hs_view_detached_from_window_callback	fDetachedFromWindowCallback;
	void*									fDetachedFromWindowUserData;
	hs_view_draw_callback					fDrawCallback;
	void*									fDrawUserData;
	hs_view_destroyed_callback				fDestroyedCallback;
	void*									fDestroyedUserData;
};


/* Same duplication-over-cross-file-sharing tradeoff hs_window.cpp's own
 * ToBRect/ToHsRect already made -- these are one-line conversions between
 * a plain hs_types.h struct and its Haiku equivalent, not worth a shared
 * header just to avoid four lines appearing twice. */
inline BRect ToBRect(hs_rect r)
{
	return BRect(r.left, r.top, r.right, r.bottom);
}


inline BPoint ToBPoint(hs_point p)
{
	return BPoint(p.x, p.y);
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


hs_handle hs_view_create(hs_rect frame, const char* name,
	uint32_t resizing_mode, uint32_t flags)
{
	HSView* view = new HSView(ToBRect(frame), name,
		static_cast<uint32>(resizing_mode), static_cast<uint32>(flags));
	return static_cast<hs_handle>(view);
}


void hs_view_destroy(hs_handle view)
{
	/* Only ever safe on a view that isn't currently attached to a parent
	 * -- see the OWNERSHIP note in hs_view.h. */
	delete static_cast<HSView*>(view);
}


void hs_view_set_attached_to_window_callback(hs_handle view,
	hs_view_attached_to_window_callback callback, void* user_data)
{
	static_cast<HSView*>(view)->SetAttachedToWindowCallback(callback, user_data);
}


void hs_view_set_detached_from_window_callback(hs_handle view,
	hs_view_detached_from_window_callback callback, void* user_data)
{
	static_cast<HSView*>(view)->SetDetachedFromWindowCallback(callback, user_data);
}


void hs_view_set_draw_callback(hs_handle view,
	hs_view_draw_callback callback, void* user_data)
{
	static_cast<HSView*>(view)->SetDrawCallback(callback, user_data);
}


void hs_view_set_destroyed_callback(hs_handle view,
	hs_view_destroyed_callback callback, void* user_data)
{
	static_cast<HSView*>(view)->SetDestroyedCallback(callback, user_data);
}


void hs_view_add_child(hs_handle view, hs_handle child)
{
	static_cast<HSView*>(view)->AddChild(static_cast<HSView*>(child));
}


bool hs_view_remove_child(hs_handle view, hs_handle child)
{
	return static_cast<HSView*>(view)->RemoveChild(static_cast<HSView*>(child));
}


void hs_view_get_frame(hs_handle view, hs_rect* out_frame)
{
	if (out_frame == NULL)
		return;
	*out_frame = ToHsRect(static_cast<HSView*>(view)->Frame());
}


void hs_view_get_bounds(hs_handle view, hs_rect* out_bounds)
{
	if (out_bounds == NULL)
		return;
	*out_bounds = ToHsRect(static_cast<HSView*>(view)->Bounds());
}


void hs_view_move_to(hs_handle view, float x, float y)
{
	static_cast<HSView*>(view)->MoveTo(x, y);
}


void hs_view_resize_to(hs_handle view, float width, float height)
{
	static_cast<HSView*>(view)->ResizeTo(width, height);
}


void hs_view_set_high_color(hs_handle view, uint8_t red, uint8_t green,
	uint8_t blue, uint8_t alpha)
{
	static_cast<HSView*>(view)->SetHighColor(red, green, blue, alpha);
}


void hs_view_set_low_color(hs_handle view, uint8_t red, uint8_t green,
	uint8_t blue, uint8_t alpha)
{
	static_cast<HSView*>(view)->SetLowColor(red, green, blue, alpha);
}


void hs_view_set_view_color(hs_handle view, uint8_t red, uint8_t green,
	uint8_t blue, uint8_t alpha)
{
	static_cast<HSView*>(view)->SetViewColor(red, green, blue, alpha);
}


void hs_view_fill_rect(hs_handle view, hs_rect rect)
{
	static_cast<HSView*>(view)->FillRect(ToBRect(rect));
}


void hs_view_stroke_rect(hs_handle view, hs_rect rect)
{
	static_cast<HSView*>(view)->StrokeRect(ToBRect(rect));
}


void hs_view_stroke_line(hs_handle view, hs_point start, hs_point end)
{
	static_cast<HSView*>(view)->StrokeLine(ToBPoint(start), ToBPoint(end));
}


void hs_view_draw_string(hs_handle view, const char* text, hs_point location)
{
	static_cast<HSView*>(view)->DrawString(text, ToBPoint(location));
}
