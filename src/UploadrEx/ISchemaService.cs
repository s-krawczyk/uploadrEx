using System.Collections.Generic;

namespace UploadrEx
{
  internal interface ISchemaService
  {
    IEnumerable<CollectionFlickr> GetSchema();
  }
}