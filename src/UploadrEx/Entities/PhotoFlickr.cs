using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace UploadrEx.Entities
{
  [DebuggerDisplay("{Title}")]
  internal class PhotoFlickr : IEquatable<PhotoFlickr>
  {
    public string Id { get; set; }

    public string Title { get; set; }

    public List<string> Tags { get; set; }

    public string FilePath { get; set; }

    public bool Equals(PhotoFlickr other)
    {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return string.Equals(Title, other.Title);
    }

    public override bool Equals(object obj)
    {
      if (ReferenceEquals(null, obj)) return false;
      if (ReferenceEquals(this, obj)) return true;
      if (obj.GetType() != this.GetType()) return false;
      return Equals((PhotoFlickr)obj);
    }

    public override int GetHashCode()
    {
      return (Title != null ? Title.GetHashCode() : 0);
    }

    public static bool operator ==(PhotoFlickr left, PhotoFlickr right)
    {
      return Equals(left, right);
    }

    public static bool operator !=(PhotoFlickr left, PhotoFlickr right)
    {
      return !Equals(left, right);
    }
  }
}