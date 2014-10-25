using FlickrNet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UploadrEx.Entities;
using UploadrEx.Infrastructure;

namespace UploadrEx
{
  internal class Program
  {
    private static ILogger Log;

    private static void Main(string[] args)
    {
      LoggerFactory.SetLogger(Log4NetLogger.GetLogger);
      Log = LoggerFactory.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

      AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

      var programOptions = new ProgramOptions();
      if (CommandLine.Parser.Default.ParseArguments(args, programOptions))
      {
        if (FlickrManager.OAuthToken == null)
        {
          Flickr f = FlickrManager.GetInstance();

          OAuthRequestToken requestToken = f.OAuthGetRequestToken("oob");

          string url = f.OAuthCalculateAuthorizationUrl(requestToken.Token, AuthLevel.Write);

          Log.Debug("Please enter authorization code:");

          System.Diagnostics.Process.Start(url);

          string readLine = Console.ReadLine();

          f = FlickrManager.GetInstance();
          try
          {
            var accessToken = f.OAuthGetAccessToken(requestToken, readLine);
            FlickrManager.OAuthToken = accessToken;
          }
          catch (FlickrApiException ex)
          {
            Log.Error("Error occured.", ex);
            Environment.Exit(-1);
          }
        }

        Flickr authInstance = FlickrManager.GetAuthInstance();
        Auth oAuthCheckToken = authInstance.AuthOAuthCheckToken();

        var flickrSchemaService = new FlickrSchemaService(authInstance, programOptions.RefreshFlickrSchema);
        var flickrSchema = flickrSchemaService.GetSchema().DistinctBy(k => k.Title).ToDictionary(k => k.Title, v => v);
        var x = new DirectorySchemaService(programOptions.InputPath);
        IEnumerable<CollectionFlickr> collectionFlickrs = x.GetSchema();
        Dictionary<string, CollectionFlickr> directorySchema = collectionFlickrs.DistinctBy(k => k.Title).ToDictionary(k => k.Title, v => v);

        string serializeObject = JsonConvert.SerializeObject(directorySchema);
        File.WriteAllText(ApplicationHelper.GetAppDataFolder() + "\\scannedInputDirectory.txt", serializeObject);

        Dictionary<string, CollectionFlickr> flickSchemaDir = flickrSchema;

        List<CollectionFlickr> notSynchronized = CompareSchemas(flickSchemaDir, directorySchema);

        string notSynchronizedSerialized = JsonConvert.SerializeObject(notSynchronized);
        File.WriteAllText(ApplicationHelper.GetAppDataFolder() + "\\notSynchronizedPhotos.txt",
          notSynchronizedSerialized);

        Upload(authInstance, ref notSynchronized, ref flickSchemaDir);
      }

      Console.ReadKey();
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
      Log.Fatal("Unhandled exception occured:", (Exception)e.ExceptionObject);
    }

    private static void Upload(Flickr flickrInstance, ref List<CollectionFlickr> toUpload, ref Dictionary<string, CollectionFlickr> flickrSchema)
    {
      foreach (var collectionToUpload in toUpload)
      {
        if (string.IsNullOrEmpty(collectionToUpload.Id))
        {
          Log.DebugFormat("Creating new collection: [{0}]", collectionToUpload.Title);

          Collection createdCollection = flickrInstance.CollectionsCreate(collectionToUpload.Title, string.Empty);

          collectionToUpload.Id = createdCollection.CollectionId;
          flickrSchema.Add(collectionToUpload.Title, new CollectionFlickr
          {
            Id = createdCollection.CollectionId,
            Title = collectionToUpload.Title,
            AlbumsFlickr = new List<AlbumFlickr>()
          });
        }

        CollectionFlickr collectionOnFlickr = flickrSchema[collectionToUpload.Title];

        foreach (var notSyncedAlbum in collectionToUpload.AlbumsFlickr)
        {
          if (string.IsNullOrEmpty(notSyncedAlbum.Id))
          {
            Log.DebugFormat("Creating new album: [{0}]", notSyncedAlbum.Title);

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
                  Log.DebugFormat("Uploading photo [{0}]", photoFlickr.Title);

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
                  Log.Warn("Uploading failed. Retrying...", ex);
                }
              }

              return arg3;
            },
            set =>
            {
              albumFlickr.PhotoList.AddRange(set);
            }
            );
        }
      }
    }

    private static List<CollectionFlickr> CompareSchemas(Dictionary<string, CollectionFlickr> collectionsFromFlickr, Dictionary<string, CollectionFlickr> fromDisk)
    {
      List<CollectionFlickr> notSynchronizedList = new List<CollectionFlickr>();

      foreach (CollectionFlickr collectionFromDisk in fromDisk.Values)
      {
        if (collectionsFromFlickr.ContainsKey(collectionFromDisk.Title) == false)
        {
          notSynchronizedList.Add(collectionFromDisk);
          continue;
        }

        var flickrAlbums = collectionsFromFlickr[collectionFromDisk.Title].AlbumsFlickr.ToDictionary(k => k.Title, v => v);

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
                Id = collectionsFromFlickr[collectionFromDisk.Title].Id,
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
                    Id = collectionsFromFlickr[collectionFromDisk.Title].Id,
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