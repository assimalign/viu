using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Adornments;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Registers frozen WPF swatches with Visual Studio's image service and retains their handles.
/// </summary>
internal sealed class ViuCompletionColorImageCatalog
{
    private const int ImageSize = 16;

    private readonly object gate = new();
    private readonly IVsManagedImageService? imageService;
    private readonly Dictionary<string, RegisteredImage> images = new(StringComparer.Ordinal);

    /// <summary>Initializes a catalog over the host image service.</summary>
    internal ViuCompletionColorImageCatalog(IVsManagedImageService? imageService) =>
        this.imageService = imageService;

    /// <summary>Gets or registers the image for a concrete completion color.</summary>
    internal ImageElement? GetOrCreate(ViuCompletionColorPresentation presentation)
    {
        if (this.imageService is null)
        {
            return null;
        }

        lock (this.gate)
        {
            if (this.images.TryGetValue(presentation.CssValue, out RegisteredImage? registered))
            {
                return registered.Element;
            }

            BitmapSource image = CreateImage(presentation.Color);
            IImageHandle handle = this.imageService.AddCustomImage(image, canTheme: false);
            ImageMoniker moniker = handle.Moniker;
            var element = new ImageElement(
                new ImageId(moniker.Guid, moniker.Id),
                "Color " + presentation.CssValue);
            registered = new RegisteredImage(handle, element);
            this.images.Add(presentation.CssValue, registered);
            return element;
        }
    }

    private static BitmapSource CreateImage(ViuCompletionColor color)
    {
        int stride = ImageSize * 4;
        var pixels = new byte[stride * ImageSize];
        for (var y = 0; y < ImageSize; y++)
        {
            for (var x = 0; x < ImageSize; x++)
            {
                bool border = x == 0 || y == 0 || x == ImageSize - 1 || y == ImageSize - 1;
                int offset = (y * stride) + (x * 4);
                if (border)
                {
                    pixels[offset] = 96;
                    pixels[offset + 1] = 96;
                    pixels[offset + 2] = 96;
                    pixels[offset + 3] = 255;
                }
                else
                {
                    pixels[offset] = color.Blue;
                    pixels[offset + 1] = color.Green;
                    pixels[offset + 2] = color.Red;
                    pixels[offset + 3] = color.Alpha;
                }
            }
        }

        BitmapSource image = BitmapSource.Create(
            ImageSize,
            ImageSize,
            96,
            96,
            PixelFormats.Bgra32,
            palette: null,
            pixels,
            stride);
        image.Freeze();
        return image;
    }

    private sealed class RegisteredImage
    {
        // The handle is deliberately retained: Visual Studio documents it as the lifetime token for
        // the custom image registered under Element.ImageId.
        internal RegisteredImage(IImageHandle handle, ImageElement element)
        {
            this.Handle = handle;
            this.Element = element;
        }

        internal IImageHandle Handle { get; }

        internal ImageElement Element { get; }
    }
}
