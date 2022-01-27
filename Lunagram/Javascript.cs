using Jint;

namespace Javascript
{
  public static class JavascriptRunner
  {
    public static string Run(string source)
    {
      return new Engine().Evaluate(source).ToString();
    }
  }
}