#!/bin/bash
# Runs Sample.exe with the LIBRARY_PATH Haiku's runtime_loader needs (see
# README.md's own note on why LIBRARY_PATH, not LD_LIBRARY_PATH) and
# MONO_THREADS_SUSPEND=preemptive set. That env var is not optional
# window dressing -- see KNOWN_ISSUES.md #3 (fix attempt 6): it gives
# every window/application teardown a real mono_thread_detach() instead
# of this binding's safe-but-cosmetic fallback, which is what actually
# silences Mono's benign "Failed aborting id" warning on quit. It's
# hardware-verified safe (6+ full Tests.exe runs and 150+ HammerProbe.exe
# create/show/close cycles, zero crashes, zero hangs, warning never
# appeared once) and must be set BEFORE `mono` starts -- Mono's own
# launcher reads it once at process bootstrap, so setting it from inside
# managed code would be too late. This script exists so that fact never
# has to be remembered by hand again; prefer it over typing the `mono`
# command directly.
set -e

cd "$(dirname "$0")"

export MONO_THREADS_SUSPEND=preemptive
export LIBRARY_PATH="$(pwd)/native:$HOME/config/non-packaged/lib:$HOME/config/lib:/boot/system/non-packaged/lib:/boot/system/lib:$LIBRARY_PATH"

exec mono Sample.exe "$@"
