using SixLabors.ImageSharp;

namespace RureSubProfiles.Services;

public interface IImageResizer
{
    void ResizeImage(Image image, int width, int height);
}
