using System;
using System.Collections.Generic;
using System.Linq;

namespace RamTreeMap
{
    /// <summary>
    /// Implements a squarified treemap layout algorithm.
    /// Produces rectangles with better aspect ratios compared to slice-and-dice.
    /// </summary>
    public class TreemapLayout
    {
        /// <summary>
        /// Calculates treemap rectangles for the given data using squarified algorithm.
        /// </summary>
        public static List<TreemapRect> CalculateLayout(
            List<ProcessMemoryInfo> items,
            double width,
            double height)
        {
            var result = new List<TreemapRect>();
            
            if (items == null || items.Count == 0 || width <= 0 || height <= 0)
                return result;
            
            var totalRamInUse = items.Sum(item => item.RamUsage);

            if (totalRamInUse <= 0)
                return result;

            // Create a mapping of process IDs to memory info for quick lookup
            var memoryMap = items.ToDictionary(i => i.ProcessId);

            var normalizedItems = items
                .Where(item => item.RamUsage > 0)
                .Select(item => (item.ProcessId, item.AppName, pixelAllocationInTreeMap: (item.RamUsage / (double)totalRamInUse) * (width * height)))
                .ToList();

            if (normalizedItems.Count == 0)
                return result;

            var rectangles = new List<(int ProcessId, string AppName, double X, double Y, double Width, double Height)>();
            Squarify(normalizedItems, rectangles, 0, 0, width, height);

            foreach (var rect in rectangles)
            {
                var memInfo = memoryMap[rect.ProcessId];
                result.Add(new TreemapRect
                {
                    Label = rect.AppName,
                    X = rect.X,
                    Y = rect.Y,
                    Width = rect.Width,
                    Height = rect.Height,
                    RamUsage = memInfo.RamUsage
                });
            }

            return result;
        }

        private static void Squarify(
            List<(int ProcessId, string AppName, double pixelAllocationInTreeMap)> items,
            List<(int ProcessId, string AppName, double X, double Y, double Width, double Height)> rectangles,
            double x,
            double y,
            double width,
            double height)
        {
            if (items.Count == 0 || width <= 0 || height <= 0)
                return;

            // Sort items by value descending for better packing
            var sorted = items.OrderByDescending(i => i.pixelAllocationInTreeMap).ToList();

            SquarifyInternal(sorted, rectangles, x, y, width, height);
        }

        private static void SquarifyInternal(
            List<(int ProcessId, string AppName, double pixelAllocationInTreeMap)> items,
            List<(int ProcessId, string AppName, double X, double Y, double Width, double Height)> rectangles,
            double x,
            double y,
            double width,
            double height)
        {
            if (items.Count == 0 || width <= 0 || height <= 0)
                return;

            if (items.Count == 1)
            {
                rectangles.Add((items[0].ProcessId, items[0].AppName, x, y, width, height));
                return;
            }

            // Determine the primary axis (longer dimension)
            bool horizontal = width >= height;
            double primarySize = horizontal ? width : height;
            double secondarySize = horizontal ? height : width;

            // Find the optimal row - items that should be laid out together
            double rowSum = 0;
            int rowCount = 0;

            for (int i = 0; i < items.Count; i++)
            {
                double testSum = rowSum + items[i].pixelAllocationInTreeMap;
                double currentWorst = CalculateRowWorstRatio(items.Take(i).ToList(), rowSum, primarySize, horizontal);
                double testWorst = CalculateRowWorstRatio(items.Take(i + 1).ToList(), testSum, primarySize, horizontal);

                // If adding next item makes things worse, stop here
                if (i > 0 && testWorst > currentWorst)
                    break;

                rowSum += items[i].pixelAllocationInTreeMap;
                rowCount++;
            }

            // Layout the row
            var rowItems = items.Take(rowCount).ToList();
            var remainingItems = items.Skip(rowCount).ToList();

            LayoutRowInSpace(rowItems, rectangles, x, y, width, height, horizontal, rowSum);

            // Recursively layout remaining items in the remaining space
            if (horizontal)
            {
                double rowHeight = rowSum / primarySize;
                SquarifyInternal(remainingItems, rectangles, x, y + rowHeight, width, height - rowHeight);
            }
            else
            {
                double rowWidth = rowSum / primarySize;
                SquarifyInternal(remainingItems, rectangles, x + rowWidth, y, width - rowWidth, height);
            }
        }

        private static void LayoutRowInSpace(
            List<(int ProcessId, string AppName, double value)> rowItems,
            List<(int ProcessId, string AppName, double X, double Y, double Width, double Height)> rectangles,
            double x,
            double y,
            double width,
            double height,
            bool horizontal,
            double rowSum)
        {
            if (rowItems.Count == 0)
                return;

            if (horizontal)
            {
                // Horizontal layout: items laid left-to-right
                double rowHeight = rowSum / width;
                double currentX = x;

                foreach (var (processId, appName, value) in rowItems)
                {
                    double itemWidth = value / rowSum * width;
                    rectangles.Add((processId, appName, currentX, y, itemWidth, rowHeight));
                    currentX += itemWidth;
                }
            }
            else
            {
                // Vertical layout: items laid top-to-bottom
                double rowWidth = rowSum / height;
                double currentY = y;

                foreach (var (processId, appName, value) in rowItems)
                {
                    double itemHeight = value / rowSum * height;
                    rectangles.Add((processId, appName, x, currentY, rowWidth, itemHeight));
                    currentY += itemHeight;
                }
            }
        }

        private static double CalculateRowWorstRatio(
            List<(int ProcessId, string AppName, double value)> rowItems,
            double rowSum,
            double primarySize,
            bool horizontal)
        {
            if (rowItems.Count == 0 || rowSum <= 0 || primarySize <= 0)
                return double.MaxValue;

            double worst = 0;
            double perpendicular = rowSum / primarySize;

            foreach (var (_, _, value) in rowItems)
            {
                double parallel = value / rowSum * primarySize;
                double ratio = Math.Max(parallel / perpendicular, perpendicular / parallel);
                worst = Math.Max(worst, ratio);
            }

            return worst;
        }
    }
}
