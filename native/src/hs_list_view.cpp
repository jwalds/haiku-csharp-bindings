/*
 * hs_list_view.cpp -- implementation of the BListView C shim. See
 * hs_list_view.h for the design rationale before changing anything here.
 * Follows hs_button.cpp's trampoline shape; read that file's header
 * comment too if this is your first kit file.
 */
#include "hs_list_view.h"

#include <cstddef>

#include <ListView.h>
#include <StringItem.h>
#include <Message.h>
#include <Rect.h>
#include <View.h>


namespace {

inline BRect ToBRect(hs_rect r)
{
	return BRect(r.left, r.top, r.right, r.bottom);
}

/* Deletes every item still in list -- see hs_list_view.h's OWNERSHIP
 * note. Every item this shim ever adds is a BStringItem it created
 * itself (see hs_list_view_add_item/add_item_at below), so this cast is
 * this shim's own invariant, not a general assumption about BListView's
 * contents. */
void DeleteAllItems(BListView* list)
{
	int32 count = list->CountItems();
	for (int32 i = 0; i < count; i++)
		delete static_cast<BStringItem*>(list->ItemAt(i));
}

} // namespace


class HSListView : public BListView {
public:
	HSListView(BRect frame, const char* name, list_view_type type,
		uint32 resizingMode, uint32 flags)
		:
		BListView(frame, name, type, resizingMode, flags),
		fSelectionChangedCallback(NULL),
		fSelectionChangedUserData(NULL),
		fInvokedCallback(NULL),
		fInvokedUserData(NULL),
		fDestroyedCallback(NULL),
		fDestroyedUserData(NULL)
	{
	}

	virtual ~HSListView()
	{
		/* Close the real, hardware-verified leak: neither MakeEmpty()
		 * nor plain ~BListView() ever deletes remaining items (see
		 * hs_list_view.h's OWNERSHIP note) -- this shim is the only
		 * thing that ever created them, so it deletes them here, before
		 * BListView's own base-class teardown runs. */
		DeleteAllItems(this);

		/* Same placement rationale as HSButton's own destructor -- fires
		 * unconditionally, first thing in the destructor body. */
		if (fDestroyedCallback != NULL)
			fDestroyedCallback(fDestroyedUserData);
	}

	virtual void SelectionChanged()
	{
		if (fSelectionChangedCallback != NULL)
			fSelectionChangedCallback(fSelectionChangedUserData);
		BListView::SelectionChanged();
	}

	virtual status_t Invoke(BMessage* /* message */ = NULL)
	{
		/* Same "replaces, not merely precedes" shape as HSButton's own
		 * Invoke() override -- see hs_list_view.h's own note. */
		if (fInvokedCallback != NULL)
			fInvokedCallback(fInvokedUserData);
		return B_OK;
	}

	void SetSelectionChangedCallback(hs_list_view_selection_changed_callback callback,
		void* userData)
	{
		fSelectionChangedCallback = callback;
		fSelectionChangedUserData = userData;
	}

	void SetInvokedCallback(hs_list_view_invoked_callback callback, void* userData)
	{
		fInvokedCallback = callback;
		fInvokedUserData = userData;
	}

	void SetDestroyedCallback(hs_list_view_destroyed_callback callback, void* userData)
	{
		fDestroyedCallback = callback;
		fDestroyedUserData = userData;
	}

private:
	hs_list_view_selection_changed_callback fSelectionChangedCallback;
	void* fSelectionChangedUserData;
	hs_list_view_invoked_callback fInvokedCallback;
	void* fInvokedUserData;
	hs_list_view_destroyed_callback fDestroyedCallback;
	void* fDestroyedUserData;
};


hs_handle hs_list_view_create(hs_rect frame, const char* name,
	uint32_t list_type, uint32_t resizing_mode, uint32_t flags)
{
	return new HSListView(ToBRect(frame), name,
		static_cast<list_view_type>(list_type), resizing_mode, flags);
}


void hs_list_view_destroy(hs_handle list_view)
{
	delete static_cast<HSListView*>(list_view);
}


void hs_list_view_set_selection_changed_callback(hs_handle list_view,
	hs_list_view_selection_changed_callback callback, void* user_data)
{
	static_cast<HSListView*>(list_view)->SetSelectionChangedCallback(callback, user_data);
}


void hs_list_view_set_invoked_callback(hs_handle list_view,
	hs_list_view_invoked_callback callback, void* user_data)
{
	static_cast<HSListView*>(list_view)->SetInvokedCallback(callback, user_data);
}


void hs_list_view_set_destroyed_callback(hs_handle list_view,
	hs_list_view_destroyed_callback callback, void* user_data)
{
	static_cast<HSListView*>(list_view)->SetDestroyedCallback(callback, user_data);
}


void hs_list_view_add_item(hs_handle list_view, const char* text)
{
	static_cast<HSListView*>(list_view)->AddItem(new BStringItem(text));
}


void hs_list_view_add_item_at(hs_handle list_view, const char* text, int32_t index)
{
	static_cast<HSListView*>(list_view)->AddItem(new BStringItem(text), index);
}


bool hs_list_view_remove_item_at(hs_handle list_view, int32_t index)
{
	BListItem* removed = static_cast<HSListView*>(list_view)->RemoveItem(index);
	if (removed == NULL)
		return false;
	delete static_cast<BStringItem*>(removed);
	return true;
}


void hs_list_view_make_empty(hs_handle list_view)
{
	HSListView* view = static_cast<HSListView*>(list_view);
	DeleteAllItems(view);
	view->MakeEmpty();
}


int32_t hs_list_view_count_items(hs_handle list_view)
{
	return static_cast<HSListView*>(list_view)->CountItems();
}


const char* hs_list_view_item_text(hs_handle list_view, int32_t index)
{
	BListItem* item = static_cast<HSListView*>(list_view)->ItemAt(index);
	if (item == NULL)
		return NULL;
	return static_cast<BStringItem*>(item)->Text();
}


void hs_list_view_set_item_text(hs_handle list_view, int32_t index, const char* text)
{
	BListItem* item = static_cast<HSListView*>(list_view)->ItemAt(index);
	if (item == NULL)
		return;
	static_cast<BStringItem*>(item)->SetText(text);
}


void hs_list_view_select(hs_handle list_view, int32_t index, bool extend)
{
	static_cast<HSListView*>(list_view)->Select(index, extend);
}


void hs_list_view_deselect(hs_handle list_view, int32_t index)
{
	static_cast<HSListView*>(list_view)->Deselect(index);
}


void hs_list_view_deselect_all(hs_handle list_view)
{
	static_cast<HSListView*>(list_view)->DeselectAll();
}


bool hs_list_view_is_item_selected(hs_handle list_view, int32_t index)
{
	return static_cast<HSListView*>(list_view)->IsItemSelected(index);
}


int32_t hs_list_view_current_selection(hs_handle list_view, int32_t selection_index)
{
	return static_cast<HSListView*>(list_view)->CurrentSelection(selection_index);
}


void hs_list_view_set_list_type(hs_handle list_view, uint32_t type)
{
	static_cast<HSListView*>(list_view)->SetListType(static_cast<list_view_type>(type));
}


uint32_t hs_list_view_list_type(hs_handle list_view)
{
	return static_cast<uint32_t>(static_cast<HSListView*>(list_view)->ListType());
}
