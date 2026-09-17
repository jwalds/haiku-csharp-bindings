/*
 * hs_message.cpp -- implementation of the BMessage C shim.
 *
 * Every function here follows the same shape: cast the opaque hs_handle
 * back to the real BMessage*, call the real method, translate the result to
 * a C-ABI-safe type. Nothing here should ever let a C++ exception escape
 * across the extern "C" boundary -- BMessage's Add.../Find... family is
 * documented as returning status_t rather than throwing, so in practice
 * there is nothing to catch for this particular slice, but this remains a
 * hard rule for every future addition to this shim: P/Invoke has no concept
 * of a C++ exception, and one escaping into the runtime's calling
 * convention is a hard crash, not a catchable managed exception.
 */
#include "hs_message.h"

#include <Message.h>


hs_handle hs_message_create(uint32_t what)
{
	return static_cast<hs_handle>(new BMessage(what));
}


void hs_message_destroy(hs_handle message)
{
	delete static_cast<BMessage*>(message);
}


uint32_t hs_message_what(hs_handle message)
{
	return static_cast<BMessage*>(message)->what;
}


void hs_message_set_what(hs_handle message, uint32_t what)
{
	static_cast<BMessage*>(message)->what = what;
}


hs_status hs_message_add_int32(hs_handle message, const char* name, int32_t value)
{
	return static_cast<BMessage*>(message)->AddInt32(name, value);
}


hs_status hs_message_find_int32(hs_handle message, const char* name, int32_t* out_value)
{
	return static_cast<BMessage*>(message)->FindInt32(name, out_value);
}


hs_status hs_message_add_bool(hs_handle message, const char* name, bool value)
{
	return static_cast<BMessage*>(message)->AddBool(name, value);
}


hs_status hs_message_find_bool(hs_handle message, const char* name, bool* out_value)
{
	return static_cast<BMessage*>(message)->FindBool(name, out_value);
}


hs_status hs_message_add_string(hs_handle message, const char* name, const char* value)
{
	return static_cast<BMessage*>(message)->AddString(name, value);
}


hs_status hs_message_find_string(hs_handle message, const char* name, const char** out_value)
{
	return static_cast<BMessage*>(message)->FindString(name, out_value);
}
