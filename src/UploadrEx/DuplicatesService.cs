using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UploadrEx.Entities;

namespace UploadrEx
{
  internal class DuplicatesService
  {
    public PhotoFlickr[] GetDuplicatesToRemove(CollectionFlickr[] inputSchema)
    {
      List<PhotoFlickr> toDelete = new List<PhotoFlickr>();

      foreach (CollectionFlickr collection in inputSchema)
      {
        foreach (AlbumFlickr albumFlickr in collection.AlbumsFlickr)
        {
          var duplicates = albumFlickr.PhotoList
            .GroupBy(g => g.Title, e => e, (s, flickrs) => new { Key = s, Items = flickrs.ToList() })
            .Where(w => w.Items.Count() > 1);

          foreach (var duplicate in duplicates)
          {
            duplicate.Items.Remove(duplicate.Items.First());

            toDelete.AddRange(duplicate.Items);
          }
        }
      }

      return toDelete.ToArray();
    }
  }
}