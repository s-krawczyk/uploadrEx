using System.Collections.Generic;
using System.Diagnostics;

namespace UploadrEx.Entities
{
  [DebuggerDisplay("{Title}")]
  internal class PhotoFlickr
  {
    public string Id { get; set; }

    public string Title { get; set; }

    public List<string> Tags { get; set; }

    public string FilePath { get; set; }
  }
}