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
 * REAL mono_thread_detach() UNDER THE DEFAULT (COOPERATIVE) SUSPEND MODE --
 * two independent, 100%-reproducible failure modes were found on real
 * hardware (both gdb-confirmed) when this was tried as a way to silence
 * KNOWN_ISSUES.md #3's benign "Failed aborting id" warning. On a window
 * whose thread ever ran BView::Draw() (real app_server IPC), a real
 * mono_thread_detach() hits a fatal g_error: "Cannot transition thread
 * ... from STATE_BLOCKING with DO_BLOCKING" -- Draw()'s own IPC leaves the
 * thread in Mono's cooperative-GC blocking state, unbalanced, and
 * mono_thread_detach() then tries to enter that same state again. On a
 * window whose thread never drew, the detach call itself returns without
 * crashing, but leaves an internal Mono hash table corrupted: mono-hash.c's
 * "hash != NULL" assertion then spins forever (never terminates, never
 * re-throws) at process shutdown, hanging the whole process with its exit
 * code never returned -- confirmed by A/B testing on real hardware:
 * identical test run, only the destructor's detach call toggled, hang
 * appears only when the real detach path runs. Both failure modes are
 * worse than the cosmetic warning they were meant to fix, so under the
 * default suspend mode this shim never calls a real detach -- see
 * KNOWN_ISSUES.md #3 for the full writeup.
 *
 * REAL mono_thread_detach() UNDER MONO_THREADS_SUSPEND=preemptive -- both
 * failure modes above are specific to Mono's cooperative-suspend GC
 * "blocking region" state machine, which the preemptive suspend
 * implementation does not use the same way. Prior art (mono/mono#20283
 * documents a related macOS deadlock worked around the same way) pointed
 * at MONO_THREADS_SUSPEND=preemptive as a plausible fix, and it was tested
 * directly on this port: with the env var set, 6 consecutive full
 * Tests.exe runs (149/149 passing every time, zero crashes, zero hangs)
 * plus 150 HammerProbe.exe create/show/close cycles across both
 * drawing and non-drawing windows, all with a REAL mono_thread_detach()
 * call in place of MarkThreadForMonoDetachOnExit() -- the "Failed
 * aborting id" warning never appeared once, versus reliably appearing
 * under the default cooperative mode. So a real detach is safe, and
 * fully silences the warning, specifically when preemptive suspend is
 * active.
 *
 * WHY getenv() AND NOT A MONO API CALL -- the obvious-looking
 * mono_thread_get_coop_aware()/mono_thread_set_coop_aware() symbols
 * (exported by libmonosgen-2.0.so) were investigated as a possible way to
 * query the active suspend policy at runtime instead of re-reading an env
 * var. gdb disassembly of the real binary (mono-threads-coop.c, lines
 * 777/792 per its DWARF line info) showed these are a PER-THREAD atomic
 * flag on the current thread's mono_thread_info struct (accessed via
 * mono_thread_info_current_unchecked(), offset 0x364, read with a `lock
 * xadd` and set with a `lock cmpxchg` loop) -- unrelated to the global
 * suspend-policy choice made once at process startup. There is no
 * supported Mono API to query MONO_THREADS_SUSPEND after the fact, so
 * this shim reads the env var itself: MONO_THREADS_SUSPEND is parsed once
 * by the `mono` launcher before any embedded/loaded library code runs,
 * and a temporary on-hardware probe (plain getenv() called from inside
 * this shim, logging its result) confirmed it reliably and unmodified
 * reflects whatever was set before `mono` launched -- no Mono-internal or
 * undocumented API involved, just libc. RealDetachIsSafe() below caches
 * that check once per process (C++11 function-local static, thread-safe
 * exactly-once init -- same pattern as CachedRootDomain() above) and both
 * ~HSWindow() and ~HSApplication() branch on it: a real
 * mono_thread_detach(mono_thread_current()) when MONO_THREADS_SUSPEND is
 * exactly "preemptive", and the existing safe
 * MarkThreadForMonoDetachOnExit() fallback otherwise -- so the default,
 * unconfigured build behaves exactly as it did before (safe fallback,
 * occasional cosmetic warning, zero crashes), and consumers who set
 * MONO_THREADS_SUSPEND=preemptive get a fully silent shutdown with no
 * source change needed on their end.
 */
#ifndef HS_MONO_THREAD_ATTACH_H
#define HS_MONO_THREAD_ATTACH_H

#include <cstdlib>
#include <cstring>

#include <mono/metadata/appdomain.h>
#include <mono/metadata/threads.h>

namespace hs_internal {

namespace detail {

inline MonoDomain*& CachedRootDomain()
{
	static MonoDomain* domain = NULL;
	return domain;
}

/* Read once; see the "WHY getenv()" section of the file comment above.
 * Deliberately exact-match ("preemptive" only) -- "hybrid" mode was not
 * tested and is not assumed safe by this check. */
inline bool ComputeRealDetachIsSafe()
{
	const char* v = getenv("MONO_THREADS_SUSPEND");
	return v != NULL && strcmp(v, "preemptive") == 0;
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

/* True iff a REAL mono_thread_detach() has been verified safe under the
 * suspend implementation this process launched with (currently: exactly
 * MONO_THREADS_SUSPEND=preemptive). Computed once per process and cached.
 * See the file comment above for the full investigation and verification
 * numbers. Callers should branch on this and fall back to
 * hs_mono_thread_detach.h's MarkThreadForMonoDetachOnExit() when false. */
inline bool RealDetachIsSafe()
{
	static bool sIsSafe = detail::ComputeRealDetachIsSafe();
	return sIsSafe;
}

} // namespace hs_internal

#endif /* HS_MONO_THREAD_ATTACH_H */
