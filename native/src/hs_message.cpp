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
#include <Point.h>
#include <Rect.h>


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


hs_status hs_message_add_int8(hs_handle message, const char* name, int8_t value)
{
	return static_cast<BMessage*>(message)->AddInt8(name, value);
}


hs_status hs_message_find_int8(hs_handle message, const char* name, int8_t* out_value)
{
	return static_cast<BMessage*>(message)->FindInt8(name, out_value);
}


hs_status hs_message_add_int16(hs_handle message, const char* name, int16_t value)
{
	return static_cast<BMessage*>(message)->AddInt16(name, value);
}


hs_status hs_message_find_int16(hs_handle message, const char* name, int16_t* out_value)
{
	return static_cast<BMessage*>(message)->FindInt16(name, out_value);
}


hs_status hs_message_add_int64(hs_handle message, const char* name, int64_t value)
{
	return static_cast<BMessage*>(message)->AddInt64(name, value);
}


hs_status hs_message_find_int64(hs_handle message, const char* name, int64_t* out_value)
{
	return static_cast<BMessage*>(message)->FindInt64(name, out_value);
}


hs_status hs_message_add_float(hs_handle message, const char* name, float value)
{
	return static_cast<BMessage*>(message)->AddFloat(name, value);
}


hs_status hs_message_find_float(hs_handle message, const char* name, float* out_value)
{
	return static_cast<BMessage*>(message)->FindFloat(name, out_value);
}


hs_status hs_message_add_double(hs_handle message, const char* name, double value)
{
	return static_cast<BMessage*>(message)->AddDouble(name, value);
}


hs_status hs_message_find_double(hs_handle message, const char* name, double* out_value)
{
	return static_cast<BMessage*>(message)->FindDouble(name, out_value);
}


hs_status hs_message_add_point(hs_handle message, const char* name, hs_point point)
{
	return static_cast<BMessage*>(message)->AddPoint(name, BPoint(point.x, point.y));
}


hs_status hs_message_find_point(hs_handle message, const char* name, hs_point* out_point)
{
	BPoint point;
	hs_status status = static_cast<BMessage*>(message)->FindPoint(name, &point);
	if (status == HS_OK) {
		out_point->x = point.x;
		out_point->y = point.y;
	}
	return status;
}


hs_status hs_message_add_rect(hs_handle message, const char* name, hs_rect rect)
{
	return static_cast<BMessage*>(message)->AddRect(name,
		BRect(rect.left, rect.top, rect.right, rect.bottom));
}


hs_status hs_message_find_rect(hs_handle message, const char* name, hs_rect* out_rect)
{
	BRect rect;
	hs_status status = static_cast<BMessage*>(message)->FindRect(name, &rect);
	if (status == HS_OK) {
		out_rect->left = rect.left;
		out_rect->top = rect.top;
		out_rect->right = rect.right;
		out_rect->bottom = rect.bottom;
	}
	return status;
}


hs_status hs_message_add_pointer(hs_handle message, const char* name, void* value)
{
	return static_cast<BMessage*>(message)->AddPointer(name, value);
}


hs_status hs_message_find_pointer(hs_handle message, const char* name, void** out_value)
{
	return static_cast<BMessage*>(message)->FindPointer(name, out_value);
}


hs_status hs_message_add_string(hs_handle message, const char* name, const char* value)
{
	return static_cast<BMessage*>(message)->AddString(name, value);
}


hs_status hs_message_find_string(hs_handle message, const char* name, const char** out_value)
{
	return static_cast<BMessage*>(message)->FindString(name, out_value);
}
