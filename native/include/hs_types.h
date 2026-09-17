/*
 * hs_types.h -- common C-ABI types shared by every hs_*.h shim header.
 *
 * Every native BeAPI object (BApplication, BMessage, ...) crosses the P/Invoke
 * boundary as an opaque `hs_handle` (just a `void*`). C# never sees the real
 * C++ class layout -- it only ever holds this handle and passes it back into
 * the shim functions that know what to do with it. This is the standard
 * "opaque handle" pattern used by every C shim over a C++ library, and it's
 * mandatory here: P/Invoke can only call C-linkage functions with a
 * C-compatible ABI, and BeAPI (libbe) exposes nothing but C++ classes with
 * virtual methods -- there is no shortcut around writing this shim.
 */
#ifndef HS_TYPES_H
#define HS_TYPES_H

#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Opaque handle to any native BeAPI object wrapped by this shim (a
 * BApplication*, BMessage*, etc., cast to void*). Which concrete type a
 * given handle points to is determined entirely by which hs_<kit>_* family
 * of functions you got it from and are passing it to -- the shim does no
 * runtime type-checking, exactly like an opaque FILE* or HANDLE. */
typedef void* hs_handle;

/* Mirrors Haiku's status_t: 0 (B_OK) means success, negative values are
 * error codes. We deliberately don't redeclare Haiku's actual B_* error
 * constants here -- callers that need to distinguish specific errors should
 * treat this as "zero is success, nonzero is not" for now. */
typedef int32_t hs_status;
#define HS_OK 0

/* Mirrors Haiku's thread_id (a small positive integer identifying a kernel
 * thread), as returned by BLooper::Run(). */
typedef int32_t hs_thread_id;

#ifdef __cplusplus
}
#endif

#endif /* HS_TYPES_H */
