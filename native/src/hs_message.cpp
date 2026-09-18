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
#include <Size.h>
#include <GraphicsDefs.h>
#include <Alignment.h>


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
hs_status hs_message_add_uint8(hs_handle message, const char* name, uint8_t value)
{
	return static_cast<BMessage*>(message)->AddUInt8(name, value);
}


hs_status hs_message_find_uint8(hs_handle message, const char* name, uint8_t* out_value)
{
	return static_cast<BMessage*>(message)->FindUInt8(name, out_value);
}


hs_status hs_message_add_uint16(hs_handle message, const char* name, uint16_t value)
{
	return static_cast<BMessage*>(message)->AddUInt16(name, value);
}


hs_status hs_message_find_uint16(hs_handle message, const char* name, uint16_t* out_value)
{
	return static_cast<BMessage*>(message)->FindUInt16(name, out_value);
}


hs_status hs_message_add_uint32(hs_handle message, const char* name, uint32_t value)
{
	return static_cast<BMessage*>(message)->AddUInt32(name, value);
}


hs_status hs_message_find_uint32(hs_handle message, const char* name, uint32_t* out_value)
{
	return static_cast<BMessage*>(message)->FindUInt32(name, out_value);
}


hs_status hs_message_add_uint64(hs_handle message, const char* name, uint64_t value)
{
	return static_cast<BMessage*>(message)->AddUInt64(name, value);
}


hs_status hs_message_find_uint64(hs_handle message, const char* name, uint64_t* out_value)
{
	return static_cast<BMessage*>(message)->FindUInt64(name, out_value);
}


hs_status hs_message_add_size(hs_handle message, const char* name, hs_size size)
{
	return static_cast<BMessage*>(message)->AddSize(name, BSize(size.width, size.height));
}


hs_status hs_message_find_size(hs_handle message, const char* name, hs_size* out_size)
{
	BSize size;
	hs_status status = static_cast<BMessage*>(message)->FindSize(name, &size);
	if (status == HS_OK) {
		out_size->width = size.width;
		out_size->height = size.height;
	}
	return status;
}


hs_status hs_message_add_color(hs_handle message, const char* name, hs_rgb_color color)
{
	rgb_color nativeColor;
	nativeColor.red = color.red;
	nativeColor.green = color.green;
	nativeColor.blue = color.blue;
	nativeColor.alpha = color.alpha;
	return static_cast<BMessage*>(message)->AddColor(name, nativeColor);
}


hs_status hs_message_find_color(hs_handle message, const char* name, hs_rgb_color* out_color)
{
	rgb_color color;
	hs_status status = static_cast<BMessage*>(message)->FindColor(name, &color);
	if (status == HS_OK) {
		out_color->red = color.red;
		out_color->green = color.green;
		out_color->blue = color.blue;
		out_color->alpha = color.alpha;
	}
	return status;
}


/*
 * BAlignment's two fields are themselves enums (`alignment` and
 * `vertical_alignment`, from InterfaceDefs.h) -- note the local variables
 * below are deliberately never named `alignment`, since that identifier is
 * Haiku's own global enum type name and shadowing it here would be an easy
 * way to confuse a future edit to this function.
 */
hs_status hs_message_add_alignment(hs_handle message, const char* name, hs_alignment value)
{
	BAlignment nativeAlignment(static_cast<::alignment>(value.horizontal),
		static_cast<vertical_alignment>(value.vertical));
	return static_cast<BMessage*>(message)->AddAlignment(name, nativeAlignment);
}


hs_status hs_message_find_alignment(hs_handle message, const char* name, hs_alignment* out_alignment)
{
	BAlignment nativeAlignment;
	hs_status status = static_cast<BMessage*>(message)->FindAlignment(name, &nativeAlignment);
	if (status == HS_OK) {
		out_alignment->horizontal = nativeAlignment.horizontal;
		out_alignment->vertical = nativeAlignment.vertical;
	}
	return status;
}


hs_status hs_message_add_message(hs_handle message, const char* name, hs_handle value)
{
	return static_cast<BMessage*>(message)->AddMessage(name, static_cast<BMessage*>(value));
}


hs_status hs_message_find_message(hs_handle message, const char* name, hs_handle out_message)
{
	return static_cast<BMessage*>(message)->FindMessage(name, static_cast<BMessage*>(out_message));
}


hs_status hs_message_add_data(hs_handle message, const char* name, uint32_t type,
	const void* data, int32_t num_bytes)
{
	return static_cast<BMessage*>(message)->AddData(name, type, data, num_bytes);
}


hs_status hs_message_find_data(hs_handle message, const char* name, uint32_t type,
	const void** out_data, int32_t* out_num_bytes)
{
	ssize_t numBytes = 0;
	hs_status status = static_cast<BMessage*>(message)->FindData(name, type, out_data, &numBytes);
	if (status == HS_OK)
		*out_num_bytes = static_cast<int32_t>(numBytes);
	return status;
}


bool hs_message_has_int8(hs_handle message, const char* name)
{
	return static_cast<BMessage*>(message)->HasInt8(name);
}


bool hs_message_has_int16(hs_handle message, const char* name)
{
	return static_cast<BMessage*>(message)->HasInt16(name);
}


bool hs_message_has_int32(hs_handle message, const char* name)
{
	return static_cast<BMessage*>(message)->HasInt32(name);
}


bool hs_message_has_int64(hs_handle message, const char* name)
{
	return static_cast<BMessage*>(message)->HasInt64(name);
}


bool hs_message_has_uint8(hs_handle message, const char* name)
{
	return static_cast<BMessage*>(message)->HasUInt8(name);
}


bool hs_message_has_uint16(hs_handle message, const char* name)
{
	return static_cast<BMessage*>(message)->HasUInt16(name);
}


bool hs_message_has_uint32(hs_handle message, const char* name)
{
	return static_cast<BMessage*>(message)->HasUInt32(name);
}


bool hs_message_has_uint64(hs_handle message, const char* name)
{
	return static_cast<BMessage*>(message)->HasUInt64(name);
}


bool hs_message_has_bool(hs_handle message, const char* name)
{
	return static_cast<BMessage*>(message)->HasBool(name);
}


bool hs_message_has_float(hs_handle message, const char* name)
{
	return static_cast<BMessage*>(message)->HasFloat(name);
}


bool hs_message_has_double(hs_handle message, const char* name)
{
	return static_cast<BMessage*>(message)->HasDouble(name);
}


bool hs_message_has_string(hs_handle message, const char* name)
{
	return static_cast<BMessage*>(message)->HasString(name);
}


bool hs_message_has_point(hs_handle message, const char* name)
{
	return static_cast<BMessage*>(message)->HasPoint(name);
}


bool hs_message_has_rect(hs_handle message, const char* name)
{
	return static_cast<BMessage*>(message)->HasRect(name);
}


bool hs_message_has_size(hs_handle message, const char* name)
{
	return static_cast<BMessage*>(message)->HasSize(name);
}


bool hs_message_has_color(hs_handle message, const char* name)
{
	return static_cast<BMessage*>(message)->HasColor(name);
}


bool hs_message_has_alignment(hs_handle message, const char* name)
{
	return static_cast<BMessage*>(message)->HasAlignment(name);
}


bool hs_message_has_pointer(hs_handle message, const char* name)
{
	return static_cast<BMessage*>(message)->HasPointer(name);
}


bool hs_message_has_message(hs_handle message, const char* name)
{
	return static_cast<BMessage*>(message)->HasMessage(name);
}


bool hs_message_has_data(hs_handle message, const char* name, uint32_t type)
{
	return static_cast<BMessage*>(message)->HasData(name, type);
}


hs_status hs_message_remove_name(hs_handle message, const char* name)
{
	return static_cast<BMessage*>(message)->RemoveName(name);
}


hs_status hs_message_remove_data(hs_handle message, const char* name, int32_t index)
{
	return static_cast<BMessage*>(message)->RemoveData(name, index);
}


hs_status hs_message_make_empty(hs_handle message)
{
	return static_cast<BMessage*>(message)->MakeEmpty();
}


bool hs_message_is_empty(hs_handle message)
{
	return static_cast<BMessage*>(message)->IsEmpty();
}


int32_t hs_message_count_names(hs_handle message, uint32_t type)
{
	return static_cast<BMessage*>(message)->CountNames(type);
}


hs_status hs_message_rename(hs_handle message, const char* old_name, const char* new_name)
{
	return static_cast<BMessage*>(message)->Rename(old_name, new_name);
}


hs_status hs_message_append(hs_handle message, hs_handle source)
{
	return static_cast<BMessage*>(message)->Append(*static_cast<BMessage*>(source));
}


hs_status hs_message_replace_int8(hs_handle message, const char* name, int8_t value)
{
	return static_cast<BMessage*>(message)->ReplaceInt8(name, value);
}


hs_status hs_message_replace_int16(hs_handle message, const char* name, int16_t value)
{
	return static_cast<BMessage*>(message)->ReplaceInt16(name, value);
}


hs_status hs_message_replace_int32(hs_handle message, const char* name, int32_t value)
{
	return static_cast<BMessage*>(message)->ReplaceInt32(name, value);
}


hs_status hs_message_replace_int64(hs_handle message, const char* name, int64_t value)
{
	return static_cast<BMessage*>(message)->ReplaceInt64(name, value);
}


hs_status hs_message_replace_uint8(hs_handle message, const char* name, uint8_t value)
{
	return static_cast<BMessage*>(message)->ReplaceUInt8(name, value);
}


hs_status hs_message_replace_uint16(hs_handle message, const char* name, uint16_t value)
{
	return static_cast<BMessage*>(message)->ReplaceUInt16(name, value);
}


hs_status hs_message_replace_uint32(hs_handle message, const char* name, uint32_t value)
{
	return static_cast<BMessage*>(message)->ReplaceUInt32(name, value);
}


hs_status hs_message_replace_uint64(hs_handle message, const char* name, uint64_t value)
{
	return static_cast<BMessage*>(message)->ReplaceUInt64(name, value);
}


hs_status hs_message_replace_bool(hs_handle message, const char* name, bool value)
{
	return static_cast<BMessage*>(message)->ReplaceBool(name, value);
}


hs_status hs_message_replace_float(hs_handle message, const char* name, float value)
{
	return static_cast<BMessage*>(message)->ReplaceFloat(name, value);
}


hs_status hs_message_replace_double(hs_handle message, const char* name, double value)
{
	return static_cast<BMessage*>(message)->ReplaceDouble(name, value);
}


hs_status hs_message_replace_string(hs_handle message, const char* name, const char* value)
{
	return static_cast<BMessage*>(message)->ReplaceString(name, value);
}


hs_status hs_message_replace_point(hs_handle message, const char* name, hs_point point)
{
	return static_cast<BMessage*>(message)->ReplacePoint(name, BPoint(point.x, point.y));
}


hs_status hs_message_replace_rect(hs_handle message, const char* name, hs_rect rect)
{
	return static_cast<BMessage*>(message)->ReplaceRect(name,
		BRect(rect.left, rect.top, rect.right, rect.bottom));
}


hs_status hs_message_replace_size(hs_handle message, const char* name, hs_size size)
{
	return static_cast<BMessage*>(message)->ReplaceSize(name, BSize(size.width, size.height));
}


hs_status hs_message_replace_color(hs_handle message, const char* name, hs_rgb_color color)
{
	rgb_color nativeColor;
	nativeColor.red = color.red;
	nativeColor.green = color.green;
	nativeColor.blue = color.blue;
	nativeColor.alpha = color.alpha;
	return static_cast<BMessage*>(message)->ReplaceColor(name, nativeColor);
}


hs_status hs_message_replace_alignment(hs_handle message, const char* name, hs_alignment value)
{
	BAlignment nativeAlignment(static_cast<::alignment>(value.horizontal),
		static_cast<vertical_alignment>(value.vertical));
	return static_cast<BMessage*>(message)->ReplaceAlignment(name, nativeAlignment);
}


hs_status hs_message_replace_pointer(hs_handle message, const char* name, void* value)
{
	return static_cast<BMessage*>(message)->ReplacePointer(name, value);
}


hs_status hs_message_replace_message(hs_handle message, const char* name, hs_handle value)
{
	return static_cast<BMessage*>(message)->ReplaceMessage(name, static_cast<BMessage*>(value));
}


hs_status hs_message_replace_data(hs_handle message, const char* name, uint32_t type,
	const void* data, int32_t num_bytes)
{
	return static_cast<BMessage*>(message)->ReplaceData(name, type, data, num_bytes);
}
