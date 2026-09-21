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
#include <Message.h>
#include <Point.h>
#include <Rect.h>
#include <View.h>
#include <Window.h>


namespace {

/* Same duplication-over-cross-file-sharing tradeoff hs_window.cpp's own
 * ToBRect/ToHsRect already made -- these are one-line conversions between
 * a plain hs_types.h struct and its Haiku equivalent, not worth a shared
 * header just to avoid four lines appearing twice. Declared before HSView
 * (rather than after, where hs_view.cpp used to put this kind of helper)
 * because HSView's own MouseDown/MouseUp/MouseMoved overrides now call
 * ToHsPoint() directly. */
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


inline hs_point ToHsPoint(BPoint p)
{
	hs_point out;
	out.x = p.x;
	out.y = p.y;
	return out;
}


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
		fDestroyedUserData(NULL),
		fMouseDownCallback(NULL),
		fMouseDownUserData(NULL),
		fMouseUpCallback(NULL),
		fMouseUpUserData(NULL),
		fMouseMovedCallback(NULL),
		fMouseMovedUserData(NULL),
		fKeyDownCallback(NULL),
		fKeyDownUserData(NULL),
		fKeyUpCallback(NULL),
		fKeyUpUserData(NULL)
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

	virtual void MouseDown(BPoint where)
	{
		if (fMouseDownCallback != NULL)
			fMouseDownCallback(fMouseDownUserData, ToHsPoint(where), CurrentButtons(),
				CurrentModifiers());
		else
			BView::MouseDown(where);
	}

	virtual void MouseUp(BPoint where)
	{
		/* No buttons parameter here -- see hs_view.h's MOUSE AND KEYBOARD
		 * INPUT note for why B_MOUSE_UP genuinely carries no "buttons"
		 * field to read, unlike MouseDown/MouseMoved. It DOES carry a
		 * "modifiers" field though (the MODIFIERS note's asymmetry runs
		 * the other way from the buttons one), so this gets
		 * CurrentModifiers() where it gets no CurrentButtons(). */
		if (fMouseUpCallback != NULL)
			fMouseUpCallback(fMouseUpUserData, ToHsPoint(where), CurrentModifiers());
		else
			BView::MouseUp(where);
	}

	virtual void MouseMoved(BPoint where, uint32 transit, const BMessage* dragMessage)
	{
		/* dragMessage is always NULL here in this slice -- no drag & drop
		 * support yet (see hs_view.h). No CurrentModifiers() call here --
		 * see hs_view.h's MODIFIERS note: B_MOUSE_MOVED genuinely carries
		 * no "modifiers" field, unlike MouseDown/MouseUp/KeyDown/KeyUp.
		 * Callers who need modifier state here call hs_modifiers()
		 * directly instead. */
		if (fMouseMovedCallback != NULL) {
			fMouseMovedCallback(fMouseMovedUserData, ToHsPoint(where),
				static_cast<uint32_t>(transit), CurrentButtons());
		} else {
			BView::MouseMoved(where, transit, dragMessage);
		}
	}

	virtual void KeyDown(const char* bytes, int32 numBytes)
	{
		if (fKeyDownCallback != NULL)
			fKeyDownCallback(fKeyDownUserData, bytes, numBytes, CurrentModifiers());
		else
			BView::KeyDown(bytes, numBytes);
	}

	virtual void KeyUp(const char* bytes, int32 numBytes)
	{
		if (fKeyUpCallback != NULL)
			fKeyUpCallback(fKeyUpUserData, bytes, numBytes, CurrentModifiers());
		else
			BView::KeyUp(bytes, numBytes);
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

	void SetMouseDownCallback(hs_view_mouse_down_callback callback, void* userData)
	{
		fMouseDownCallback = callback;
		fMouseDownUserData = userData;
	}

	void SetMouseUpCallback(hs_view_mouse_up_callback callback, void* userData)
	{
		fMouseUpCallback = callback;
		fMouseUpUserData = userData;
	}

	void SetMouseMovedCallback(hs_view_mouse_moved_callback callback, void* userData)
	{
		fMouseMovedCallback = callback;
		fMouseMovedUserData = userData;
	}

	void SetKeyDownCallback(hs_view_key_down_callback callback, void* userData)
	{
		fKeyDownCallback = callback;
		fKeyDownUserData = userData;
	}

	void SetKeyUpCallback(hs_view_key_up_callback callback, void* userData)
	{
		fKeyUpCallback = callback;
		fKeyUpUserData = userData;
	}

private:
	/* Pulls the "buttons" field out of the message currently being
	 * dispatched -- see hs_view.h's MOUSE AND KEYBOARD INPUT note for why
	 * that's the right (and only) way to learn this for MouseDown/
	 * MouseMoved, and why MouseUp gets no such helper call at all. */
	uint32_t CurrentButtons()
	{
		BWindow* window = Window();
		if (window == NULL)
			return 0;
		BMessage* message = window->CurrentMessage();
		if (message == NULL)
			return 0;
		int32 buttons = 0;
		message->FindInt32("buttons", &buttons);
		return static_cast<uint32_t>(buttons);
	}

	/* Same mechanism as CurrentButtons() above, pulling "modifiers"
	 * instead of "buttons" -- see hs_view.h's MODIFIERS note for which
	 * four hooks actually have this field to read (MouseMoved doesn't,
	 * so it never calls this). */
	uint32_t CurrentModifiers()
	{
		BWindow* window = Window();
		if (window == NULL)
			return 0;
		BMessage* message = window->CurrentMessage();
		if (message == NULL)
			return 0;
		int32 modifiers = 0;
		message->FindInt32("modifiers", &modifiers);
		return static_cast<uint32_t>(modifiers);
	}

	hs_view_attached_to_window_callback	fAttachedToWindowCallback;
	void*									fAttachedToWindowUserData;
	hs_view_detached_from_window_callback	fDetachedFromWindowCallback;
	void*									fDetachedFromWindowUserData;
	hs_view_draw_callback					fDrawCallback;
	void*									fDrawUserData;
	hs_view_destroyed_callback				fDestroyedCallback;
	void*									fDestroyedUserData;
	hs_view_mouse_down_callback				fMouseDownCallback;
	void*									fMouseDownUserData;
	hs_view_mouse_up_callback				fMouseUpCallback;
	void*									fMouseUpUserData;
	hs_view_mouse_moved_callback			fMouseMovedCallback;
	void*									fMouseMovedUserData;
	hs_view_key_down_callback				fKeyDownCallback;
	void*									fKeyDownUserData;
	hs_view_key_up_callback					fKeyUpCallback;
	void*									fKeyUpUserData;
};


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


void hs_view_set_mouse_down_callback(hs_handle view,
	hs_view_mouse_down_callback callback, void* user_data)
{
	static_cast<HSView*>(view)->SetMouseDownCallback(callback, user_data);
}


void hs_view_set_mouse_up_callback(hs_handle view,
	hs_view_mouse_up_callback callback, void* user_data)
{
	static_cast<HSView*>(view)->SetMouseUpCallback(callback, user_data);
}


void hs_view_set_mouse_moved_callback(hs_handle view,
	hs_view_mouse_moved_callback callback, void* user_data)
{
	static_cast<HSView*>(view)->SetMouseMovedCallback(callback, user_data);
}


void hs_view_set_key_down_callback(hs_handle view,
	hs_view_key_down_callback callback, void* user_data)
{
	static_cast<HSView*>(view)->SetKeyDownCallback(callback, user_data);
}


void hs_view_set_key_up_callback(hs_handle view,
	hs_view_key_up_callback callback, void* user_data)
{
	static_cast<HSView*>(view)->SetKeyUpCallback(callback, user_data);
}


void hs_view_add_child(hs_handle view, hs_handle child)
{
	/* `child` may be an HSView* OR any other HSView-family handle this
	 * binding creates (as of the Button/Control slice, that also means
	 * HSButton* -- see hs_button.cpp). Casting straight to BView*, same
	 * as hs_window_add_child() already does below, rather than the
	 * narrower HSView*: every such class inherits BView (directly or,
	 * for HSButton via BButton -> BControl, transitively) as its first,
	 * non-virtual base, which the Itanium C++ ABI this binding builds
	 * under places at offset 0 -- verified empirically with a small
	 * scratch probe on real Haiku hardware for the HSButton case
	 * (offsets: BView/BControl/BButton all 0, BInvoker 272 -- see
	 * KNOWN_ISSUES.md or the Button/Control section of README.md for
	 * the probe itself), matching the reasoning hs_window_add_child()'s
	 * own comment already gives for HSView. AddChild()'s signature only
	 * ever wants a BView*, so this is the correct, minimal cast for
	 * "any concrete view-family type", not just HSView specifically. */
	static_cast<HSView*>(view)->AddChild(static_cast<BView*>(child));
}


bool hs_view_remove_child(hs_handle view, hs_handle child)
{
	/* Same BView* reasoning as hs_view_add_child() just above. */
	return static_cast<HSView*>(view)->RemoveChild(static_cast<BView*>(child));
}


void hs_view_get_frame(hs_handle view, hs_rect* out_frame)
{
	if (out_frame == NULL)
		return;
	/* BView* cast, not the narrower HSView* -- see hs_view_add_child()'s
	 * comment above. Reused directly by Control.cs/Button.cs (there is
	 * no separate hs_button_get_frame()) since this only ever touches
	 * plain inherited BView state. */
	*out_frame = ToHsRect(static_cast<BView*>(view)->Frame());
}


void hs_view_get_bounds(hs_handle view, hs_rect* out_bounds)
{
	if (out_bounds == NULL)
		return;
	*out_bounds = ToHsRect(static_cast<HSView*>(view)->Bounds());
}


void hs_view_move_to(hs_handle view, float x, float y)
{
	/* BView* cast -- same reasoning/reuse note as hs_view_get_frame()
	 * just above. */
	static_cast<BView*>(view)->MoveTo(x, y);
}


void hs_view_resize_to(hs_handle view, float width, float height)
{
	/* BView* cast -- same reasoning/reuse note as hs_view_get_frame()
	 * above. */
	static_cast<BView*>(view)->ResizeTo(width, height);
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


void hs_view_invalidate(hs_handle view)
{
	static_cast<HSView*>(view)->Invalidate();
}


void hs_view_make_focus(hs_handle view, bool focus)
{
	static_cast<HSView*>(view)->MakeFocus(focus);
}


bool hs_view_is_focus(hs_handle view)
{
	return static_cast<HSView*>(view)->IsFocus();
}


uint32_t hs_modifiers(void)
{
	return static_cast<uint32_t>(modifiers());
}
