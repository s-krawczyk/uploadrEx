using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UploadrEx.Entities;

namespace UploadrEx
{
  internal class SchemaComparer
  {
    public List<CollectionFlickr> CompareSchemas(IEnumerable<CollectionFlickr> collectionsFromFlickr, IEnumerable<CollectionFlickr> fromDisk)
    {
      Dictionary<string, CollectionFlickr> dictionaryCollectionsFromFlickr = collectionsFromFlickr.ToDictionary(k => k.Title, v => v);
      Dictionary<string, CollectionFlickr> dictionaryCollectionsFromDisk = fromDisk.ToDictionary(k => k.Title, v => v);

      List<CollectionFlickr> notSynchronizedList = new List<CollectionFlickr>();

      foreach (CollectionFlickr collectionFromDisk in dictionaryCollectionsFromDisk.Values)
      {
        if (dictionaryCollectionsFromFlickr.ContainsKey(collectionFromDisk.Title) == false)
        {
          notSynchronizedList.Add(collectionFromDisk);
          continue;
        }

        Dictionary<string, AlbumFlickr> flickrAlbums = dictionaryCollectionsFromFlickr[collectionFromDisk.Title].AlbumsFlickr.ToDictionary(k => k.Title, v => v);

        foreach (AlbumFlickr localAlbum in collectionFromDisk.AlbumsFlickr)
        {
          // we check if album is already on server
          if (flickrAlbums.ContainsKey(localAlbum.Title) == false)
          {
            // if collection of not synchronized has no then add it
            if (notSynchronizedList.All(a => a.Title != collectionFromDisk.Title))
            {
              notSynchronizedList.Add(new CollectionFlickr()
              {
                Id = dictionaryCollectionsFromFlickr[collectionFromDisk.Title].Id,
                Title = collectionFromDisk.Title,
                AlbumsFlickr = new BindingList<AlbumFlickr>
                {
                  localAlbum
                }
              });
            }
            else // otherwise update whole list
            {
              notSynchronizedList.Single(s => s.Title == collectionFromDisk.Title).AlbumsFlickr.Add(localAlbum);
            }
          }
          else // album exists on server so we are checking if all file are already there
          {
            Dictionary<string, bool> flickrAlbumPhotos = flickrAlbums[localAlbum.Title].PhotoList.DistinctBy(d => d.Title).ToDictionary(k => k.Title, v => true);

            foreach (PhotoFlickr localAlbumPhoto in localAlbum.PhotoList)
            {
              if (flickrAlbumPhotos.ContainsKey(localAlbumPhoto.Title) == false)
              {
                // if collection of not synchronized hasn't it then add new one
                if (notSynchronizedList.All(a => a.Title != collectionFromDisk.Title))
                {
                  notSynchronizedList.Add(new CollectionFlickr()
                  {
                    Id = dictionaryCollectionsFromFlickr[collectionFromDisk.Title].Id,
                    Title = collectionFromDisk.Title,
                    AlbumsFlickr = new BindingList<AlbumFlickr>
                    {
                      new AlbumFlickr()
                      {
                        Id = flickrAlbums[localAlbum.Title].Id,
                        PhotoList = new BindingList<PhotoFlickr>
                        {
                          localAlbumPhoto
                        },
                        Title = localAlbum.Title
                      }
                    },
                  });
                }
                else // otherwise update list with new photo
                {
                  CollectionFlickr coll = notSynchronizedList.Single(s => s.Title == collectionFromDisk.Title);
                  AlbumFlickr album = coll.AlbumsFlickr.SingleOrDefault(a => a.Title == localAlbum.Title);

                  if (album != null)
                  {
                    album.PhotoList.Add(localAlbumPhoto);
                  }
                  else
                  {
                    coll.AlbumsFlickr.Add(new AlbumFlickr
                    {
                      Id = flickrAlbums[localAlbum.Title].Id,
                      Title = localAlbum.Title,
                      PhotoList = new BindingList<PhotoFlickr>
                      {
                        localAlbumPhoto
                      }
                    });
                  }
                }
              }
            }
          }
        }
      }

      return notSynchronizedList;
    }
  }
}