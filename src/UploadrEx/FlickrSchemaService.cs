using FlickrNet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using UploadrEx.Entities;
using UploadrEx.Infrastructure;

namespace UploadrEx
{
  internal class FlickrSchemaService : ISchemaService
  {
    private readonly ILogger _log = LoggerFactory.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    public const string DefaultFlickrSchemaCacheFilePath = "UploadrEx_FlickrSchemaCache.cache";

    private readonly Flickr _flickr;
    private bool _reloadFromFlickr;

    static FlickrSchemaService()
    {
      FlickrSchemaCacheFilePath = () => ApplicationHelper.GetAppDataFolder() + "\\" + DefaultFlickrSchemaCacheFilePath;
    }

    public static Func<string> FlickrSchemaCacheFilePath { get; set; }

    public FlickrSchemaService(Flickr flickr, bool reloadFromFlickr)
    {
      _flickr = flickr;
      _reloadFromFlickr = reloadFromFlickr;
    }

    public IEnumerable<CollectionFlickr> GetSchema()
    {
      string cFlickrschemaTxt = FlickrSchemaCacheFilePath();

      if (File.Exists(cFlickrschemaTxt) && _reloadFromFlickr == false)
      {
        string readAllText = File.ReadAllText(cFlickrschemaTxt);
        List<CollectionFlickr> deserializeObject = JsonConvert.DeserializeObject<List<CollectionFlickr>>(readAllText);

        return deserializeObject;
      }

      CollectionCollection collectionsGetTree = _flickr.CollectionsGetTree();

      List<CollectionFlickr> collections = new List<CollectionFlickr>(collectionsGetTree.Count);

      foreach (Collection collectionFlickr in collectionsGetTree)
      {
        CollectionFlickr flickr = new CollectionFlickr
        {
          Id = collectionFlickr.CollectionId,
          Title = collectionFlickr.Title,
          AlbumsFlickr = new BindingList<AlbumFlickr>()
        };

        foreach (CollectionSet collectionSet in collectionFlickr.Sets)
        {
          AlbumFlickr albumFlickr = new AlbumFlickr
          {
            Id = collectionSet.SetId,
            Title = collectionSet.Title
          };
          flickr.AlbumsFlickr.Add(albumFlickr);

          _log.DebugFormat("Getting photos from album: [{0}]", albumFlickr.Title);
          int page = 1;
          PhotosetPhotoCollection photosetPhotoCollection = new PhotosetPhotoCollection();
          int numberOfPhotos = _flickr.PhotosetsGetInfo(albumFlickr.Id).NumberOfPhotos;

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

            PhotosetPhotoCollection getPhotosetPhotoCollection = _flickr.PhotosetsGetPhotos(albumFlickr.Id, page, count);

            foreach (Photo getPhoto in getPhotosetPhotoCollection)
            {
              photosetPhotoCollection.Add(getPhoto);
            }

            if (numberOfPhotos == 0)
            {
              break;
            }

            page++;
          }

          albumFlickr.PhotoList = new BindingList<PhotoFlickr>();

          foreach (Photo photoFromSet in photosetPhotoCollection)
          {
            albumFlickr.PhotoList.Add(new PhotoFlickr
            {
              Id = photoFromSet.PhotoId,
              Title = photoFromSet.Title,
              Tags = photoFromSet.Tags.ToList()
            });
          }
        }

        collections.Add(flickr);
      }

      string serializeObject = JsonConvert.SerializeObject(collections);

      File.WriteAllText(cFlickrschemaTxt, serializeObject);

      return collections;
    }
  }
}