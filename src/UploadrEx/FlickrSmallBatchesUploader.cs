using FlickrNet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using UploadrEx.Entities;
using UploadrEx.Infrastructure;

namespace UploadrEx
{
  internal class FlickrSmallBatchesUploader
  {
    private readonly ILogger _log = LoggerFactory.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    private readonly Flickr _flickrApi;
    private readonly int _uploadThreadsCount;
    private static readonly object _cacheFileSyncObject = new object();
    private BindingList<CollectionFlickr> _cachedList;

    public FlickrSmallBatchesUploader()
    {
      _uploadThreadsCount = 8;
    }

    public FlickrSmallBatchesUploader(Flickr flickrApi)

      : this()
    {
      _flickrApi = flickrApi;
    }

    public void UploadSchema(IEnumerable<CollectionFlickr> toUpload)
    {
      if (_cachedList == null)
      {
        if (File.Exists(GetCahceFilePath()) == false)
        {
          IEnumerable<CollectionFlickr> downloadSchemaFromFlickr = DownloadSchemaFromFlickr();

          _cachedList = new BindingList<CollectionFlickr>(downloadSchemaFromFlickr.ToList());
        }
        else
        {
          _cachedList = JsonConvert.DeserializeObject<BindingList<CollectionFlickr>>(File.ReadAllText(GetCahceFilePath()));
        }

        BindEvents();
      }

      foreach (CollectionFlickr localCollection in toUpload)
      {
        if (_cachedList.Any(a => a.Title == localCollection.Title) == false)
        {
          Collection addedCollection = _flickrApi.CollectionsCreate(localCollection.Title, string.Empty);

          _cachedList.Add(
            new CollectionFlickr(SaveCachedData)
            {
              Id = addedCollection.CollectionId,
              Title = localCollection.Title
            }
            );

          _log.InfoFormat("Created not existing collection [{0}].", addedCollection.Title);
        }

        var cachedCollection = _cachedList.Single(s => s.Title == localCollection.Title);

        foreach (AlbumFlickr localAlbum in localCollection.AlbumsFlickr)
        {
          if (cachedCollection.AlbumsFlickr.Any(a => a.Title == localAlbum.Title) == false)
          {
            if (localAlbum.PhotoList.Count == 0)
            {
              continue;
            }

            // first sent photo because it is not possible to create album without it
            PhotoFlickr photoFlickr = localAlbum.PhotoList.First();
            photoFlickr.Id = _flickrApi.UploadPicture(photoFlickr.FilePath, photoFlickr.Title, string.Empty,
              string.Format("\"#Collection={0}\" \"#Album={1}\"", localCollection.Title, localAlbum.Title),
              false, false, false);

            Photoset addedPhotoset = _flickrApi.PhotosetsCreate(localAlbum.Title, photoFlickr.Id);

            cachedCollection.AlbumsFlickr.Add(
              new AlbumFlickr(SaveCachedData)
              {
                Id = addedPhotoset.PhotosetId,
                Title = localAlbum.Title
              });

            _flickrApi.CollectionsEditSets(cachedCollection.Id,
              cachedCollection.AlbumsFlickr.Select(s => s.Id).ToList());

            _log.InfoFormat("Added album [{0}] to collection [{1}].", localAlbum.Title, localCollection.Title);
          }

          var flickrPhotoset = cachedCollection.AlbumsFlickr.Single(s => s.Title == localAlbum.Title);

          AlbumFlickr cachedAlbum = _cachedList
            .Single(s => s.Title == localCollection.Title)
            .AlbumsFlickr.Single(s => s.Title == localAlbum.Title);

          List<PhotoFlickr> photosOnFlickr = cachedAlbum.PhotoList.ToList();
          _log.DebugFormat("Got photos from album: [{0}]", localAlbum.Title);

          List<PhotoFlickr> photosToUpload = localAlbum.PhotoList.Except(photosOnFlickr).ToList();

          _log.DebugFormat("Photos to upload count: [{0}]", photosToUpload.Count());

          UploadInternal(flickrPhotoset.Id, localCollection.Title, localAlbum.Title, photosToUpload, uploadedPhoto => cachedAlbum.PhotoList.Add(uploadedPhoto));
        }
      }
    }

    private void BindEvents()
    {
      _cachedList.AddingNew += (sender, args) => SaveCachedData();
      _cachedList.ListChanged += (sender, args) => SaveCachedData();

      foreach (var collectionFlickr in _cachedList)
      {
        collectionFlickr.AlbumsFlickr.AddingNew += (sender, args) => SaveCachedData();
        collectionFlickr.AlbumsFlickr.ListChanged += (sender, args) => SaveCachedData();

        foreach (var flickr in collectionFlickr.AlbumsFlickr)
        {
          flickr.PhotoList.AddingNew += (sender, args) => SaveCachedData();
          flickr.PhotoList.ListChanged += (sender, args) => SaveCachedData();
        }
      }
    }

    private IEnumerable<CollectionFlickr> DownloadSchemaFromFlickr()
    {
      return new FlickrSchemaService(_flickrApi, true).GetSchema();
    }

    private void UploadInternal(
      string photoSetId,
      string collectionTitle,
      string albumTitle,
      IEnumerable<PhotoFlickr> photosToUpload,
      Action<PhotoFlickr> photoUploadedEvent)
    {
      string tag = string.Format("\"#Collection={0}\" \"#Album={1}\"", collectionTitle, albumTitle);

      Parallel.ForEach(photosToUpload, new ParallelOptions
      {
        MaxDegreeOfParallelism = _uploadThreadsCount
      },
        (photoFlickr, state, arg3) =>
        {
          if (state.IsStopped)
          {
            return;
          }

          int retryCount = 0;

          while (retryCount < 3)
          {
            try
            {
              photoFlickr.Id = _flickrApi.UploadPicture(
                photoFlickr.FilePath,
                photoFlickr.Title,
                string.Empty,
                tag,
                false,
                false,
                false);

              _flickrApi.PhotosetsAddPhoto(photoSetId, photoFlickr.Id);

              photoUploadedEvent(photoFlickr);

              _log.DebugFormat("Uploaded photo [{0}] to album [{1}].", photoFlickr.Title, albumTitle);

              break;
            }
            catch (Exception ex)
            {
              retryCount++;
              _log.Warn("Uploading failed. Retrying...", ex);
            }
          }
        }
        );
    }

    private List<Photo> GetPhotosFromFlickrAlbum(string photoSetId)
    {
      List<Photo> photosOnFlickr = new List<Photo>();
      PhotosetPhotoCollection local;

      int numberOfPhotos = _flickrApi.PhotosetsGetInfo(photoSetId).NumberOfPhotos;
      int page = 1;

      while (true)
      {
        int count = 0;

        if (numberOfPhotos >= 100)
        {
          count = 100;
          numberOfPhotos -= 100;
        }
        else
        {
          count = numberOfPhotos;
          numberOfPhotos = 0;
        }

        local = _flickrApi.PhotosetsGetPhotos(photoSetId, page, count);

        foreach (Photo getPhoto in local)
        {
          photosOnFlickr.Add(getPhoto);
        }

        if (numberOfPhotos == 0)
        {
          break;
        }

        page++;
      }

      return photosOnFlickr;
    }

    private void SaveCachedData()
    {
      lock (_cacheFileSyncObject)
      {
        string dataSerialized = JsonConvert.SerializeObject(_cachedList);
        File.WriteAllText(GetCahceFilePath(), dataSerialized);
      }
    }

    private static string GetCahceFilePath()
    {
      return ApplicationHelper.GetAppDataFolder() + "\\" + "LocalFlickrCache.txt";
    }
  }
}