using FlickrNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UploadrEx.Properties;

namespace UploadrEx
{
  public class FlickrManager
  {
    public const string ApiKey = "cd954c37ae19b5a4499a6e6d59003d82";
    public const string SharedSecret = "94898dd04ccd7284";

    static FlickrManager()
    {
      Cache.CacheDisabled = true;
    }

    public static Flickr GetInstance()
    {
      return new Flickr(ApiKey, SharedSecret);
    }

    public static Flickr GetAuthInstance()
    {
      Flickr f = new Flickr(ApiKey, SharedSecret);
      f.OAuthAccessToken = OAuthToken.Token;
      f.OAuthAccessTokenSecret = OAuthToken.TokenSecret;
      return f;
    }

    public static OAuthAccessToken OAuthToken
    {
      get
      {
        return Settings.Default.OAuthToken;
      }
      set
      {
        Settings.Default.OAuthToken = value;
        Settings.Default.Save();
      }
    }
  }
}