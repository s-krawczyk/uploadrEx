using FlickrNet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UploadrEx.Entities;
using UploadrEx.Infrastructure;

namespace UploadrEx
{
  internal class Program
  {
    private const string FlickrSchemaFileName = "flickrSchema.txt";
    private const string NotSynchronizedElementdFileName = "notSynchronizedPhotos.txt";
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

        var flickrSchemaService = new FlickrSchemaService(authInstance, programOptions.RefreshFlickrSchema);
        var directorySchemaService = new DirectorySchemaService(programOptions.InputPath);

        IEnumerable<CollectionFlickr> flickrSchema = flickrSchemaService.GetSchema();
        SaveToFile(FlickrSchemaFileName, flickrSchema);
        IEnumerable<CollectionFlickr> directorySchema = directorySchemaService.GetSchema();

        List<CollectionFlickr> toSynchronize = new SchemaComparer().CompareSchemas(flickrSchema, directorySchema);

        SaveToFile(NotSynchronizedElementdFileName, directorySchema);

        BackgroundWorker backgroundWorker = new BackgroundWorker();
        backgroundWorker.WorkerSupportsCancellation = true;

        backgroundWorker.DoWork += (sender, eventArgs) =>
        {
          var worker = (BackgroundWorker)sender;
          var tuple = eventArgs.Argument as Tuple<Flickr, IEnumerable<CollectionFlickr>, IEnumerable<CollectionFlickr>>;

          new UploadManager().Upload(() => worker.CancellationPending, tuple.Item1, tuple.Item2, tuple.Item3);
        };

        backgroundWorker.RunWorkerCompleted += (sender, eventArgs) =>
        {
          SaveToFile(FlickrSchemaFileName, flickrSchema);
        };

        var bwData = new Tuple<Flickr, IEnumerable<CollectionFlickr>, IEnumerable<CollectionFlickr>>(
          authInstance,
          toSynchronize,
          flickrSchema);

        backgroundWorker.RunWorkerAsync(bwData);

        Console.ReadKey();

        if (backgroundWorker.IsBusy)
        {
          Log.Debug("Cancelling processing...");

          backgroundWorker.CancelAsync();

          while (backgroundWorker.IsBusy)
          {
            Thread.Sleep(100);
          }
        }
      }
      else
      {
        Console.ReadKey();
      }
    }

    private static void SaveToFile(string fileName, object data)
    {
      string dataSerialized = JsonConvert.SerializeObject(data);
      File.WriteAllText(ApplicationHelper.GetAppDataFolder() + "\\" + fileName, dataSerialized);
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
      Log.Fatal("Unhandled exception occured:", (Exception)e.ExceptionObject);
    }
  }
}