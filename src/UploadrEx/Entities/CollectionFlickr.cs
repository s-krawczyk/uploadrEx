using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace UploadrEx.Entities
{
  [DebuggerDisplay("{Title}")]
  internal class CollectionFlickr
  {
    public string Id { get; set; }

    public string Title { get; set; }

    public BindingList<AlbumFlickr> AlbumsFlickr { get; set; }

    public CollectionFlickr()
    {
      AlbumsFlickr = new BindingList<AlbumFlickr>();
    }

    public CollectionFlickr(Action onCollectionChanged)
      : this()
    {
      AlbumsFlickr.AddingNew += (sender, args) => onCollectionChanged();
      AlbumsFlickr.ListChanged += (sender, args) => onCollectionChanged();
    }
  }
}