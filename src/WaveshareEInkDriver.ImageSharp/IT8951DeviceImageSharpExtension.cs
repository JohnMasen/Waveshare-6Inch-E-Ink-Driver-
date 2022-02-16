using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;

namespace WaveshareEInkDriver
{
    public static class IT8951DeviceImageSharpExtension
    {
        /// <summary>
        /// Display a full sreen image from Image Object. 
        /// </summary>
        /// <param name="device">device to present the image</param>
        /// <param name="image"> Image object</param>
        /// <param name="dither"></param>
        /// <param name="autoResize">Resize the image if necessary. An exception will thrown if this parameter is false and image size not equal device screen size</param>
        /// <param name="bpp"></param>
        /// <param name="displayMode">Display mode, GC16=16 level greyscale, A2=Balck and White</param>
        public static void Draw(this IT8951SPIDevice device,Image<L8> image,bool dither=true, bool autoResize=true, ImagePixelPackEnum bpp = ImagePixelPackEnum.BPP4,DisplayModeEnum displayMode=DisplayModeEnum.GC16)
        {
            if (device.DeviceInfo.ScreenSize.Width!=image.Width || device.DeviceInfo.ScreenSize.Height!=image.Height)
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
            var p = device.PrepareBuffer(bpp);
            if (dither && bpp==ImagePixelPackEnum.BPP4)
            {
                image.Mutate(opt =>
                {
                    //TODO: only works for 4 bit grey scale,should fix it
                    Color[] palette = new Color[16];
                    for (int i = 0; i < palette.Length; i++)
                    {
                        palette[i] = new Color(new L16((ushort)(i * 4096)));
                    }
                    opt.Dither(KnownDitherings.FloydSteinberg, palette);
                });
            }
            image.ProcessPixelRows(acc =>
            {

                for (int i = 0; i < acc.Height; i++)
                {
                    var rowBytes = acc.GetRowSpan(i);
                    var targetRow = IT8951SPIDeviceExtension.PixelBuffer.GetRowBuffer(p, i).Span;
                    setDeviceStride(rowBytes, targetRow, p.PixelPerByte, p.GapLeft, p.GapRight, image.Width);
                }
            });
            device.LoadImage(bpp, ImageEndianTypeEnum.BigEndian, ImageRotateEnum.Rotate0, p.Buffer.Span);
            device.RefreshScreen(displayMode);
        }

        private static void setDeviceStride(Span<L8> L8Strinde, Span<byte> deviceStride, int pixelPerByte, int gapLeft, int gapRight, int width)
        {
            int pixelSize = 8 / pixelPerByte; //pixel size in bits
            for (int i = 0; i < deviceStride.Length; i++)
            {
                int pixelIndex = i * pixelPerByte - gapLeft;
                for (int p = 0; p < pixelPerByte; p++)
                {

                    if (pixelIndex >= 0 && pixelIndex < width)
                    {
                        byte value = (byte)(L8Strinde[pixelIndex].PackedValue >> (8 - pixelSize)); //shrink to target size
                        value = (byte)(value << (pixelSize * (pixelPerByte - p - 1)));//shift bits to correct pixel position
                        deviceStride[i] |= value;
                    }
                    pixelIndex++;
                }
            }
        }
    }
}
