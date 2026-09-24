/*
 * hs_scroll_view.cpp -- implementation of the BScrollView C shim. See
 * hs_scroll_view.h for the design rationale before changing anything
 * here. Follows hs_checkbox.cpp's trampoline shape; read that file first
 * if you haven't.
 */
#include "hs_scroll_view.h"

#include <ScrollView.h>
#include <View.h>


class HSScrollView : public BScrollView {
public:
	HSScrollView(const char* name, BView* target, uint32 resizingMode,
		uint32 flags, bool horizontal, bool vertical, border_style border)
		:
		/* Real BScrollView constructor -- its own _Init() reparents
		 * `target` internally (see hs_scroll_view.h's WRAPPING AND
		 * REPARENTING note); nothing else needed here to make that
		 * happen. */
		BScrollView(name, target, resizingMode, flags, horizontal,
			vertical, border),
		fDestroyedCallback(NULL),
		fDestroyedUserData(NULL)
	{
	}

	virtual ~HSScrollView()
	{
		/* Same placement rationale as every other HS* destructor in this
		 * binding -- fires unconditionally, first thing in the
		 * destructor body, before any BScrollView/BView teardown (which
		 * recursively deletes the still-attached target and scrollbar(s),
		 * cascading their own destroyed callbacks) has run. */
		if (fDestroyedCallback != NULL)
			fDestroyedCallback(fDestroyedUserData);
	}

	void SetDestroyedCallback(hs_scroll_view_destroyed_callback callback,
		void* userData)
	{
		fDestroyedCallback = callback;
		fDestroyedUserData = userData;
	}

private:
	hs_scroll_view_destroyed_callback fDestroyedCallback;
	void* fDestroyedUserData;
};


hs_handle hs_scroll_view_create(hs_handle target, const char* name,
	uint32_t resizing_mode, uint32_t flags, bool horizontal, bool vertical,
	uint32_t border)
{
	/* Parameter deliberately NOT named `border_style` -- that identifier
	 * is itself the real BeAPI enum type name (border_style), and a
	 * same-named parameter would hide the type within this function,
	 * breaking the static_cast below. */
	return new HSScrollView(name, static_cast<BView*>(target),
		resizing_mode, flags, horizontal, vertical,
		static_cast<border_style>(border));
}


void hs_scroll_view_destroy(hs_handle scroll_view)
{
	/* Only ever safe on a scroll view that isn't currently attached to a
	 * parent -- see hs_scroll_view.h's OWNERSHIP note. */
	delete static_cast<HSScrollView*>(scroll_view);
}


void hs_scroll_view_set_destroyed_callback(hs_handle scroll_view,
	hs_scroll_view_destroyed_callback callback, void* user_data)
{
	static_cast<HSScrollView*>(scroll_view)->SetDestroyedCallback(callback,
		user_data);
}
