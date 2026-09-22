/*
 * hs_control.h -- C shim over the shared BControl-level state
 * (headers/os/interface/Control.h) that every concrete control this
 * binding wraps (HSButton, HSTextControl, ...) has in common.
 *
 * WHY THIS FILE EXISTS NOW, NOT SOONER
 * --------------------------------------
 * hs_button.h originally implemented Label/Value/IsEnabled directly on
 * HSButton, with a note saying to revisit that the day a second concrete
 * control arrived rather than build a shared abstraction ahead of an
 * actual second use case. HSTextControl (see hs_text_control.h) is that
 * second use case, so this file now holds the shared functions instead.
 *
 * WHY A BLIND BControl* CAST IS SAFE HERE
 * ------------------------------------------
 * Every function below casts the opaque handle straight to `BControl*`,
 * regardless of which concrete class (HSButton, HSTextControl, ...) is
 * actually behind it -- safe for the same reason hs_view_add_child()'s
 * own comment (in hs_view.cpp) documents for BView*: every class in
 * every one of this binding's control chains inherits BControl as its
 * own first, non-virtual base (`class BButton : public BControl`,
 * `class BTextControl : public BControl`), which the Itanium C++ ABI
 * this binding builds under places at offset 0. Verified empirically on
 * real Haiku hardware for both chains with small scratch probes (see
 * hs_button.cpp's original probe, from the Button/Control slice, and
 * details.md's Text field section for the HSTextControl one) before
 * either relied on it.
 *
 * WHY THIS DOESN'T ALSO ABSORB Frame/MoveTo/ResizeTo
 * ------------------------------------------------------
 * Those already live in hs_view.h/hs_view.cpp, cast via the even more
 * general `BView*` (BControl's own first base) -- ViewBase.cs already
 * calls hs_view_get_frame()/move_to()/resize_to() directly against any
 * ViewBase-derived handle, control or plain view alike. Nothing
 * BControl-specific needed here for geometry.
 */
#ifndef HS_CONTROL_H
#define HS_CONTROL_H

#include "hs_types.h"

#ifdef __cplusplus
extern "C" {
#endif

void hs_control_set_label(hs_handle control, const char* label);

/* Returns a pointer into BControl's own internal storage, same borrowed-
 * pointer situation as hs_window_title() -- copy it into a managed
 * string immediately, do not hold onto it or free it. */
const char* hs_control_label(hs_handle control);

void hs_control_set_value(hs_handle control, int32_t value);
int32_t hs_control_value(hs_handle control);

void hs_control_set_enabled(hs_handle control, bool enabled);
bool hs_control_is_enabled(hs_handle control);

#ifdef __cplusplus
}
#endif

#endif /* HS_CONTROL_H */
