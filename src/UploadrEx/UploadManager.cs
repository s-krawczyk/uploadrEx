using FlickrNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UploadrEx.Entities;
using UploadrEx.Infrastructure;

namespace UploadrEx
{
  internal class UploadManager
  {
    private readonly ILogger _log = LoggerFactory.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    public void Upload(Func<bool> cancellationPending, Flickr flickrInstance, IEnumerable<CollectionFlickr> toUpload, IEnumerable<CollectionFlickr> flickrSchema)
    {
      Dictionary<string, CollectionFlickr> dictionaryFlickrSchema = flickrSchema.ToDictionary(k => k.Title, v => v);

      foreach (var collectionToUpload in toUpload)
      {
        if (string.IsNullOrEmpty(collectionToUpload.Id))
        {
          _log.DebugFormat("Creating new collection: [{0}]", collectionToUpload.Title);

          Collection createdCollection = flickrInstance.CollectionsCreate(collectionToUpload.Title, string.Empty);

          collectionToUpload.Id = createdCollection.CollectionId;
          dictionaryFlickrSchema.Add(
            collectionToUpload.Title,
            new CollectionFlickr
            {
              Id = createdCollection.CollectionId,
              Title = collectionToUpload.Title,
              AlbumsFlickr = new List<AlbumFlickr>()
            });
        }

        CollectionFlickr collectionOnFlickr = dictionaryFlickrSchema[collectionToUpload.Title];

        foreach (var notSyncedAlbum in collectionToUpload.AlbumsFlickr)
        {
          if (string.IsNullOrEmpty(notSyncedAlbum.Id))
          {
            _log.DebugFormat("Creating new album: [{0}]", notSyncedAlbum.Title);

            // najpierw trzeba przeslac zdjecie inaczej nie mozna utworzyc kolekcji
            PhotoFlickr photoFlickr = notSyncedAlbum.PhotoList.First();
            photoFlickr.Id = flickrInstance.UploadPicture(photoFlickr.FilePath, photoFlickr.Title, string.Empty,
              string.Format("\"#Collection={0}\" \"#Album={1}\"", collectionToUpload.Title, notSyncedAlbum.Title), false, false, false);

            var albumId = flickrInstance.PhotosetsCreate(notSyncedAlbum.Title, photoFlickr.Id).PhotosetId;
            notSyncedAlbum.Id = albumId;
            collectionOnFlickr.AlbumsFlickr.Add(new AlbumFlickr
            {
              Id = albumId,
              Title = notSyncedAlbum.Title,
              PhotoList = new List<PhotoFlickr>
              {
                photoFlickr
              }
            });

            flickrInstance.CollectionsEditSets(collectionOnFlickr.Id, collectionOnFlickr.AlbumsFlickr.Select(s => s.Id).ToList());

            // po dodaniu mozna uznac jako zsynchronizowane wiec usuwamy z listy do synchronizacji
            notSyncedAlbum.PhotoList.Remove(photoFlickr);
          }

          AlbumFlickr albumFlickr = collectionOnFlickr.AlbumsFlickr.Single(s => s.Id == notSyncedAlbum.Id);

          Parallel.ForEach(notSyncedAlbum.PhotoList, new ParallelOptions
          {
            MaxDegreeOfParallelism = 4
          },
            () => new List<PhotoFlickr>(),
            (photoFlickr, state, arg3) =>
            {
              int retryCount = 0;

              while (retryCount < 3)
              {
                try
                {
                  if (cancellationPending())
                  {
                    state.Stop();
                  }

                  _log.DebugFormat("Uploading photo [{0}]", photoFlickr.Title);

                  photoFlickr.Id = flickrInstance.UploadPicture(photoFlickr.FilePath, photoFlickr.Title, string.Empty,
                    string.Format("\"#Collection={0}\" \"#Album={1}\"", collectionToUpload.Title, notSyncedAlbum.Title),
                    false,
                    false, false);

                  flickrInstance.PhotosetsAddPhoto(notSyncedAlbum.Id, photoFlickr.Id);
                  arg3.Add(photoFlickr);

                  break;
                }
                catch (Exception ex)
                {
                  retryCount++;
                  _log.Warn("Uploading failed. Retrying...", ex);
                }
              }

              return arg3;
            },
            set =>
            {
              albumFlickr.PhotoList.AddRange(set);
            }
            );

          if (cancellationPending())
          {
            return;
          }
        }
      }
    }
  }
}