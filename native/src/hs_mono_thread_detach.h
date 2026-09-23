/*
 * hs_mono_thread_detach.h -- pairs with hs_mono_thread_attach.h. Registers
 * a real pthread TLS destructor so mono_thread_detach_if_exiting() runs at
 * each thread's genuine OS-level exit, confirmed via a standalone probe
 * (probe_tls_destructor.cpp, not committed) to actually fire for a
 * BLooper-spawned thread. See KNOWN_ISSUES.md #3/#5 for the full history.
 */
#ifndef HS_MONO_THREAD_DETACH_H
#define HS_MONO_THREAD_DETACH_H

#include <pthread.h>

#include <mono/metadata/threads.h>

namespace hs_internal {

namespace detail {

inline pthread_key_t& MonoDetachKeyStorage()
{
	static pthread_key_t key;
	return key;
}

inline void MonoDetachDestructor(void* /* value */)
{
	mono_thread_detach_if_exiting();
}

inline void CreateMonoDetachKey()
{
	pthread_key_create(&MonoDetachKeyStorage(), MonoDetachDestructor);
}

} // namespace detail

inline void MarkThreadForMonoDetachOnExit()
{
	static pthread_once_t once = PTHREAD_ONCE_INIT;
	pthread_once(&once, detail::CreateMonoDetachKey);
	pthread_setspecific(detail::MonoDetachKeyStorage(), reinterpret_cast<void*>(1));
}

} // namespace hs_internal

#endif /* HS_MONO_THREAD_DETACH_H */
