using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UploadrEx.Infrastructure
{
  internal class ApplicationHelper
  {
    public static string GetAppDataFolder()
    {
      return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }
  }
}