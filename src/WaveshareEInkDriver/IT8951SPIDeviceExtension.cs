using SixLabors.ImageSharp.Formats.Bmp;
using System;
using System.Collections.Generic;
using System.Text;

namespace WaveshareEInkDriver
{
    public static class IT8951SPIDeviceExtension
    {
        public static Memory<byte> PrepareBuffer(this IT8951SPIDevice device, ImagePixelPackEnum bpp)
        {
            int size = (int)Math.Ceiling((decimal)device.DeviceInfo.ScreenSize.Width) * device.DeviceInfo.ScreenSize.Height;
            size /= bpp switch
            {
                ImagePixelPackEnum.BPP2 => 4,
                ImagePixelPackEnum.BPP3 => 2,
                ImagePixelPackEnum.BPP4 => 2,
                ImagePixelPackEnum.BPP8 => 1,
                _ => throw new ArgumentOutOfRangeException(nameof(bpp))
            };
            return new Memory<byte>(new byte[size]);
        }

        public static void LoadImage(this IT8951SPIDevice device,ImagePixelPackEnum bpp,ImageEndianTypeEnum endian,ImageRotateEnum rotate,Span<byte> pixelBuffer)
        {
            device.WaitForDisplayReady(TimeSpan.FromSeconds(5));
            device.SetTargetMemoryAddress(device.DeviceInfo.BufferAddress);
            device.LoadImageStart(endian, bpp, rotate);
            device.SendBuffer(pixelBuffer);
            device.LoadImageEnd();
        }

        public static void RefreshScreen(this IT8951SPIDevice device,DisplayModeEnum mode)
        {
            device.DisplayArea(0, 0, (ushort)device.DeviceInfo.ScreenSize.Width, (ushort)device.DeviceInfo.ScreenSize.Height, mode);
        }

        public static void Draw(this IT8951SPIDevice device,Action<Memory<byte>> pixelOperateCallback, ImagePixelPackEnum bpp=ImagePixelPackEnum.BPP4, ImageEndianTypeEnum endian=ImageEndianTypeEnum.BigEndian, ImageRotateEnum rotate=ImageRotateEnum.Rotate0, DisplayModeEnum mode=DisplayModeEnum.GC16)
        {
            var buffer = device.PrepareBuffer(bpp);
            pixelOperateCallback(buffer);
            device.LoadImage(bpp, endian, rotate, buffer.Span);
            device.RefreshScreen(mode);
        }
    }
}
