using System.Collections.Generic;
using UploadrEx.Entities;

namespace UploadrEx
{
  internal interface ISchemaService
  {
    IEnumerable<CollectionFlickr> GetSchema();
  }
}