namespace Haiku.Interface
{
	/// <summary>
	/// Mirrors real BeAPI's list_view_type (headers/os/interface/
	/// ListView.h) -- a plain, unvalued C++ enum, so these values (0, 1)
	/// are guaranteed by the language itself, not merely likely to match.
	/// </summary>
	public enum ListViewType
	{
		/// <summary>At most one row selected at a time -- the default.</summary>
		SingleSelection = 0,

		/// <summary>Any number of rows may be selected at once (see <see cref="ListView.Select"/>'s <c>extend</c> parameter).</summary>
		MultipleSelection = 1,
	}
}
