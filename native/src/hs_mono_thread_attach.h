/*
 * hs_mono_thread_attach.h -- internal helper shared by hs_window.cpp and
 * hs_application.cpp. NOT part of this binding's public C shim API.
 *
 * ROOT CAUSE (confirmed with gdb, real debug symbols -- this Haiku mono
 * port is NOT stripped, contrary to what KNOWN_ISSUES.md #3 originally
 * assumed): calling mono_thread_current() on a Haiku-spawned thread (a
 * BWindow's or BApplication's own message-loop thread) that Mono only
 * ever attached IMPLICITLY -- as a side effect of the JIT trampoline
 * invoking a delegate, the first time this shim calls into managed code
 * on that thread -- crashes with a real SIGSEGV. The actual fault, from a
 * live gdb backtrace:
 *
 *   mono_thread_current() [threads.c:2184]
 *   -> get_current_thread_ptr_for_domain(domain=0x0, ...) [threads.c:632]
 *   -> mono_class_vtable_checked(domain=0x0, klass=..., ...) [object.c:1943]
 *   -> crashes dereferencing a field at domain+0x7c -- domain is NULL.
 *
 * i.e. mono_domain_get() returns NULL on this thread. The IMPLICIT attach
 * path apparently gets the thread far enough to run JIT'd code, but never
 * sets this thread's current-MonoDomain TLS the way an EXPLICIT
 * mono_thread_attach(domain) call does. Every earlier fix attempt in
 * KNOWN_ISSUES.md #3 assumed the problem was WHEN to call
 * mono_thread_current()/mono_thread_detach() on an already-implicitly-
 * attached thread; none questioned whether that implicit attach was ever
 * complete in the first place.
 *
 * THE FIX: attach each such thread EXPLICITLY, with a real MonoDomain*,
 * before its first managed call -- i.e. before this shim's own callback
 * trampolines (MessageReceived, QuitRequested, ReadyToRun) invoke any
 * delegate. mono_thread_attach() on an already-attached thread is cheap
 * and safe (returns the existing MonoThread*), so calling this
 * unconditionally at the top of every such trampoline, every time, is
 * fine -- EnsureThreadAttached() still only does real work once per
 * thread (a thread-local flag), but even without that guard a redundant
 * mono_thread_attach() call would not be harmful.
 *
 * WHAT THIS FIX DOES NOT COVER -- calling a REAL mono_thread_detach() is
 * NOT safe, even with the domain fix above in place, and hs_window.cpp/
 * hs_application.cpp deliberately never call it: two independent,
 * 100%-reproducible failure modes were found on real hardware (both
 * gdb-confirmed) when this was tried as a way to silence KNOWN_ISSUES.md
 * #3's benign "Failed aborting id" warning. On a window whose thread ever
 * ran BView::Draw() (real app_server IPC), mono_thread_detach() hits a
 * fatal g_error: "Cannot transition thread ... from STATE_BLOCKING with
 * DO_BLOCKING" -- Draw()'s own IPC leaves the thread in Mono's
 * cooperative-GC blocking state, unbalanced, and mono_thread_detach()
 * then tries to enter that same state again. On a window whose thread
 * never drew, the detach call itself returns without crashing, but
 * leaves an internal Mono hash table corrupted: mono-hash.c's "hash !=
 * NULL" assertion then spins forever (never terminates, never re-throws)
 * at process shutdown, hanging the whole process with its exit code
 * never returned -- confirmed by A/B testing on real hardware: identical
 * test run, only the destructor's detach call toggled, hang appears only
 * when the real detach path runs. Both failure modes are worse than the
 * cosmetic warning they were meant to fix, so neither is worth it -- see
 * KNOWN_ISSUES.md #3 for the full writeup. Use
 * hs_mono_thread_detach.h's MarkThreadForMonoDetachOnExit() instead,
 * unconditionally, for any thread that might need "detaching" -- it's a
 * safe no-op (mono_thread_detach_if_exiting() never actually succeeds
 * for these threads either, but at least it doesn't corrupt anything).
 */
#ifndef HS_MONO_THREAD_ATTACH_H
#define HS_MONO_THREAD_ATTACH_H

#include <mono/metadata/appdomain.h>
#include <mono/metadata/threads.h>

namespace hs_internal {

namespace detail {

inline MonoDomain*& CachedRootDomain()
{
	static MonoDomain* domain = NULL;
	return domain;
}

} // namespace detail

/* Call once, early, from a thread Mono already has valid domain state for
 * (this shim calls it from hs_application_create(), on whatever thread
 * constructs the first HSApplication -- always the real app's own
 * thread, never a Haiku-spawned foreign one). Safe to call more than
 * once; only the first call's value sticks. */
inline void CacheRootDomainForAttach()
{
	if (detail::CachedRootDomain() == NULL)
		detail::CachedRootDomain() = mono_get_root_domain();
}

/* Call as the very first thing in any native->managed callback trampoline
 * that might be running on a Haiku-spawned thread, BEFORE invoking the
 * actual managed delegate -- see the file comment above for why. */
inline void EnsureThreadAttached()
{
	static __thread bool tAttached = false;
	if (!tAttached) {
		MonoDomain* domain = detail::CachedRootDomain();
		if (domain != NULL)
			mono_thread_attach(domain);
		tAttached = true;
	}
}

} // namespace hs_internal

#endif /* HS_MONO_THREAD_ATTACH_H */
