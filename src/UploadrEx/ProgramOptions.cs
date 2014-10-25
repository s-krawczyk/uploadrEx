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
  }
}