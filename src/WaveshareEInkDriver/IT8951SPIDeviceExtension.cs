using SixLabors.ImageSharp.Formats.Bmp;
using System;
using System.Collections.Generic;
using System.Text;

namespace WaveshareEInkDriver
{
    public static class IT8951SPIDeviceExtension
    {
        public struct PixelBuffer
        {
            public readonly Memory<byte> Buffer;
            public int LastBytePixels;
            public bool LastByteFull;
            public ImagePixelPackEnum Bpp;
            /// <summary>
            /// Buffer size in pixel for each line
            /// </summary>
            public int Stride;
            public PixelBuffer(int width, int height, ImagePixelPackEnum bpp)
            {
                Bpp = bpp;
                int pixelPerByte = bpp switch
                {
                    ImagePixelPackEnum.BPP2 => 4,
                    ImagePixelPackEnum.BPP3 => 2,
                    ImagePixelPackEnum.BPP4 => 2,
                    ImagePixelPackEnum.BPP8 => 1,
                    ImagePixelPackEnum.BPP1 => 8,
                    _ => throw new ArgumentOutOfRangeException(nameof(bpp)),
                };
                LastByteFull = width % pixelPerByte == 0;
                Stride = width / pixelPerByte + (LastByteFull ? 0 : 1);
                LastBytePixels = LastByteFull ? pixelPerByte : width % pixelPerByte;
                Buffer = new byte[Stride * height].AsMemory();
            }
            public static Memory<byte> GetRowBuffer(PixelBuffer pixelBuffer, int row)
            {
                return pixelBuffer.Buffer.Slice(pixelBuffer.Stride * row);
            }
        }


        public static PixelBuffer PrepareBuffer(this IT8951SPIDevice device, ImagePixelPackEnum bpp)
            => PrepareBuffer(device, bpp, device.DeviceInfo.ScreenSize.Width, device.DeviceInfo.ScreenSize.Height);
      
        public static PixelBuffer PrepareBuffer(this IT8951SPIDevice device, ImagePixelPackEnum bpp,int width,int height)
        {
            return new PixelBuffer(width, height, bpp);
        }

        public static void LoadImage(this IT8951SPIDevice device, ImagePixelPackEnum bpp, ImageEndianTypeEnum endian, ImageRotateEnum rotate, Span<byte> pixelBuffer)
        {
            device.WaitForDisplayReady(TimeSpan.FromSeconds(5));
            device.SetTargetMemoryAddress(device.DeviceInfo.BufferAddress);
            device.LoadImageStart(endian, bpp, rotate);
            device.SendBuffer(pixelBuffer);
            device.LoadImageEnd();
        }
        public static void LoadImageArea(this IT8951SPIDevice device, ImagePixelPackEnum bpp, ImageEndianTypeEnum endian, ImageRotateEnum rotate, Span<byte> pixelBuffer,ushort x,ushort y,ushort width,ushort height)
        {
            device.WaitForDisplayReady(TimeSpan.FromSeconds(5));
            device.SetTargetMemoryAddress(device.DeviceInfo.BufferAddress);
            device.LoadImageAreaStart(endian, bpp, rotate,x,y,width,height);
            device.SendBuffer(pixelBuffer);
            device.LoadImageEnd();
        }

        public static void RefreshScreen(this IT8951SPIDevice device, DisplayModeEnum mode)
            => device.DisplayArea(0, 0, (ushort)device.DeviceInfo.ScreenSize.Width, (ushort)device.DeviceInfo.ScreenSize.Height, mode);

        public static void RefreshArea(this IT8951SPIDevice device,DisplayModeEnum mode,ushort x,ushort y,ushort width,ushort height)
        {
            device.DisplayArea(x, y, width, height, mode);
        }


        public static void Draw(this IT8951SPIDevice device, Action<PixelBuffer> pixelOperateCallback, ImagePixelPackEnum bpp = ImagePixelPackEnum.BPP4, ImageEndianTypeEnum endian = ImageEndianTypeEnum.BigEndian, ImageRotateEnum rotate = ImageRotateEnum.Rotate0, DisplayModeEnum mode = DisplayModeEnum.GC16)
        {
            var p = device.PrepareBuffer(bpp);
            pixelOperateCallback(p);
            device.LoadImage(bpp, endian, rotate, p.Buffer.Span);
            device.RefreshScreen(mode);
        }

        public static void DrawArea(this IT8951SPIDevice device, Action<PixelBuffer> pixelOperateCallback,ushort x,ushort y,ushort width,ushort height, ImagePixelPackEnum bpp = ImagePixelPackEnum.BPP4, ImageEndianTypeEnum endian = ImageEndianTypeEnum.BigEndian, ImageRotateEnum rotate = ImageRotateEnum.Rotate0, DisplayModeEnum mode = DisplayModeEnum.GC16)
        {
            var p = device.PrepareBuffer(bpp,width,height);
            pixelOperateCallback(p);
            device.LoadImageArea(bpp, endian, rotate, p.Buffer.Span,x,y,width,height);
            device.RefreshArea(mode,x,y,height,width);
        }
    }
}
