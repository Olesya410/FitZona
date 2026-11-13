namespace FitZone.Models
{
    public static class ServiceStyle
    {
        private static readonly string[] Colors = { "dark", "mint", "pink", "milk", "dark", "mint" };
        private static readonly string[] SpansFull = { "7", "5", "6", "6", "5", "7" };
        private static readonly string[] SpansShort = { "5", "3", "4", "4", "3", "5" };

        private static readonly string[] Icons =
        {
            "<path d=\"M8 92A84 84 0 0 1 92 8M8 70A62 62 0 0 1 70 8M8 48A40 40 0 0 1 48 8\"/>",
            "<circle cx=\"50\" cy=\"50\" r=\"40\"/><path d=\"M10 50h80\"/><circle cx=\"50\" cy=\"30\" r=\"9\" fill=\"currentColor\" stroke=\"none\"/>",
            "<path d=\"M6 72 26 28 46 72 66 28 94 72\"/><path d=\"M6 90 26 56 46 90 66 56 94 90\"/>",
            "<path d=\"M4 30c16-22 26 22 46 0s30 22 46 0M4 56c16-22 26 22 46 0s30 22 46 0M4 82c16-22 26 22 46 0s30 22 46 0\"/>",
            "<path d=\"M8 92 92 8M30 92 92 30M52 92 92 52M8 70 70 8\"/>",
            "<path d=\"M50 4 61 39 96 50 61 61 50 96 39 61 4 50 39 39Z\" fill=\"currentColor\" stroke=\"none\"/>"
        };

        public static string Color(int i) { return Colors[i % Colors.Length]; }
        public static string SpanFull(int i) { return SpansFull[i % SpansFull.Length]; }
        public static string SpanShort(int i) { return SpansShort[i % SpansShort.Length]; }

        public static string Icon(int i)
        {
            return "<svg class=\"motif\" viewBox=\"0 0 100 100\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"3\" stroke-linecap=\"round\" aria-hidden=\"true\">"
                   + Icons[i % Icons.Length] + "</svg>";
        }
    }
}
