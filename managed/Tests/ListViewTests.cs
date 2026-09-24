using System;
using Haiku.App;
using Haiku.Interface;
using Haiku.Testing;

/// <summary>
/// Regression coverage for Haiku.Interface.ListView (see
/// managed/Haiku.Interface/ListView.cs and native/include/
/// hs_list_view.h) -- this binding's first BView+BInvoker widget (not a
/// BControl at all, see ListView.cs's own class comment), and its
/// eleventh Interface Kit slice. Re-exercises the shared ViewBase
/// geometry/parenting/lifecycle surface every prior widget already
/// proved out, plus BListView's own text-item and selection surface.
///
/// THERE IS NO AUTOMATED TEST HERE THAT ACTUALLY FIRES OnInvoked. Same
/// reasoning as every other input hook in this binding (Button's
/// OnClick, Slider's OnValueCommitted, ...): there is no supported way
/// to synthesize a real double-click from inside the same process.
/// OnSelectionChanged is different -- a hardware probe confirmed it
/// fires correctly for a plain programmatic Select()/Deselect() call,
/// not just a real click (see hs_list_view.h's own note), so
/// SelectionChangedFiresOnProgrammaticSelect below exercises it for
/// real, unlike any single-hook widget's click/invoke callback in this
/// binding so far.
///
/// EVERY TEST HERE OPENS AN Application FIRST, DESPITE hs_list_view.h's
/// OWN "NO BApplication NEEDED" FINDING -- SAME SUITE-ORDERING HAZARD
/// CheckBoxTests.cs ALREADY DOCUMENTED FOR ITS OWN WIDGET. A standalone
/// native probe (see hs_list_view.h) proved a BListView constructs and
/// deletes cleanly with NO BApplication ever having existed anywhere in
/// the process. But Tests.exe runs every module in one shared process in
/// alphabetical order, and BListViewTests would run well after several
/// prior modules have already constructed and disposed Applications of
/// their own (BButton, BCheckBox, BColorControl all sort before it) --
/// CheckBoxTests.cs's own remarks explain exactly why that makes an
/// Application-less construction unsafe to rely on here even though the
/// isolated probe genuinely never had one. Every test below wraps in
/// `using (new Application(AppSignature))` for that reason, not because
/// ListView actually needs it the way TextControl/RadioButton/Slider/
/// ColorControl's own hard requirement does.
/// </summary>
[Haiku.Testing.TestModule("BListView")]
public class ListViewTests
{
	private const string AppSignature = "application/x-vnd.HaikuSharp-Tests-ListView";

	private class ProbeListView : ListView
	{
		public volatile int SelectionChangedCount;
		public volatile bool DestroyedFired;

		public ProbeListView(Rect frame, string name)
			: base(frame, name)
		{
		}

		public ProbeListView(Rect frame, string name, ListViewType type,
			ViewResizingMode resizingMode, ViewFlags flags)
			: base(frame, name, type, resizingMode, flags)
		{
		}

		protected override void OnSelectionChanged()
		{
			SelectionChangedCount++;
		}

		protected override void OnDestroyed()
		{
			DestroyedFired = true;
		}
	}

	[Test]
	public void ConstructionAndGeometryRoundTrip()
	{
		using (new Application(AppSignature)) {
			Rect frame = new Rect(10, 20, 210, 120);
			ListView listView = new ListView(frame, "geometry probe");

			Rect afterConstruct = listView.Frame;
			Assert.AreEqual(frame.Left, afterConstruct.Left, "Frame.Left should read back exactly what the constructor was given");
			Assert.AreEqual(frame.Top, afterConstruct.Top, "Frame.Top should read back exactly what the constructor was given");
			Assert.AreEqual(frame.Right - frame.Left, afterConstruct.Right - afterConstruct.Left, "Frame width should read back exactly what the constructor was given");
			Assert.AreEqual(frame.Bottom - frame.Top, afterConstruct.Bottom - afterConstruct.Top, "Frame height should read back exactly what the constructor was given");

			listView.MoveTo(5, 7);
			Rect afterMove = listView.Frame;
			Assert.AreEqual(5f, afterMove.Left, "MoveTo should update Frame.Left");
			Assert.AreEqual(7f, afterMove.Top, "MoveTo should update Frame.Top");

			listView.ResizeTo(150, 80);
			Rect afterResize = listView.Frame;
			Assert.AreEqual(150f, afterResize.Right - afterResize.Left, "ResizeTo should set the new width");
			Assert.AreEqual(80f, afterResize.Bottom - afterResize.Top, "ResizeTo should set the new height");

			Assert.AreEqual(0, listView.CountItems, "A freshly-constructed ListView should have no items");

			// Never added to any parent -- Dispose() should succeed and
			// fire OnDestroyed synchronously, right here.
			listView.Dispose();
		}
	}

	[Test]
	public void AddItemAppendsAndInsertsCorrectly()
	{
		using (new Application(AppSignature)) {
			ListView listView = new ListView(new Rect(0, 0, 200, 100), "add item probe");

			listView.AddItem("Alpha");
			listView.AddItem("Beta");
			listView.AddItem("Gamma");
			Assert.AreEqual(3, listView.CountItems, "CountItems should reflect three appended items");
			Assert.AreEqual("Alpha", listView.ItemTextAt(0), "Item 0 should be the first appended item");
			Assert.AreEqual("Beta", listView.ItemTextAt(1), "Item 1 should be the second appended item");
			Assert.AreEqual("Gamma", listView.ItemTextAt(2), "Item 2 should be the third appended item");

			listView.AddItem("Inserted", 1);
			Assert.AreEqual(4, listView.CountItems, "CountItems should reflect the inserted item too");
			Assert.AreEqual("Alpha", listView.ItemTextAt(0), "Insert at index 1 should not move index 0");
			Assert.AreEqual("Inserted", listView.ItemTextAt(1), "Insert at index 1 should land exactly at index 1");
			Assert.AreEqual("Beta", listView.ItemTextAt(2), "Insert at index 1 should shift the old index-1 item to index 2");
			Assert.AreEqual("Gamma", listView.ItemTextAt(3), "Insert at index 1 should shift the old index-2 item to index 3");

			listView.Dispose();
		}
	}

	[Test]
	public void SetItemTextRoundTrips()
	{
		using (new Application(AppSignature)) {
			ListView listView = new ListView(new Rect(0, 0, 200, 100), "set text probe");
			listView.AddItem("Original");

			listView.SetItemTextAt(0, "Changed");
			Assert.AreEqual("Changed", listView.ItemTextAt(0), "ItemTextAt should read back what SetItemTextAt was just given");

			listView.Dispose();
		}
	}

	[Test]
	public void RemoveItemAtRemovesAndReportsSuccess()
	{
		using (new Application(AppSignature)) {
			ListView listView = new ListView(new Rect(0, 0, 200, 100), "remove item probe");
			listView.AddItem("Alpha");
			listView.AddItem("Beta");
			listView.AddItem("Gamma");

			bool removed = listView.RemoveItemAt(1);
			Assert.IsTrue(removed, "RemoveItemAt should return true for a valid index");
			Assert.AreEqual(2, listView.CountItems, "CountItems should decrease after a successful removal");
			Assert.AreEqual("Alpha", listView.ItemTextAt(0), "Item 0 should be unaffected by removing item 1");
			Assert.AreEqual("Gamma", listView.ItemTextAt(1), "The old item 2 should shift down to index 1 after removal");

			bool removedOutOfRange = listView.RemoveItemAt(99);
			Assert.IsFalse(removedOutOfRange, "RemoveItemAt should return false for an out-of-range index");

			listView.Dispose();
		}
	}

	[Test]
	public void MakeEmptyClearsAllItems()
	{
		using (new Application(AppSignature)) {
			ListView listView = new ListView(new Rect(0, 0, 200, 100), "make empty probe");
			listView.AddItem("Alpha");
			listView.AddItem("Beta");

			listView.MakeEmpty();
			Assert.AreEqual(0, listView.CountItems, "CountItems should be 0 after MakeEmpty");

			listView.Dispose();
		}
	}

	[Test]
	public void SingleSelectionRoundTrips()
	{
		using (new Application(AppSignature)) {
			ListView listView = new ListView(new Rect(0, 0, 200, 100), "single selection probe");
			listView.AddItem("Alpha");
			listView.AddItem("Beta");
			listView.AddItem("Gamma");

			Assert.AreEqual(ListViewType.SingleSelection, listView.ListType, "Default constructor overload should produce a single-selection list");

			listView.Select(1);
			Assert.IsTrue(listView.IsItemSelected(1), "IsItemSelected should be true for the just-selected index");
			Assert.IsFalse(listView.IsItemSelected(0), "IsItemSelected should be false for a never-selected index");
			Assert.AreEqual(1, listView.CurrentSelection(0), "CurrentSelection(0) should return the selected index");
			Assert.AreEqual(-1, listView.CurrentSelection(1), "CurrentSelection(1) should be -1 -- only one row is selected");

			listView.Dispose();
		}
	}

	[Test]
	public void MultipleSelectionExtendRoundTrips()
	{
		using (new Application(AppSignature)) {
			ListView listView = new ListView(new Rect(0, 0, 200, 100), "multi selection probe",
				ListViewType.MultipleSelection, ViewResizingMode.None,
				ViewFlags.WillDraw | ViewFlags.FrameEvents | ViewFlags.Navigable);
			listView.AddItem("Alpha");
			listView.AddItem("Beta");
			listView.AddItem("Gamma");

			Assert.AreEqual(ListViewType.MultipleSelection, listView.ListType, "Full constructor overload should honor an explicit MultipleSelection type");

			listView.Select(0);
			listView.Select(2, true);
			Assert.IsTrue(listView.IsItemSelected(0), "Index 0 should be selected");
			Assert.IsFalse(listView.IsItemSelected(1), "Index 1 should not be selected");
			Assert.IsTrue(listView.IsItemSelected(2), "Index 2 should be selected after Select(2, extend: true)");
			Assert.AreEqual(0, listView.CurrentSelection(0), "CurrentSelection(0) should be the first selected index");
			Assert.AreEqual(2, listView.CurrentSelection(1), "CurrentSelection(1) should be the second selected index");
			Assert.AreEqual(-1, listView.CurrentSelection(2), "CurrentSelection(2) should be -1 -- only two rows are selected");

			listView.Deselect(0);
			Assert.IsFalse(listView.IsItemSelected(0), "Index 0 should no longer be selected after Deselect(0)");
			Assert.IsTrue(listView.IsItemSelected(2), "Index 2 should remain selected after only Deselect(0)");

			listView.DeselectAll();
			Assert.AreEqual(-1, listView.CurrentSelection(0), "CurrentSelection(0) should be -1 after DeselectAll");

			listView.Dispose();
		}
	}

	[Test]
	public void ListTypeRoundTrips()
	{
		using (new Application(AppSignature)) {
			ListView listView = new ListView(new Rect(0, 0, 200, 100), "list type probe");

			listView.ListType = ListViewType.MultipleSelection;
			Assert.AreEqual(ListViewType.MultipleSelection, listView.ListType, "ListType should read back what the property setter was just given");

			listView.ListType = ListViewType.SingleSelection;
			Assert.AreEqual(ListViewType.SingleSelection, listView.ListType, "ListType should read back SingleSelection after being set back to it");

			listView.Dispose();
		}
	}

	[Test]
	public void SelectionChangedFiresOnProgrammaticSelect()
	{
		// Hardware-verified via a native probe before this test was
		// written -- see hs_list_view.h's own note. Unlike every other
		// widget's click/invoke hook in this binding, this one really is
		// exercised end-to-end here, not just visually via Sample.exe.
		using (new Application(AppSignature)) {
			ProbeListView listView = new ProbeListView(new Rect(0, 0, 200, 100), "selection changed probe",
				ListViewType.MultipleSelection, ViewResizingMode.None,
				ViewFlags.WillDraw | ViewFlags.FrameEvents | ViewFlags.Navigable);
			listView.AddItem("Alpha");
			listView.AddItem("Beta");

			listView.Select(0);
			Assert.AreEqual(1, listView.SelectionChangedCount, "SelectionChanged should fire once after Select(0)");

			listView.Select(1, true);
			Assert.AreEqual(2, listView.SelectionChangedCount, "SelectionChanged should fire again after a second, extending Select");

			listView.DeselectAll();
			Assert.AreEqual(3, listView.SelectionChangedCount, "SelectionChanged should fire again after DeselectAll");

			listView.Dispose();
		}
	}

	[Test]
	public void AddChildUnderWindowSucceeds()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 300, 200), "ListViewTests probe window")) {
				ListView listView = new ListView(new Rect(10, 10, 280, 180), "probe");

				// Should not throw -- exercises hs_window_add_child()
				// against a real HSListView handle for the first time,
				// and against a first widget whose ABI offset-0 fact
				// (BView first, BInvoker second) was verified separately
				// from every BControl-derived widget's own probe.
				window.AddChild(listView);

				// Leave listView attached -- window's own Dispose() below
				// cascades into it, exercised separately by
				// WindowDisposeCascadesDestroyToAttachedListView.
			}
		}
	}

	[Test]
	public void AddChildUnderPlainViewSucceeds()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 300, 200), "ListViewTests probe window")) {
				View view = new View(new Rect(0, 0, 300, 200), "container");
				window.AddChild(view);

				ListView listView = new ListView(new Rect(10, 10, 280, 180), "probe");
				view.AddChild(listView);

				// Leave listView attached -- the window's cascading
				// Dispose() below tears down view, which tears down
				// listView in turn.
			}
		}
	}

	[Test]
	public void RemoveChildDetachesListView()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 300, 200), "ListViewTests probe window")) {
				ProbeListView listView = new ProbeListView(new Rect(10, 10, 280, 180), "probe");
				window.AddChild(listView);

				bool removed = window.RemoveChild(listView);

				Assert.IsTrue(removed, "RemoveChild should return true for a list view that actually was a direct child");

				// No longer attached -- Dispose() should now succeed
				// rather than throw (see DisposeWhileAttachedThrows for
				// the still-attached case).
				listView.Dispose();
				Assert.IsTrue(listView.DestroyedFired, "Dispose() on a detached list view should fire OnDestroyed");
			}
		}
	}

	[Test]
	public void RemoveChildReturnsFalseForNonChild()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 300, 200), "ListViewTests probe window"))
			using (ListView listView = new ListView(new Rect(10, 10, 280, 180), "never added")) {
				bool removed = window.RemoveChild(listView);

				Assert.IsFalse(removed, "RemoveChild should return false for a list view that was never added to this window");
			}
		}
	}

	[Test]
	public void DisposeWhileAttachedThrows()
	{
		using (new Application(AppSignature)) {
			using (Window window = new Window(new Rect(0, 0, 300, 200), "ListViewTests probe window")) {
				ListView listView = new ListView(new Rect(10, 10, 280, 180), "probe");
				window.AddChild(listView);

				bool threw = false;
				try {
					listView.Dispose();
				} catch (InvalidOperationException) {
					threw = true;
				}

				Assert.IsTrue(threw,
					"Dispose() on a still-attached ListView should throw InvalidOperationException rather than risk corrupting its parent's child list");

				// Leave it attached -- window's own Dispose() below cleans
				// it up via the implicit cascade.
			}
		}
	}

	[Test]
	public void WindowDisposeCascadesDestroyToAttachedListView()
	{
		using (new Application(AppSignature)) {
			ProbeListView listView = new ProbeListView(new Rect(10, 10, 280, 180), "probe");
			listView.AddItem("Left in place when window is destroyed");

			using (Window window = new Window(new Rect(0, 0, 300, 200), "ListViewTests probe window")) {
				window.AddChild(listView);
				// window.Dispose() fires here, at the end of this `using`
				// block -- never shown, so it deletes the native BWindow
				// synchronously, which recursively deletes its still-
				// attached children, including this list view, with no
				// explicit RemoveChild() or Dispose() call on it at all.
				// Exercises HSListView's destructor cascade for the
				// first time with a non-empty item still present -- see
				// hs_list_view.h's OWNERSHIP note: this shim's own
				// destructor deletes any remaining items itself before
				// firing the destroyed callback, closing the real leak
				// plain BListView would otherwise leave behind here.
			}

			Assert.IsTrue(listView.DestroyedFired,
				"OnDestroyed should fire on a still-attached child list view when its parent window is destroyed, even though nothing disposed it directly");
		}
	}

	[Test]
	public void SwapItemsSwapsInPlace()
	{
		using (new Application(AppSignature)) {
			ListView listView = new ListView(new Rect(0, 0, 200, 100), "swap probe");
			listView.AddItem("Alpha");
			listView.AddItem("Beta");
			listView.AddItem("Gamma");

			bool swapped = listView.SwapItems(0, 2);
			Assert.IsTrue(swapped, "SwapItems should return true for two valid indices");
			Assert.AreEqual("Gamma", listView.ItemTextAt(0), "Index 0 should now hold what was at index 2");
			Assert.AreEqual("Beta", listView.ItemTextAt(1), "Index 1 (not part of the swap) should be unaffected");
			Assert.AreEqual("Alpha", listView.ItemTextAt(2), "Index 2 should now hold what was at index 0");

			bool swappedOutOfRange = listView.SwapItems(0, 99);
			Assert.IsFalse(swappedOutOfRange, "SwapItems should return false when either index is out of range");

			listView.Dispose();
		}
	}

	[Test]
	public void MoveItemShiftsCorrectly()
	{
		using (new Application(AppSignature)) {
			ListView listView = new ListView(new Rect(0, 0, 200, 100), "move probe");
			listView.AddItem("Alpha");
			listView.AddItem("Beta");
			listView.AddItem("Gamma");
			listView.AddItem("Delta");

			bool moved = listView.MoveItem(0, 2);
			Assert.IsTrue(moved, "MoveItem should return true for two valid indices");
			// Alpha moves from index 0 to index 2, shifting Beta and Gamma up by one.
			Assert.AreEqual("Beta", listView.ItemTextAt(0), "Beta should shift down to index 0");
			Assert.AreEqual("Gamma", listView.ItemTextAt(1), "Gamma should shift down to index 1");
			Assert.AreEqual("Alpha", listView.ItemTextAt(2), "Alpha should now be at index 2");
			Assert.AreEqual("Delta", listView.ItemTextAt(3), "Delta (not part of the move) should be unaffected");

			bool movedOutOfRange = listView.MoveItem(0, 99);
			Assert.IsFalse(movedOutOfRange, "MoveItem should return false when either index is out of range");

			listView.Dispose();
		}
	}

	[Test]
	public void RemoveItemsRemovesRangeAndReportsCount()
	{
		using (new Application(AppSignature)) {
			ListView listView = new ListView(new Rect(0, 0, 200, 100), "remove range probe");
			listView.AddItem("Alpha");
			listView.AddItem("Beta");
			listView.AddItem("Gamma");
			listView.AddItem("Delta");
			listView.AddItem("Epsilon");

			int removed = listView.RemoveItems(1, 2);
			Assert.AreEqual(2, removed, "RemoveItems should report exactly how many items it removed");
			Assert.AreEqual(3, listView.CountItems, "CountItems should reflect the two removed items");
			Assert.AreEqual("Alpha", listView.ItemTextAt(0), "Item 0 should be unaffected by removing items 1-2");
			Assert.AreEqual("Delta", listView.ItemTextAt(1), "The old item 3 should shift down to index 1");
			Assert.AreEqual("Epsilon", listView.ItemTextAt(2), "The old item 4 should shift down to index 2");

			// Clamping: asking for more items than remain should remove
			// only what's actually there, not fail outright.
			int removedClamped = listView.RemoveItems(1, 99);
			Assert.AreEqual(2, removedClamped, "RemoveItems should clamp to the number of items actually remaining, not fail");
			Assert.AreEqual(1, listView.CountItems, "Only the first item should remain after the clamped removal");

			int removedOutOfRange = listView.RemoveItems(50, 3);
			Assert.AreEqual(0, removedOutOfRange, "RemoveItems should report 0 removed for an already-out-of-range index");

			listView.Dispose();
		}
	}

	[Test]
	public void SortAscendingOrdersAlphabetically()
	{
		using (new Application(AppSignature)) {
			ListView listView = new ListView(new Rect(0, 0, 200, 100), "sort ascending probe");
			listView.AddItem("Gamma");
			listView.AddItem("Alpha");
			listView.AddItem("Delta");
			listView.AddItem("Beta");

			listView.Sort();
			Assert.AreEqual("Alpha", listView.ItemTextAt(0), "Sort() should default to ascending order");
			Assert.AreEqual("Beta", listView.ItemTextAt(1), "Sort() should default to ascending order");
			Assert.AreEqual("Delta", listView.ItemTextAt(2), "Sort() should default to ascending order");
			Assert.AreEqual("Gamma", listView.ItemTextAt(3), "Sort() should default to ascending order");

			listView.Dispose();
		}
	}

	[Test]
	public void SortDescendingReversesOrder()
	{
		using (new Application(AppSignature)) {
			ListView listView = new ListView(new Rect(0, 0, 200, 100), "sort descending probe");
			listView.AddItem("Gamma");
			listView.AddItem("Alpha");
			listView.AddItem("Delta");
			listView.AddItem("Beta");

			listView.Sort(ascending: false);
			Assert.AreEqual("Gamma", listView.ItemTextAt(0), "Sort(ascending: false) should produce descending order");
			Assert.AreEqual("Delta", listView.ItemTextAt(1), "Sort(ascending: false) should produce descending order");
			Assert.AreEqual("Beta", listView.ItemTextAt(2), "Sort(ascending: false) should produce descending order");
			Assert.AreEqual("Alpha", listView.ItemTextAt(3), "Sort(ascending: false) should produce descending order");

			listView.Dispose();
		}
	}

	[Test]
	public void SelectRangeSelectsInclusiveSpan()
	{
		using (new Application(AppSignature)) {
			ListView listView = new ListView(new Rect(0, 0, 200, 100), "select range probe",
				ListViewType.MultipleSelection, ViewResizingMode.None,
				ViewFlags.WillDraw | ViewFlags.FrameEvents | ViewFlags.Navigable);
			listView.AddItem("Zero");
			listView.AddItem("One");
			listView.AddItem("Two");
			listView.AddItem("Three");
			listView.AddItem("Four");

			listView.Select(1, 3);
			Assert.IsFalse(listView.IsItemSelected(0), "Index 0 should be outside the selected range 1-3");
			Assert.IsTrue(listView.IsItemSelected(1), "Index 1 should be inside the selected range 1-3");
			Assert.IsTrue(listView.IsItemSelected(2), "Index 2 should be inside the selected range 1-3");
			Assert.IsTrue(listView.IsItemSelected(3), "Index 3 should be inside the selected range 1-3");
			Assert.IsFalse(listView.IsItemSelected(4), "Index 4 should be outside the selected range 1-3");

			listView.Dispose();
		}
	}

	[Test]
	public void DeselectExceptKeepsOnlyTheGivenRange()
	{
		using (new Application(AppSignature)) {
			ListView listView = new ListView(new Rect(0, 0, 200, 100), "deselect except probe",
				ListViewType.MultipleSelection, ViewResizingMode.None,
				ViewFlags.WillDraw | ViewFlags.FrameEvents | ViewFlags.Navigable);
			listView.AddItem("Zero");
			listView.AddItem("One");
			listView.AddItem("Two");
			listView.AddItem("Three");
			listView.AddItem("Four");

			listView.Select(0, 4);
			listView.DeselectExcept(2, 2);

			Assert.IsFalse(listView.IsItemSelected(0), "Index 0 should be deselected -- outside the except range 2-2");
			Assert.IsFalse(listView.IsItemSelected(1), "Index 1 should be deselected -- outside the except range 2-2");
			Assert.IsTrue(listView.IsItemSelected(2), "Index 2 should remain selected -- inside the except range 2-2");
			Assert.IsFalse(listView.IsItemSelected(3), "Index 3 should be deselected -- outside the except range 2-2");
			Assert.IsFalse(listView.IsItemSelected(4), "Index 4 should be deselected -- outside the except range 2-2");

			listView.Dispose();
		}
	}

	[Test]
	public void ItemFrameReturnsIncreasingTopForEachRow()
	{
		// A ListView must be attached to a window for ItemFrame() to
		// return real geometry -- a hardware probe found every item's
		// frame comes back all-zero (0-height) on a never-attached list
		// view, presumably because BStringItem's height is only
		// measured against a real owner/font once attached. Confirmed
		// via Console.WriteLine before this assertion shape was chosen;
		// see this class's own remarks near the top on why every test
		// already wraps in an Application for a similar
		// attachment-order reason.
		using (new Application(AppSignature))
		using (Window window = new Window(new Rect(0, 0, 300, 200), "item frame probe window")) {
			ListView listView = new ListView(new Rect(0, 0, 200, 100), "item frame probe");
			window.AddChild(listView);
			listView.AddItem("Alpha");
			listView.AddItem("Beta");
			listView.AddItem("Gamma");

			Rect frame0 = listView.ItemFrame(0);
			Rect frame1 = listView.ItemFrame(1);
			Rect frame2 = listView.ItemFrame(2);

			Assert.IsTrue(frame0.Bottom > frame0.Top, "Item 0's frame should have positive height once attached to a window");
			Assert.IsTrue(frame1.Top >= frame0.Bottom, "Item 1's frame should start at or below where item 0's frame ends");
			Assert.IsTrue(frame2.Top >= frame1.Bottom, "Item 2's frame should start at or below where item 1's frame ends");

			// listView is still attached -- window's own Dispose() below
			// cascades into it, same pattern as every other attached-child
			// test in this file.
		}
	}

	[Test]
	public void IndexOfPointMatchesItemFrame()
	{
		// Same attachment requirement as ItemFrameReturnsIncreasingTopForEachRow
		// above -- IndexOf(BPoint) hit-tests against the same per-item
		// geometry ItemFrame() reads, which is only valid once attached.
		using (new Application(AppSignature))
		using (Window window = new Window(new Rect(0, 0, 300, 200), "index of point probe window")) {
			ListView listView = new ListView(new Rect(0, 0, 200, 100), "index of point probe");
			window.AddChild(listView);
			listView.AddItem("Alpha");
			listView.AddItem("Beta");
			listView.AddItem("Gamma");

			Rect frame1 = listView.ItemFrame(1);
			Point centerOfItem1 = new Point(
				(frame1.Left + frame1.Right) / 2f,
				(frame1.Top + frame1.Bottom) / 2f);

			Assert.AreEqual(1, listView.IndexOfPoint(centerOfItem1), "A point inside item 1's own frame should hit-test to index 1");

			Point wayOutside = new Point(-500f, -500f);
			Assert.AreEqual(-1, listView.IndexOfPoint(wayOutside), "A point far outside every item's frame should hit-test to -1");

			// listView is still attached -- window's own Dispose() below
			// cascades into it, same pattern as every other attached-child
			// test in this file.
		}
	}

	[Test]
	public void IsEmptyReflectsItemCount()
	{
		using (new Application(AppSignature)) {
			ListView listView = new ListView(new Rect(0, 0, 200, 100), "is empty probe");
			Assert.IsTrue(listView.IsEmpty, "A freshly-constructed ListView should be empty");

			listView.AddItem("Alpha");
			Assert.IsFalse(listView.IsEmpty, "IsEmpty should be false once an item has been added");

			listView.MakeEmpty();
			Assert.IsTrue(listView.IsEmpty, "IsEmpty should be true again after MakeEmpty");

			listView.Dispose();
		}
	}

	[Test]
	public void ScrollToAndScrollToSelectionDoNotThrow()
	{
		// Smoke coverage only -- ListView derives from ViewBase, not
		// View, so there is no managed Bounds accessor here to assert an
		// actual scroll offset against (see View.cs's own Bounds
		// property, which ListView deliberately does not have -- same
		// reasoning as ViewBase.cs's own note on why Bounds stays
		// View-only). The underlying native calls were separately
		// confirmed on hardware to actually move the scroll position
		// before this binding relied on them -- see hs_list_view.h's own
		// completeness-pass note and details.md's verification writeup.
		using (new Application(AppSignature)) {
			ListView listView = new ListView(new Rect(0, 0, 100, 40), "scroll probe");
			for (int i = 0; i < 20; i++)
				listView.AddItem("Item " + i);

			listView.ScrollTo(15);
			listView.Select(10);
			listView.ScrollToSelection();

			Assert.AreEqual(20, listView.CountItems, "Scrolling should not alter the item count");

			listView.Dispose();
		}
	}

	[Test]
	public void AddItemsBulkConvenienceAddsEveryStringInOrder()
	{
		using (new Application(AppSignature)) {
			ListView listView = new ListView(new Rect(0, 0, 200, 100), "add items probe");
			listView.AddItem("Zero");

			listView.AddItems(new string[] { "One", "Two", "Three" });

			Assert.AreEqual(4, listView.CountItems, "AddItems should append every given string");
			Assert.AreEqual("Zero", listView.ItemTextAt(0), "AddItems should not disturb items already present");
			Assert.AreEqual("One", listView.ItemTextAt(1), "AddItems should preserve the given order");
			Assert.AreEqual("Two", listView.ItemTextAt(2), "AddItems should preserve the given order");
			Assert.AreEqual("Three", listView.ItemTextAt(3), "AddItems should preserve the given order");

			listView.Dispose();
		}
	}
}
