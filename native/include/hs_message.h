/*
 * hs_message.h -- C shim over BMessage (headers/os/app/Message.h).
 *
 * BMessage is Haiku's universal typed-data / IPC container: every event that
 * flows through a BLooper (including every user-defined message you post)
 * is a BMessage. Real BMessage has a large overloaded Add.../Find... family
 * (AddInt8/16/32/64, AddFloat, AddDouble, AddPoint, AddRect, AddString,
 * AddBool, AddPointer, AddFlat<T>, ...). This first slice covers only
 * int32/string/bool, which is enough to prove the whole marshaling story
 * end to end; more Add.../Find... pairs are a mechanical, low-risk addition once
 * this shape is proven out -- see the "Adding more BMessage fields" note in
 * the top-level README before adding one.
 *
 * Ownership: a message you create with hs_message_create() is yours -- call
 * hs_message_destroy() on it when done (or hand it to
 * hs_application_post_message(), which takes ownership the same way
 * BLooper::PostMessage(BMessage*) does not: Haiku's PostMessage() COPIES the
 * message you pass it, so you still own and must destroy your original
 * afterward). The message handle your MessageReceived callback receives is
 * different: that BMessage is owned by the BLooper's message queue and will
 * be deleted by Haiku itself right after your callback returns. Do NOT call
 * hs_message_destroy() on a message you received via a callback -- see
 * hs_application.h's callback-ownership note for the full rule.
 */
#ifndef HS_MESSAGE_H
#define HS_MESSAGE_H

#include "hs_types.h"

#ifdef __cplusplus
extern "C" {
#endif

/* Create a new, owned BMessage with the given "what" code (BMessage's own
 * public uint32 `what` field -- Haiku convention is to make this a 4-char
 * constant like 'quit' packed into a uint32, but any value works). */
hs_handle hs_message_create(uint32_t what);

/* Destroy a message YOU own (see the ownership note above). Safe to call
 * with NULL (no-op). */
void hs_message_destroy(hs_handle message);

uint32_t hs_message_what(hs_handle message);
void hs_message_set_what(hs_handle message, uint32_t what);

hs_status hs_message_add_int32(hs_handle message, const char* name, int32_t value);
hs_status hs_message_find_int32(hs_handle message, const char* name, int32_t* out_value);

hs_status hs_message_add_bool(hs_handle message, const char* name, bool value);
hs_status hs_message_find_bool(hs_handle message, const char* name, bool* out_value);

hs_status hs_message_add_int8(hs_handle message, const char* name, int8_t value);
hs_status hs_message_find_int8(hs_handle message, const char* name, int8_t* out_value);

hs_status hs_message_add_int16(hs_handle message, const char* name, int16_t value);
hs_status hs_message_find_int16(hs_handle message, const char* name, int16_t* out_value);

hs_status hs_message_add_int64(hs_handle message, const char* name, int64_t value);
hs_status hs_message_find_int64(hs_handle message, const char* name, int64_t* out_value);

hs_status hs_message_add_float(hs_handle message, const char* name, float value);
hs_status hs_message_find_float(hs_handle message, const char* name, float* out_value);

hs_status hs_message_add_double(hs_handle message, const char* name, double value);
hs_status hs_message_find_double(hs_handle message, const char* name, double* out_value);

/* hs_point/hs_rect (see hs_types.h) are passed BY VALUE -- on the native
 * side this is a plain field-by-field copy into/out of a real BPoint or
 * BRect, not a reinterpret_cast, even though the layouts happen to match
 * exactly; see hs_message.cpp. */
hs_status hs_message_add_point(hs_handle message, const char* name, hs_point point);
hs_status hs_message_find_point(hs_handle message, const char* name, hs_point* out_point);

hs_status hs_message_add_rect(hs_handle message, const char* name, hs_rect rect);
hs_status hs_message_find_rect(hs_handle message, const char* name, hs_rect* out_rect);

/* AddPointer/FindPointer store an opaque address verbatim -- BMessage never
 * dereferences it or takes ownership of whatever it points to. Useful for
 * passing another hs_handle (or any address meaningful only within your
 * own process) through a message; not a way to move managed data, since
 * the GC can relocate managed memory out from under a raw address like
 * this. */
hs_status hs_message_add_pointer(hs_handle message, const char* name, void* value);
hs_status hs_message_find_pointer(hs_handle message, const char* name, void** out_value);

/* AddString copies the string into the message (BMessage's own behavior),
 * so `value` need not outlive this call. FindString hands back a pointer
 * INTO the message's own internal storage -- valid only until the message
 * is next mutated or destroyed, which is why the managed wrapper copies it
 * into a System.String immediately rather than holding onto the pointer. */
hs_status hs_message_add_string(hs_handle message, const char* name, const char* value);
hs_status hs_message_find_string(hs_handle message, const char* name, const char** out_value);

/* -- Unsigned integer scalars (same semantics as their signed Int
 * counterparts above). -- */
hs_status hs_message_add_uint8(hs_handle message, const char* name, uint8_t value);
hs_status hs_message_find_uint8(hs_handle message, const char* name, uint8_t* out_value);

hs_status hs_message_add_uint16(hs_handle message, const char* name, uint16_t value);
hs_status hs_message_find_uint16(hs_handle message, const char* name, uint16_t* out_value);

hs_status hs_message_add_uint32(hs_handle message, const char* name, uint32_t value);
hs_status hs_message_find_uint32(hs_handle message, const char* name, uint32_t* out_value);

hs_status hs_message_add_uint64(hs_handle message, const char* name, uint64_t value);
hs_status hs_message_find_uint64(hs_handle message, const char* name, uint64_t* out_value);

/* hs_size/hs_rgb_color/hs_alignment (see hs_types.h) are passed BY VALUE,
 * copied field-by-field on the native side exactly like hs_point/hs_rect
 * above -- see hs_message.cpp. */
hs_status hs_message_add_size(hs_handle message, const char* name, hs_size size);
hs_status hs_message_find_size(hs_handle message, const char* name, hs_size* out_size);

hs_status hs_message_add_color(hs_handle message, const char* name, hs_rgb_color color);
hs_status hs_message_find_color(hs_handle message, const char* name, hs_rgb_color* out_color);

hs_status hs_message_add_alignment(hs_handle message, const char* name, hs_alignment value);
hs_status hs_message_find_alignment(hs_handle message, const char* name, hs_alignment* out_alignment);

/* Nested messages: `value` / the message `out_message` points at are
 * themselves hs_handles created with hs_message_create(), exactly like any
 * other message this shim hands you. AddMessage COPIES `value` into
 * `message` (BMessage's own behavior) -- you still own and must destroy
 * `value` afterward. FindMessage fills `out_message`, which YOU must have
 * already created (and still own) before calling this; unlike FindString's
 * borrowed-pointer pattern, this does not hand back a pointer into
 * `message`'s own storage, so there is no lifetime hazard here beyond the
 * ordinary rule that you own what you created. */
hs_status hs_message_add_message(hs_handle message, const char* name, hs_handle value);
hs_status hs_message_find_message(hs_handle message, const char* name, hs_handle out_message);

/* Generic escape hatch for any type_code not covered by a named Add/Find
 * pair above -- mirrors BMessage's own AddData/FindData. `type` is any
 * Haiku type_code (see TypeConstants.h; B_RAW_TYPE if you don't care).
 * AddData copies `data` into the message, so it need not outlive the
 * call. FindData hands back a pointer INTO the message's own storage,
 * exactly like FindString -- copy it out immediately (see
 * Message.FindData's managed-side handling). */
hs_status hs_message_add_data(hs_handle message, const char* name, uint32_t type,
	const void* data, int32_t num_bytes);
hs_status hs_message_find_data(hs_handle message, const char* name, uint32_t type,
	const void** out_data, int32_t* out_num_bytes);

/* -- Has* (does a field with this name exist as this type? index 0 only --
 * this binding doesn't yet support multiple values under one name). -- */
bool hs_message_has_int8(hs_handle message, const char* name);
bool hs_message_has_int16(hs_handle message, const char* name);
bool hs_message_has_int32(hs_handle message, const char* name);
bool hs_message_has_int64(hs_handle message, const char* name);
bool hs_message_has_uint8(hs_handle message, const char* name);
bool hs_message_has_uint16(hs_handle message, const char* name);
bool hs_message_has_uint32(hs_handle message, const char* name);
bool hs_message_has_uint64(hs_handle message, const char* name);
bool hs_message_has_bool(hs_handle message, const char* name);
bool hs_message_has_float(hs_handle message, const char* name);
bool hs_message_has_double(hs_handle message, const char* name);
bool hs_message_has_string(hs_handle message, const char* name);
bool hs_message_has_point(hs_handle message, const char* name);
bool hs_message_has_rect(hs_handle message, const char* name);
bool hs_message_has_size(hs_handle message, const char* name);
bool hs_message_has_color(hs_handle message, const char* name);
bool hs_message_has_alignment(hs_handle message, const char* name);
bool hs_message_has_pointer(hs_handle message, const char* name);
bool hs_message_has_message(hs_handle message, const char* name);
bool hs_message_has_data(hs_handle message, const char* name, uint32_t type);

/* -- Whole-message and whole-field operations. -- */
hs_status hs_message_remove_name(hs_handle message, const char* name);
hs_status hs_message_remove_data(hs_handle message, const char* name, int32_t index);
hs_status hs_message_make_empty(hs_handle message);
bool hs_message_is_empty(hs_handle message);
int32_t hs_message_count_names(hs_handle message, uint32_t type);
hs_status hs_message_rename(hs_handle message, const char* old_name, const char* new_name);
/* Copies every field from `source` into `message` (BMessage::Append) --
 * does not take ownership of or modify `source`. */
hs_status hs_message_append(hs_handle message, hs_handle source);

/* -- Replace* (same semantics as the matching Add* above, but the name
 * must already exist with the same type -- see BMessage::Replace*). -- */
hs_status hs_message_replace_int8(hs_handle message, const char* name, int8_t value);
hs_status hs_message_replace_int16(hs_handle message, const char* name, int16_t value);
hs_status hs_message_replace_int32(hs_handle message, const char* name, int32_t value);
hs_status hs_message_replace_int64(hs_handle message, const char* name, int64_t value);
hs_status hs_message_replace_uint8(hs_handle message, const char* name, uint8_t value);
hs_status hs_message_replace_uint16(hs_handle message, const char* name, uint16_t value);
hs_status hs_message_replace_uint32(hs_handle message, const char* name, uint32_t value);
hs_status hs_message_replace_uint64(hs_handle message, const char* name, uint64_t value);
hs_status hs_message_replace_bool(hs_handle message, const char* name, bool value);
hs_status hs_message_replace_float(hs_handle message, const char* name, float value);
hs_status hs_message_replace_double(hs_handle message, const char* name, double value);
hs_status hs_message_replace_string(hs_handle message, const char* name, const char* value);
hs_status hs_message_replace_point(hs_handle message, const char* name, hs_point point);
hs_status hs_message_replace_rect(hs_handle message, const char* name, hs_rect rect);
hs_status hs_message_replace_size(hs_handle message, const char* name, hs_size size);
hs_status hs_message_replace_color(hs_handle message, const char* name, hs_rgb_color color);
hs_status hs_message_replace_alignment(hs_handle message, const char* name, hs_alignment value);
hs_status hs_message_replace_pointer(hs_handle message, const char* name, void* value);
hs_status hs_message_replace_message(hs_handle message, const char* name, hs_handle value);
hs_status hs_message_replace_data(hs_handle message, const char* name, uint32_t type,
	const void* data, int32_t num_bytes);

#ifdef __cplusplus
}
#endif

#endif /* HS_MESSAGE_H */
