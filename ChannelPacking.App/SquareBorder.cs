using System.Windows;
using System.Windows.Controls;

namespace ChannelPacking.App;

public sealed class SquareBorder : Border
{
    protected override Size MeasureOverride(Size constraint)
    {
        var size = ResolveSquareSize(constraint);
        Child?.Measure(new Size(size, size));
        return new Size(size, size);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var size = ResolveSquareSize(arrangeSize);
        Child?.Arrange(new Rect(0, 0, size, size));
        return new Size(size, size);
    }

    private double ResolveSquareSize(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) || availableSize.Width <= 0
            ? Math.Max(MinWidth, MinHeight)
            : availableSize.Width;

        if (!double.IsInfinity(MaxWidth))
        {
            width = Math.Min(width, MaxWidth);
        }

        if (!double.IsInfinity(availableSize.Height) && availableSize.Height > 0)
        {
            width = Math.Min(width, availableSize.Height);
        }

        width = Math.Max(width, Math.Max(MinWidth, MinHeight));
        return width <= 0 ? 140 : width;
    }
}
