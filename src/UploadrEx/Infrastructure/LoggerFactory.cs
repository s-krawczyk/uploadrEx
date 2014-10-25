using System;

namespace UploadrEx.Infrastructure
{
  internal class LoggerFactory
  {
    private static Func<Type, ILogger> _loggerFactory;

    public static void SetLogger(Func<Type, ILogger> loggerFactory)
    {
      _loggerFactory = loggerFactory;
    }

    public static ILogger GetLogger(Type type)
    {
      return _loggerFactory(type);
    }
  }
}