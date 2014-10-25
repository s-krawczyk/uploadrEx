using System.Collections.Generic;
using System.Diagnostics;

namespace UploadrEx.Entities
{
  [DebuggerDisplay("{Title}")]
  internal class CollectionFlickr
  {
    public string Id { get; set; }

    public string Title { get; set; }

    public List<AlbumFlickr> AlbumsFlickr { get; set; }
  }
}