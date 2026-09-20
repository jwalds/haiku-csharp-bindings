using System.Runtime.CompilerServices;

/*
 * The one and only InternalsVisibleTo in this project, and worth explaining
 * why it exists: Haiku.Interface.Window needs to hand a borrowed BMessage to
 * its OnMessageReceived override (mirroring Application.OnMessageReceived),
 * which means constructing a Message around a raw native handle it doesn't
 * own -- exactly what Message's `internal Message(IntPtr borrowedHandle)`
 * constructor is for. That constructor is intentionally not public (a public
 * "wrap this arbitrary pointer" constructor would let any caller create a
 * Message around garbage), so Haiku.Interface needs this grant to reach it.
 *
 * This is a different tradeoff than the one made for the small HsRect struct
 * duplicated in Haiku.Interface's own Native.cs: HsRect is six lines of
 * plain-data layout with no behavior, cheaper to copy than to share: link.
 * Message is a large, real class with its whole BMessage surface area --
 * duplicating it would be absurd, so sharing the one constructor it needs to
 * expose is the right call here instead.
 */
[assembly: InternalsVisibleTo("Haiku.Interface")]
