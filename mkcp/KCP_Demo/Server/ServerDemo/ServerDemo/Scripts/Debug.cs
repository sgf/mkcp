using System;

public class Debug
{
    public Debug()
    {

    }
    public static void Log(object msg)
    {
        Console.WriteLine(msg);
    }
    public static void Log(string msg)
    {
        Console.WriteLine(msg);
    }
    public static void Log(string format, params object[] pars)
    {
        Console.WriteLine(string.Format(format, pars));
    }
}