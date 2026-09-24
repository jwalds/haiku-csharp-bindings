#!/bin/bash
# Runs Tests.exe -- same LIBRARY_PATH/MONO_THREADS_SUSPEND rationale as
# run_sample.sh's own comment; read that first if you haven't. Pass a
# substring (e.g. "./run_tests.sh Button") to run just one test module,
# same as calling `mono Tests.exe <substring>` directly.
set -e

cd "$(dirname "$0")"

export MONO_THREADS_SUSPEND=preemptive
export LIBRARY_PATH="$(pwd)/native:$HOME/config/non-packaged/lib:$HOME/config/lib:/boot/system/non-packaged/lib:/boot/system/lib:$LIBRARY_PATH"

exec mono Tests.exe "$@"
