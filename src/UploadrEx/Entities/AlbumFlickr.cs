using System.Collections.Generic;
using System.Diagnostics;

namespace UploadrEx.Entities
{
  [DebuggerDisplay("{Title}")]
  internal class AlbumFlickr
  {
    public string Id { get; set; }

    public string Title { get; set; }

    public List<PhotoFlickr> PhotoList { get; set; }
  }
}