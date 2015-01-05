using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UploadrEx
{
  internal class ProgramOptions
  {
    [Option('r', "refreshSchema", DefaultValue = false, HelpText = "Refresh schema from Flickr server.")]
    public bool RefreshFlickrSchema { get; set; }

    [Option('i', "input", HelpText = "Input directory to upload.")]
    public string InputPath { get; set; }

    [Option('t', "threads", DefaultValue = 4, HelpText = "Upload parallel threads count.")]
    public int UploadThreadsCount { get; set; }

    [Option('d', "removeDuplicates", DefaultValue = false, HelpText = "If set removes duplicates detected on Flickr in same album on end of synchronization.")]
    public bool RemoveDuplicates { get; set; }
  }
}