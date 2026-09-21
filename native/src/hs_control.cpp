/*
 * hs_control.cpp -- implementation of the shared BControl-level state
 * shim. See hs_control.h for the design rationale before changing
 * anything here.
 */
#include "hs_control.h"

#include <Control.h>


void hs_control_set_label(hs_handle control, const char* label)
{
	static_cast<BControl*>(control)->SetLabel(label);
}


const char* hs_control_label(hs_handle control)
{
	return static_cast<BControl*>(control)->Label();
}


void hs_control_set_value(hs_handle control, int32_t value)
{
	static_cast<BControl*>(control)->SetValue(value);
}


int32_t hs_control_value(hs_handle control)
{
	return static_cast<BControl*>(control)->Value();
}


void hs_control_set_enabled(hs_handle control, bool enabled)
{
	static_cast<BControl*>(control)->SetEnabled(enabled);
}


bool hs_control_is_enabled(hs_handle control)
{
	return static_cast<BControl*>(control)->IsEnabled();
}
