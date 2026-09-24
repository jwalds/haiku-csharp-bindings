using System;
using System.Runtime.InteropServices;
using Haiku.App;

namespace Haiku.Interface
{
	/*
	 * Managed wrapper over the native HSButton (see native/include/
	 * hs_button.h for the full design rationale -- read it before
	 * changing anything here). Full BButton parity for the state surface
	 * (Label/Value/IsEnabled via Control, IsDefault/MakeDefault, IsFlat,
	 * Behavior) -- see details.md's Button/Control section for the click-
	 * mechanism scope decision (a direct OnClick hook, no BMessage/
	 * BInvoker/target plumbing) and why it was made that way.
	 *
	 * LIFECYCLE: same shape as View -- a freshly-constructed Button has
	 * no parent and is safe to configure (Label, MoveTo, ...) from
	 * whatever thread created it. Once added to a Window or View, it
	 * draws and handles input entirely on its own (see Control.cs's own
	 * remarks for why there's no OnDraw/OnMouseDown/... here). The most
	 * common real usage never calls Dispose() at all: once added, its
	 * parent owns it and deletes it automatically, recursively, whenever
	 * that parent itself is destroyed -- OnDestroyed still fires when
	 * that happens.
	 */
	public class Button : Control
	{
		private readonly ButtonClickCallback _clickThunk;
		private readonly ButtonDestroyedCallback _destroyedThunk;

		/// <summary>
		/// Convenience overload for the common case: a plain push button
		/// with real BeAPI's own default flags
		/// (B_WILL_DRAW | B_NAVIGABLE | B_FULL_UPDATE_ON_RESIZE, see
		/// Button.h) and no special resizing mode.
		/// </summary>
		public Button(Rect frame, string name, string label)
			: this(frame, name, label, ViewResizingMode.None,
				ViewFlags.WillDraw | ViewFlags.Navigable | ViewFlags.FullUpdateOnResize)
		{
		}

		public Button(Rect frame, string name, string label,
			ViewResizingMode resizingMode, ViewFlags flags)
			: base(CreateNativeButton(frame, name, label, resizingMode, flags))
		{
			IntPtr userData = SelfHandleUserData;

			_clickThunk = ClickThunk;
			_destroyedThunk = DestroyedThunk;

			Native.hs_button_set_click_callback(_handle, _clickThunk, userData);
			Native.hs_button_set_destroyed_callback(_handle, _destroyedThunk, userData);
		}

		// See View.cs's CreateNativeView -- same "base(...) needs an
		// expression" reason for pulling hs_button_create() out here.
		private static IntPtr CreateNativeButton(Rect frame, string name, string label,
			ViewResizingMode resizingMode, ViewFlags flags)
		{
			HsRect nativeFrame = new HsRect {
				Left = frame.Left,
				Top = frame.Top,
				Right = frame.Right,
				Bottom = frame.Bottom,
			};
			return Native.hs_button_create(nativeFrame, name, label,
				(uint)resizingMode, (uint)flags);
		}

		/// <summary>
		/// Called on the owning window's thread when this button is
		/// clicked (MouseDown followed by MouseUp still over the button,
		/// or the synthetic click a default button gets from Enter/Return
		/// -- see <see cref="MakeDefault"/>). No BMessage/target is
		/// involved -- see hs_button.h's "NO BMessage/BInvoker/TARGET
		/// PLUMBING" note. Default: does nothing.
		/// </summary>
		protected virtual void OnClick() { }

		/// <summary>
		/// Marks (or unmarks) this button as the window's default button
		/// -- it fires <see cref="OnClick"/> when Enter/Return is pressed
		/// anywhere in the window, even while some other view has
		/// keyboard focus. Verified against Button.h: a real BButton
		/// method, not something this binding invented. Only meaningful
		/// once this button is attached to a window.
		/// </summary>
		public void MakeDefault(bool isDefault = true)
		{
			CheckNotConsumed();
			Native.hs_button_make_default(_handle, isDefault);
		}

		/// <summary>Whether this button is currently its window's default button. See <see cref="MakeDefault"/>.</summary>
		public bool IsDefault
		{
			get
			{
				CheckNotConsumed();
				return Native.hs_button_is_default(_handle);
			}
		}

		/// <summary>Whether this button draws with a flat (borderless-until-hovered) look instead of the normal 3D bevel.</summary>
		public bool IsFlat
		{
			get
			{
				CheckNotConsumed();
				return Native.hs_button_is_flat(_handle);
			}
			set
			{
				CheckNotConsumed();
				Native.hs_button_set_flat(_handle, value);
			}
		}

		/// <summary>
		/// This button's <see cref="ButtonBehavior"/> -- push (the
		/// default), toggle, or pop-up-menu. See
		/// <see cref="ButtonBehavior.PopUpMenu"/>'s own remarks for why
		/// that particular value isn't fully usable through this binding
		/// yet.
		/// </summary>
		public ButtonBehavior Behavior
		{
			get
			{
				CheckNotConsumed();
				return (ButtonBehavior)Native.hs_button_behavior(_handle);
			}
			set
			{
				CheckNotConsumed();
				Native.hs_button_set_behavior(_handle, (uint)value);
			}
		}

		/// <summary>
		/// Fires <see cref="OnClick"/> directly, exactly as if the button
		/// had just been clicked (or, for the default button, as if
		/// Enter/Return had just been pressed) -- real BeAPI's own
		/// BInvoker::Invoke() is meant to be called directly by
		/// application code this way, not just internally by mouse
		/// tracking, so this is genuine API usage, not a synthetic test
		/// hook. For a non-<see cref="ButtonBehavior.Toggle"/> button
		/// whose <see cref="Control.Value"/> is currently B_CONTROL_ON
		/// (i.e. it looks pressed), this also resets it back to
		/// B_CONTROL_OFF afterward, matching real BButton::Invoke() --
		/// see hs_button.h's own "A REAL BUG THIS OVERRIDE INTRODUCED"
		/// note for the hardware-verified story behind that reset.
		/// </summary>
		public void Invoke()
		{
			CheckNotConsumed();
			Native.hs_button_invoke(_handle);
		}

		private static Button FromUserData(IntPtr userData)
		{
			return (Button)GCHandle.FromIntPtr(userData).Target;
		}

		private static void ClickThunk(IntPtr userData)
		{
			FromUserData(userData).OnClick();
		}

		private static void DestroyedThunk(IntPtr userData)
		{
			// Same "consumed-before-notified" ordering as View.cs's own
			// DestroyedThunk -- see ViewBase.MarkDestroyed()'s comment.
			FromUserData(userData).MarkDestroyed();
		}

		protected override void DestroyNativeHandle(IntPtr handle)
		{
			Native.hs_button_destroy(handle);
		}
	}
}
