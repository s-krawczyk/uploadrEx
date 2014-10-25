using FlickrNet;
using System;
using System.Collections.Generic;

namespace UploadrEx
{
  internal class FlickrSchemaService : ISchemaService
  {
    private readonly Flickr _flickr;

    public FlickrSchemaService(Flickr flickr)
    {
      _flickr = flickr;
    }

    public IEnumerable<CollectionFlickr> GetSchema()
    {
      throw new NotImplementedException();
    }
  }
}