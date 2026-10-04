internal static class Program
{
    private static int Main(string[] args)
    {
        return Tests.Run(args.Length > 0 ? args[0] : "");
    }
}
