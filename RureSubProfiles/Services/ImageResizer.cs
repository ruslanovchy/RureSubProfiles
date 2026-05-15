using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace RureSubProfiles.Services;

public class ImageResizer : IImageResizer
{
    public void ResizeImage(Image image, int width, int height)
    {
        image.Mutate(x =>
        {
            x.Resize(width, height);
        });
    }
}
