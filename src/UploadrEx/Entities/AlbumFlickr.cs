using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace UploadrEx.Entities
{
  [DebuggerDisplay("{Title}")]
  internal class AlbumFlickr
  {
    public string Id { get; set; }

    public string Title { get; set; }

    public BindingList<PhotoFlickr> PhotoList { get; set; }

    public AlbumFlickr()
    {
      PhotoList = new BindingList<PhotoFlickr>();
    }

    public AlbumFlickr(Action onCollectionChanged)
      : this()
    {
      PhotoList.AddingNew += (sender, args) => onCollectionChanged();
      PhotoList.ListChanged += (sender, args) => onCollectionChanged();
    }
  }
}