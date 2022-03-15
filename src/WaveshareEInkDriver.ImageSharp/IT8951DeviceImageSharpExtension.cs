using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace WaveshareEInkDriver
{
    public static class IT8951DeviceImageSharpExtension
    {
        /// <summary>
        /// Display a full sreen image from Image Object. 
        /// </summary>
        /// <param name="device">device to present the image</param>
        /// <param name="image"> Image object</param>
        /// <param name="dither">dither the image for better visual</param>
        /// <param name="autoResize">Resize the image if necessary. An exception will thrown if this parameter is false and image size not equal device screen size</param>
        /// <param name="bpp">BitsPerPixel mode in pixel transfer. suggest use BPP4 for GC16 and BPP1 for A2 </param>
        /// <param name="displayMode">Display mode, GC16=16 level greyscale, A2=Balck and White</param>
        public static void DrawImage(this IT8951SPIDevice device, Image<L8> image, bool dither = true, bool autoResize = true, ImagePixelPackEnum bpp = ImagePixelPackEnum.BPP4, DisplayModeEnum displayMode = DisplayModeEnum.GC16)
        {
            if (displayMode == DisplayModeEnum.INIT)
            {
                throw new ArgumentOutOfRangeException(nameof(displayMode), "displayMode can't be INIT while drawing image");
            }
            if (device.DeviceInfo.ScreenSize.Width != image.Width || device.DeviceInfo.ScreenSize.Height != image.Height)
            {
                if (!autoResize)
                {
                    throw new ArgumentException("image size should be same as device screen size");
                }
                else
                {
                    var o = new ResizeOptions();
                    o.Size = new Size(device.DeviceInfo.ScreenSize.Width, device.DeviceInfo.ScreenSize.Height);
                    o.Mode = ResizeMode.Pad;
                    image.Mutate(opt =>
                    {
                        opt.Resize(o);
                    });
                }
            }
            var p = new DrawingBuffer(device, bpp);
            if (dither)
            {
                switch (bpp)
                {
                    case ImagePixelPackEnum.BPP2:
                        image.Mutate(opt =>
                        {
                            Color[] palette = new Color[4];
                            for (int i = 0; i < palette.Length; i++)
                            {
                                palette[i] = new Color(new L16((ushort)(i * 16384)));
                            }
                            opt.Dither(KnownDitherings.FloydSteinberg, palette);
                        });
                        break;
                    case ImagePixelPackEnum.BPP3:
                        throw new NotImplementedException();
                    case ImagePixelPackEnum.BPP4: //maximum grey scale is 16 levels
                    case ImagePixelPackEnum.BPP8: //BPP8=BPP4, 4 lower bits are ignored
                        image.Mutate(opt =>
                        {
                            Color[] palette = new Color[16];
                            for (int i = 0; i < palette.Length; i++)
                            {
                                palette[i] = new Color(new L16((ushort)(i * 4096)));
                            }
                            opt.Dither(KnownDitherings.FloydSteinberg, palette);
                        });
                        break;
                    case ImagePixelPackEnum.BPP1:
                        image.Mutate(opt =>
                        {
                            opt.BinaryDither(KnownDitherings.FloydSteinberg);
                        });
                        break;
                    default:
                        break;
                }
            }

            Parallel.For(0, image.Height, i =>
              {
                  var rowBytes = image.DangerousGetPixelRowMemory(i).Span;
                  p.WriteRow(rowBytes, i, getByteFromL8);
               });

            if (bpp == ImagePixelPackEnum.BPP1)
            {
                ushort width = (ushort)(image.Width / 8);
                ushort height = (ushort)(image.Height);

                //use bpp8 to transfer full bytes data
                device.LoadImageArea(ImagePixelPackEnum.BPP8, ImageEndianTypeEnum.BigEndian, ImageRotateEnum.Rotate0, p.Buffer.Span, 0, 0, width, height);
                device.Set1BPPMode(true);
                device.RefreshArea(displayMode, 0, 0, (ushort)image.Width, (ushort)image.Height);
                device.Set1BPPMode(false);//restore to default display mode
            }
            else
            {
                device.LoadImage(bpp, ImageEndianTypeEnum.BigEndian, ImageRotateEnum.Rotate0, p.Buffer.Span);
                device.RefreshScreen(displayMode);
            }

        }
        /// <summary>
        /// Partial display image from Image Object
        /// </summary>
        /// <param name="device">device to present the image</param>
        /// <param name="image">Image object</param>
        /// <param name="sourceImageArea">the area to be displayed from source image</param>
        /// <param name="targetPosition">the top left position to place the image of device screen</param>
        /// <param name="bpp">BitsPerPixel mode in pixel transfer. suggest use BPP4 for GC16 and BPP1 for A2</param>
        /// <param name="displayMode">Display mode, GC16=16 level greyscale, A2=Balck and White</param>
        /// <exception cref="ArgumentOutOfRangeException">display mode cannot be INIT and position X and width must be multiple of 32 in 1bpp mode</exception>
        public static void DrawImagePartial(this IT8951SPIDevice device, Image<L8> image, Rectangle sourceImageArea, Point targetPosition, ImagePixelPackEnum bpp = ImagePixelPackEnum.BPP4, DisplayModeEnum displayMode = DisplayModeEnum.GC16)
        {
            if (displayMode == DisplayModeEnum.INIT)
            {
                throw new ArgumentOutOfRangeException(nameof(displayMode), "displayMode can't be INIT while drawing image");
            }
            if (bpp == ImagePixelPackEnum.BPP1)
            {
                if (sourceImageArea.Width % 32 != 0 || targetPosition.X % 32 != 0)
                {
                    throw new ArgumentOutOfRangeException("position X and width must be multiple of 32 in 1bpp mode");
                }
            }
            var p = new DrawingBuffer(targetPosition.X, targetPosition.Y, sourceImageArea.Width, sourceImageArea.Height, bpp);

            Parallel.For(0, sourceImageArea.Height, i =>
            {
                var rowBytes = image.DangerousGetPixelRowMemory(sourceImageArea.Y + i).Span.Slice(sourceImageArea.X, sourceImageArea.Width);
                p.WriteRow(rowBytes, i, getByteFromL8);
            });
            ushort x = (ushort)targetPosition.X;
            ushort y = (ushort)targetPosition.Y;
            ushort w = (ushort)sourceImageArea.Width;
            ushort h = (ushort)sourceImageArea.Height;
            if (bpp == ImagePixelPackEnum.BPP1)
            {
                //use bpp8 to transfer full bytes data
                device.LoadImageArea(ImagePixelPackEnum.BPP8, ImageEndianTypeEnum.BigEndian, ImageRotateEnum.Rotate0, p.Buffer.Span, (ushort)(x / 8), y, (ushort)(w / 8), h);
                device.Set1BPPMode(true);
                device.RefreshArea(displayMode, x, y, w, h);
                device.Set1BPPMode(false);//restore to default display mode
            }
            else
            {
                device.LoadImageArea(bpp, ImageEndianTypeEnum.BigEndian, ImageRotateEnum.Rotate0, p.Buffer.Span, x, y, w, h);
                device.RefreshArea(displayMode, x, y, w, h);
            }
        }

        //TODO: test display area buffer

        private static byte getByteFromL8(L8 source)
        {
            return source.PackedValue;
        }

    }
}
