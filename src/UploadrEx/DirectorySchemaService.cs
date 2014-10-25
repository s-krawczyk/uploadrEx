﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UploadrEx
{
  internal class DirectorySchemaService : ISchemaService
  {
    private readonly string _directoryPath;

    public DirectorySchemaService(string directoryPath)
    {
      _directoryPath = directoryPath;
    }

    public IEnumerable<CollectionFlickr> GetSchema()
    {
      List<string> listDirsLoop = ListDirsLoop(_directoryPath);
      Dictionary<string, CollectionFlickr> collectionFlickrs = ParseDirectories(listDirsLoop);

      return collectionFlickrs.Values;
    }

    private static Dictionary<string, CollectionFlickr> ParseDirectories(List<string> directoriesList)
    {
      // format {year}/{any_name} translated to {collection}/{album}

      List<string> selectedDirs = directoriesList;

      Dictionary<string, CollectionFlickr> collections = new Dictionary<string, CollectionFlickr>(selectedDirs.Count);

      foreach (string selectedDir in selectedDirs)
      {
        DirectoryInfo directoryInfo = new DirectoryInfo(selectedDir);
        string name = directoryInfo.Name;
        string year = directoryInfo.Parent == null ? "" : directoryInfo.Parent.Name;

        int parserYear = 0;

        if (Int32.TryParse(year, out parserYear))
        {
          if (collections.ContainsKey(year) == false)
          {
            collections.Add(year, new CollectionFlickr());
            collections[year].Title = year;
            collections[year].AlbumsFlickr = new List<AlbumFlickr>();
          }

          AlbumFlickr albumFlickr = new AlbumFlickr
          {
            Title = name,
            PhotoList = new List<PhotoFlickr>()
          };
          collections[year].AlbumsFlickr.Add(albumFlickr);

          FileInfo[] fileInfos = directoryInfo.GetFiles("*.jpg");
          if (fileInfos.Length > 0)
          {
            albumFlickr.PhotoList.AddRange(fileInfos.Select(s => new PhotoFlickr
            {
              Title = s.Name,
              FilePath = s.FullName
            }));
          };
          fileInfos = directoryInfo.GetFiles("*.jpeg");
          if (fileInfos.Length > 0)
          {
            albumFlickr.PhotoList.AddRange(fileInfos.Select(s => new PhotoFlickr
            {
              Title = s.Name,
              FilePath = s.FullName
            }));
          };
        }
      }

      return collections;
    }

    private static List<string> ListDirsLoop(string directory)
    {
      List<string> directories = Directory.GetDirectories(directory).ToList();
      List<string> localDirs = directories.ToList();

      foreach (string dir in localDirs)
      {
        directories.AddRange(ListDirsLoop(dir));
      }

      return directories;
    }
  }
}