namespace RamTreeMap
{
    /// <summary>
    /// Represents a treemap rectangle with layout information.
    /// </summary>
    public class TreemapRect
    {
        public required string Label { get; set; }
        public long RamUsage { get; set; }

        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public double Value => Width * Height;
    }
}
