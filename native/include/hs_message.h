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

/* AddString copies the string into the message (BMessage's own behavior),
 * so `value` need not outlive this call. FindString hands back a pointer
 * INTO the message's own internal storage -- valid only until the message
 * is next mutated or destroyed, which is why the managed wrapper copies it
 * into a System.String immediately rather than holding onto the pointer. */
hs_status hs_message_add_string(hs_handle message, const char* name, const char* value);
hs_status hs_message_find_string(hs_handle message, const char* name, const char** out_value);

#ifdef __cplusplus
}
#endif

#endif /* HS_MESSAGE_H */
