/*
 * hs_list_view.h -- C shim over BListView (headers/os/interface/
 * ListView.h), backed by BStringItem (headers/os/interface/StringItem.h)
 * for its items. Same trampoline shape as hs_button.h -- read that file
 * first if you haven't. This comment covers only what's actually
 * different about BListView.
 *
 * NOT A BControl -- BView PLUS BInvoker, MULTIPLE INHERITANCE, VERIFIED
 * FOR THE FIRST TIME IN THIS BINDING
 * --------------------------------------------------------------------------
 * Every widget this binding has wrapped so far (Button, TextControl,
 * CheckBox, RadioButton, Slider, ColorControl) derives from BControl,
 * itself a single, non-virtual BView subclass. Real BListView does not:
 * `class BListView : public BView, public BInvoker`. A probe confirmed
 * BView is still the first base and still sits at offset 0 (casting a
 * live ProbeListView* to BView* produced the identical pointer value,
 * and Frame() read through either path matched exactly) -- so
 * hs_view_get_frame()/move_to()/resize_to()/add_child()/remove_child()
 * are reused unchanged, same as every other widget. BInvoker itself is
 * NOT at offset 0 (a second base class never is under the Itanium C++
 * ABI this binding builds under) -- this shim never blind-casts to
 * BInvoker*, and never needs to: SelectionChanged()/Invoke() are called
 * through HSListView's own real vtable (see below), not through a
 * generic cast, so the BInvoker subobject's actual offset never matters
 * here.
 *
 * NO BApplication REQUIREMENT -- THE SECOND WIDGET TO NOT NEED ONE
 * --------------------------------------------------------------------------
 * Unlike TextControl/RadioButton/Slider/ColorControl (all of which hang
 * indefinitely without a live BApplication), a probe confirmed
 * BListView constructs fine with none -- matching BCheckBox's own
 * exception, not the majority pattern. Verified directly (a dedicated
 * probe constructed one, added nothing else, and returned normally),
 * not assumed to carry over from any other widget's own finding.
 *
 * BListItem OWNERSHIP: REAL BeAPI NEVER DELETES ITEMS -- THIS SHIM DOES,
 * SINCE IT'S THE ONLY THING THAT EVER CREATES ONE
 * --------------------------------------------------------------------------
 * A hardware probe with an instrumented BStringItem subclass (destructor
 * printed when it ran) confirmed that real BListView's RemoveItem(int32),
 * MakeEmpty(), AND ~BListView() itself never delete the BListItems they
 * stop referencing -- ownership passes back to the caller on RemoveItem,
 * and MakeEmpty()/~BListView() both leak silently if the caller doesn't
 * track and delete items separately. This is genuine, verified BeAPI
 * behavior, not a shim bug to work around later. Since this shim is the
 * ONLY thing that ever calls `new BStringItem(...)` for a list it
 * created (no function here ever exposes a raw BListItem* handle to the
 * managed side -- see the SCOPE note below), it can safely take on the
 * ownership BeAPI itself declines: hs_list_view_remove_item_at() deletes
 * the item after removing it, hs_list_view_make_empty() deletes every
 * item before clearing the list, and HSListView's own destructor deletes
 * whatever items remain before letting ~BListView() run. Nothing above
 * BStringItem's own text storage is affected by this -- it only closes
 * the real, hardware-confirmed leak.
 *
 * SelectionChanged()/Invoke() -- A DIRECT CALLBACK PAIR, MATCHING
 * TextControl/Slider's TWO-HOOK SHAPE, NOT Button/ColorControl's ONE
 * --------------------------------------------------------------------------
 * Same "no BMessage/BInvoker/target plumbing" scope decision as every
 * other widget in this binding (see hs_button.h's own note) --
 * HSListView overrides both SelectionChanged() and Invoke() to fire
 * direct callbacks instead of posting anywhere; SetSelectionMessage()/
 * SetInvocationMessage() are simply never called, so both stay NULL
 * forever and neither override ever has anything to post regardless.
 * A probe confirmed SelectionChanged() fires correctly on ordinary
 * programmatic Select()/Select(..., extend=true)/DeselectAll() calls,
 * not merely on a real mouse click -- useful both for this shim's own
 * correctness and for ListViewTests.cs, which drives selection
 * programmatically (no synthetic mouse input available over this
 * project's SSH-only hardware harness). Invoke() (real BeAPI: fires on
 * double-click, or Enter/Return while a row has keyboard focus) was not
 * separately probed the same way -- like Button's OnClick, it's the
 * kind of input-driven behavior this binding demonstrates visually
 * (Sample.exe) rather than synthesizes programmatically.
 *
 * REUSING hs_view_get_frame()/move_to()/resize_to()/add_child()/
 * remove_child() -- SEE THE ABI NOTE ABOVE
 * --------------------------------------------------------------------------
 * There is no hs_list_view_get_frame()/move_to()/resize_to() -- see
 * hs_view.h, reused directly against a list view handle exactly like
 * every other HSView-family object (verified safe for THIS multiple-
 * inheritance layout specifically, not merely assumed -- see above).
 *
 * SCOPE
 * --------------------------------------------------------------------------
 * Covered: construction (frame/name/list_view_type/resizing/flags),
 * text-only items (add/insert/remove/read/replace, all backed by a
 * BStringItem this shim owns), CountItems, list_view_type get/set,
 * single- and multi-selection (Select/Deselect/DeselectAll/
 * IsItemSelected/CurrentSelection -- BeAPI's own CurrentSelection(index)
 * already handles both list types, so both are supported for the same
 * cost), and the SelectionChanged()/Invoke() callback pair. NOT covered,
 * a deliberate scope decision: custom BListItem subclasses (anything
 * beyond plain text -- would need a DrawItem() callback into managed
 * code, a meaningfully bigger design than this slice), BOutlineListView
 * (a related but separate class), drag-and-drop reordering
 * (InitiateDrag), SortItems/SwapItems/MoveItem/ReplaceItem/AddList
 * (bulk/reordering operations beyond simple add-at-index), ItemFrame/
 * DoForEach, and the BMessage-based constructor/Archive/Instantiate
 * (out of scope everywhere in this binding). Also out of scope: wrapping
 * in a BScrollView -- a plain BListView works and scrolls its selection
 * into view via ScrollToSelection() internally, it just has no visible
 * scrollbar without one; Sample.exe's demo sizes its frame to fit every
 * row so this doesn't need to be solved to prove the widget works.
 */
#ifndef HS_LIST_VIEW_H
#define HS_LIST_VIEW_H

#include "hs_types.h"

#ifdef __cplusplus
extern "C" {
#endif

typedef void (*hs_list_view_selection_changed_callback)(void* user_data);
typedef void (*hs_list_view_invoked_callback)(void* user_data);
typedef void (*hs_list_view_destroyed_callback)(void* user_data);

/* Create a new HSListView (a BListView subclass) with the given frame,
 * name, list_view_type (the raw enum value: B_SINGLE_SELECTION_LIST=0,
 * B_MULTIPLE_SELECTION_LIST=1 -- checked against the actual installed
 * ListView.h, a plain unvalued C++ enum so these are guaranteed by the
 * language, not merely likely), resizing mode, and flags. Has no parent
 * and is not attached to any window yet -- safe to configure from
 * whatever thread created it, same threading rule as hs_view.h's. Unlike
 * most other widgets in this binding, does NOT need a live BApplication
 * to construct -- see this file's header comment. */
hs_handle hs_list_view_create(hs_rect frame, const char* name,
	uint32_t list_type, uint32_t resizing_mode, uint32_t flags);

/* ONLY safe on a list view that is not currently attached to a parent --
 * same OWNERSHIP rule as hs_view_destroy(). Deletes every item still in
 * the list first (see this file's header comment on BListItem
 * ownership), then fires the destroyed callback, same as any other path
 * to this list view's destruction. */
void hs_list_view_destroy(hs_handle list_view);

void hs_list_view_set_selection_changed_callback(hs_handle list_view,
	hs_list_view_selection_changed_callback callback, void* user_data);
void hs_list_view_set_invoked_callback(hs_handle list_view,
	hs_list_view_invoked_callback callback, void* user_data);
void hs_list_view_set_destroyed_callback(hs_handle list_view,
	hs_list_view_destroyed_callback callback, void* user_data);

/* Appends a new text item (backed by a BStringItem this shim owns -- see
 * the OWNERSHIP note above) to the end of the list. */
void hs_list_view_add_item(hs_handle list_view, const char* text);

/* Inserts a new text item at the given index (0 = the very top; passing
 * CountItems() is equivalent to hs_list_view_add_item()). */
void hs_list_view_add_item_at(hs_handle list_view, const char* text,
	int32_t index);

/* Removes and deletes the item at index (see the OWNERSHIP note above --
 * real BeAPI would merely hand the pointer back and leak it if the
 * caller didn't; this shim never lets a raw BListItem* escape, so it
 * deletes it here instead). Returns false if index is out of range. */
bool hs_list_view_remove_item_at(hs_handle list_view, int32_t index);

/* Removes and deletes every item (see the OWNERSHIP note above -- plain
 * BListView::MakeEmpty() alone would leak every item still present). */
void hs_list_view_make_empty(hs_handle list_view);

int32_t hs_list_view_count_items(hs_handle list_view);

/* Returns a pointer into the item's own internal text storage, same
 * borrowed-pointer situation as hs_control_label() -- copy it into a
 * managed string immediately, do not hold onto it or free it. Returns
 * NULL if index is out of range. */
const char* hs_list_view_item_text(hs_handle list_view, int32_t index);

/* Replaces the text of the item already at index in place (same
 * BStringItem, just a new string) -- does nothing if index is out of
 * range. */
void hs_list_view_set_item_text(hs_handle list_view, int32_t index,
	const char* text);

/* Selects index, extending the current selection instead of replacing it
 * when extend is true (only meaningful for a B_MULTIPLE_SELECTION_LIST --
 * see hs_list_view_set_list_type() below). Fires SelectionChanged() --
 * confirmed on hardware to fire for a programmatic call exactly like a
 * real click, see this file's header comment. */
void hs_list_view_select(hs_handle list_view, int32_t index, bool extend);
void hs_list_view_deselect(hs_handle list_view, int32_t index);
void hs_list_view_deselect_all(hs_handle list_view);

bool hs_list_view_is_item_selected(hs_handle list_view, int32_t index);

/* Returns the selection_index-th selected row's index (0 for the
 * first-selected row, 1 for the second, ...), or -1 once selection_index
 * runs past the number of currently-selected rows -- real BeAPI's own
 * CurrentSelection(int32 index = 0) shape, which already handles both
 * list_view_type values without this shim needing two different
 * functions. */
int32_t hs_list_view_current_selection(hs_handle list_view,
	int32_t selection_index);

void hs_list_view_set_list_type(hs_handle list_view, uint32_t type);
uint32_t hs_list_view_list_type(hs_handle list_view);

#ifdef __cplusplus
}
#endif

#endif /* HS_LIST_VIEW_H */
