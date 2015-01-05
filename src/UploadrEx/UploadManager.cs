using FlickrNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using UploadrEx.Entities;
using UploadrEx.Infrastructure;

namespace UploadrEx
{
  internal class UploadManager
  {
    private readonly int _uploadThreadsCount;
    private readonly ILogger _log = LoggerFactory.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    public UploadManager(int uploadThreadsCount)
    {
      _uploadThreadsCount = uploadThreadsCount;
    }

    public void Upload(Func<bool> cancellationPending, Flickr flickrInstance, IEnumerable<CollectionFlickr> toUpload, IEnumerable<CollectionFlickr> flickrSchema)
    {
      Dictionary<string, CollectionFlickr> dictionaryFlickrSchema = flickrSchema.ToDictionary(k => k.Title, v => v);

      foreach (CollectionFlickr collectionToUpload in toUpload)
      {
        if (string.IsNullOrEmpty(collectionToUpload.Id))
        {
          if (collectionToUpload.AlbumsFlickr.Count > 0)
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
                AlbumsFlickr = new BindingList<AlbumFlickr>()
              });
          }
          else
          {
            _log.WarnFormat("Collection [{0}] is empty. Please verify what's going on...", collectionToUpload.Title);
            continue;
          }
        }

        CollectionFlickr collectionOnFlickr = dictionaryFlickrSchema[collectionToUpload.Title];

        foreach (AlbumFlickr notSyncedAlbum in collectionToUpload.AlbumsFlickr)
        {
          if (string.IsNullOrEmpty(notSyncedAlbum.Id))
          {
            if (notSyncedAlbum.PhotoList.Count > 0)
            {
              _log.DebugFormat("Creating new album: [{0}]", notSyncedAlbum.Title);

              // first we send a picture because without that is it not possible to create an album
              PhotoFlickr photoFlickr = notSyncedAlbum.PhotoList.First();
              photoFlickr.Id = flickrInstance.UploadPicture(photoFlickr.FilePath, photoFlickr.Title, string.Empty,
                string.Format("\"#Collection={0}\" \"#Album={1}\"", collectionToUpload.Title, notSyncedAlbum.Title),
                false, false, false);

              string albumId = flickrInstance.PhotosetsCreate(notSyncedAlbum.Title, photoFlickr.Id).PhotosetId;
              notSyncedAlbum.Id = albumId;
              collectionOnFlickr.AlbumsFlickr.Add(new AlbumFlickr
              {
                Id = albumId,
                Title = notSyncedAlbum.Title,
                PhotoList = new BindingList<PhotoFlickr>
                {
                  photoFlickr
                }
              });

              flickrInstance.CollectionsEditSets(collectionOnFlickr.Id,
                collectionOnFlickr.AlbumsFlickr.Select(s => s.Id).ToList());

              // after add we can say that is syncronized so we delete it from synchronization list
              notSyncedAlbum.PhotoList.Remove(photoFlickr);
            }
            else
            {
              _log.WarnFormat("Album [{0}] is empty. Please verify what's going on...", notSyncedAlbum.Title);
              continue;
            }
          }

          AlbumFlickr albumFlickr = collectionOnFlickr.AlbumsFlickr.Single(s => s.Id == notSyncedAlbum.Id);

          Parallel.ForEach(notSyncedAlbum.PhotoList, new ParallelOptions
          {
            MaxDegreeOfParallelism = _uploadThreadsCount
          },
            () => new List<PhotoFlickr>(),
            (photoFlickr, state, arg3) =>
            {
              if (state.IsStopped)
              {
                return arg3;
              }

              int retryCount = 0;

              while (retryCount < 3)
              {
                try
                {
                  if (cancellationPending())
                  {
                    state.Stop();
                    return arg3;
                  }

                  photoFlickr.Id = flickrInstance.UploadPicture(photoFlickr.FilePath, photoFlickr.Title, string.Empty,
                    string.Format("\"#Collection={0}\" \"#Album={1}\"", collectionToUpload.Title, notSyncedAlbum.Title),
                    false,
                    false, false);

                  flickrInstance.PhotosetsAddPhoto(notSyncedAlbum.Id, photoFlickr.Id);
                  arg3.Add(photoFlickr);

                  _log.DebugFormat("Uploaded photo [{0}] to album [{1}].", photoFlickr.Title, albumFlickr.Title);

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
              if (set != null)
              {
                set.ForEach(albumFlickr.PhotoList.Add);
              }
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