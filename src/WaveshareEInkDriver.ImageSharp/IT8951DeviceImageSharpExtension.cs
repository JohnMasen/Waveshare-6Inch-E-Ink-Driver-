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
        public static void DrawImage(this IT8951SPIDevice device,Image<L8> image,bool dither=true, bool autoResize=true, ImagePixelPackEnum bpp = ImagePixelPackEnum.BPP4,DisplayModeEnum displayMode=DisplayModeEnum.GC16)
        {
            if (displayMode==DisplayModeEnum.INIT)
            {
                throw new ArgumentOutOfRangeException(nameof(displayMode), "displayMode can't be INIT while drawing image");
            }
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
                            opt.Dither(KnownDitherings.FloydSteinberg,palette);
                        });
                        break;
                    case ImagePixelPackEnum.BPP3:
                        throw new NotImplementedException();
                        break;
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
            image.ProcessPixelRows(acc =>
            {

                for (int i = 0; i < acc.Height; i++)
                {
                    var rowBytes = acc.GetRowSpan(i);
                    var targetRow = IT8951SPIDeviceExtension.PixelBuffer.GetRowBuffer(p, i).Span;
                    setDeviceStride(rowBytes, targetRow, p.PixelPerByte, p.GapLeft, p.GapRight, image.Width,bpp==ImagePixelPackEnum.BPP1);
                }
            });
            
            
            
            if (bpp == ImagePixelPackEnum.BPP1)
            {
                ushort width = (ushort)(image.Width / 8 );
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

        private static void setDeviceStride(Span<L8> L8Strinde, Span<byte> deviceStride, int pixelPerByte, int gapLeft, int gapRight, int width,bool reverseBitOrder=false)
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
                        if (!reverseBitOrder)
                        {
                            value = (byte)(value << (pixelSize * (pixelPerByte - p - 1)));//shift bits to correct pixel position
                        }
                        else
                        {
                            value= (byte)(value << (pixelSize * p));//shift bits to correct pixel position
                        }
                        deviceStride[i] |= value;
                    }
                    pixelIndex++;
                }
            }
        }
    }
}
