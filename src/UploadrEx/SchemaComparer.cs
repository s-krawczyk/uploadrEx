using System.Collections.Generic;
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

        var flickrAlbums = dictionaryCollectionsFromFlickr[collectionFromDisk.Title].AlbumsFlickr.ToDictionary(k => k.Title, v => v);

        foreach (AlbumFlickr localAlbum in collectionFromDisk.AlbumsFlickr)
        {
          // sprawdzam czy jest album juz na serwerze
          if (flickrAlbums.ContainsKey(localAlbum.Title) == false)
          {
            // jezeli kolekcja nie zsynchronizowanych nie posiada jeszcze tej brakujacej kolekcji dodaj nowa
            if (notSynchronizedList.All(a => a.Title != collectionFromDisk.Title))
            {
              notSynchronizedList.Add(new CollectionFlickr()
              {
                Id = dictionaryCollectionsFromFlickr[collectionFromDisk.Title].Id,
                Title = collectionFromDisk.Title,
                AlbumsFlickr = new List<AlbumFlickr>
                {
                  localAlbum
                }
              });
            }
            else // inaczej zaaktualizuj liste
            {
              notSynchronizedList.Single(s => s.Title == collectionFromDisk.Title).AlbumsFlickr.Add(localAlbum);
            }
          }
          else // album istnieje na serwerze sprawdzam czy sa zaladowane wszystkie pliki
          {
            var flickrAlbumPhotos = flickrAlbums[localAlbum.Title].PhotoList.DistinctBy(d => d.Title).ToDictionary(k => k.Title, v => true);

            foreach (var localAlbumPhoto in localAlbum.PhotoList)
            {
              if (flickrAlbumPhotos.ContainsKey(localAlbumPhoto.Title) == false)
              {
                // jezeli kolekcja nie zsynchronizowanych nie posiada jeszcze tej brakujacej kolekcji dodaj nowa
                if (notSynchronizedList.All(a => a.Title != collectionFromDisk.Title))
                {
                  notSynchronizedList.Add(new CollectionFlickr()
                  {
                    Id = dictionaryCollectionsFromFlickr[collectionFromDisk.Title].Id,
                    Title = collectionFromDisk.Title,
                    AlbumsFlickr = new List<AlbumFlickr>
                    {
                      new AlbumFlickr()
                      {
                        Id = flickrAlbums[localAlbum.Title].Id,
                        PhotoList = new List<PhotoFlickr>
                        {
                          localAlbumPhoto
                        },
                        Title = localAlbum.Title
                      }
                    },
                  });
                }
                else // inaczej zaaktualizuj liste o nowe zdjecie
                {
                  var coll = notSynchronizedList.Single(s => s.Title == collectionFromDisk.Title);
                  var album = coll.AlbumsFlickr.SingleOrDefault(a => a.Title == localAlbum.Title);

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
                      PhotoList = new List<PhotoFlickr>
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