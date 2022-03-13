using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using System;
using System.Collections.Generic;
using System.Text;

namespace WaveshareEInkDriver
{
    public static class IT8951SPIDeviceExtension
    {
       


        public static void LoadImage(this IT8951SPIDevice device, ImagePixelPackEnum bpp, ImageEndianTypeEnum endian, ImageRotateEnum rotate, Span<byte> pixelBuffer)
        {
            device.WaitForDisplayReady(TimeSpan.FromSeconds(5));
            device.SetTargetMemoryAddress(device.DeviceInfo.BufferAddress);
            device.LoadImageStart(endian, bpp, rotate);
            device.SendBuffer(pixelBuffer);
            device.LoadImageEnd();
        }
        public static void LoadImageArea(this IT8951SPIDevice device, ImagePixelPackEnum bpp, ImageEndianTypeEnum endian, ImageRotateEnum rotate, Span<byte> pixelBuffer, ushort x, ushort y, ushort width, ushort height,ushort displayBufferAddressOffset=0)
        {
            device.WaitForDisplayReady(TimeSpan.FromSeconds(5));
            device.SetTargetMemoryAddress(device.DeviceInfo.BufferAddress+displayBufferAddressOffset);
            device.LoadImageAreaStart(endian, bpp, rotate, x, y, width, height);
            device.SendBuffer(pixelBuffer);
            device.LoadImageEnd();
        }

        public static void RefreshScreen(this IT8951SPIDevice device, DisplayModeEnum mode)
            => device.DisplayArea(0, 0, (ushort)device.DeviceInfo.ScreenSize.Width, (ushort)device.DeviceInfo.ScreenSize.Height, mode);

        public static void RefreshArea(this IT8951SPIDevice device, DisplayModeEnum mode, ushort x, ushort y, ushort width, ushort height)
        {
            device.DisplayArea(x, y, width, height, mode);
        }


        public static void Draw(this IT8951SPIDevice device, Action<DrawingBuffer> pixelOperateCallback, ImagePixelPackEnum bpp = ImagePixelPackEnum.BPP4, ImageEndianTypeEnum endian = ImageEndianTypeEnum.BigEndian, ImageRotateEnum rotate = ImageRotateEnum.Rotate0, DisplayModeEnum mode = DisplayModeEnum.GC16)
        {
            var p = new DrawingBuffer(device, bpp);
            pixelOperateCallback(p);
            if (bpp==ImagePixelPackEnum.BPP1)
            {
                device.LoadImageArea(ImagePixelPackEnum.BPP8, endian, rotate, p.Buffer.Span, 0, 0, (ushort)(device.DeviceInfo.ScreenSize.Width / 8),(ushort) device.DeviceInfo.ScreenSize.Height);
                device.Set1BPPMode(true);
                device.RefreshScreen(DisplayModeEnum.A2);
                device.Set1BPPMode(false);
            }
            else
            {
                device.LoadImage(bpp, endian, rotate, p.Buffer.Span);
                device.RefreshScreen(mode);
            }
            
        }

        public static void DrawArea(this IT8951SPIDevice device, Action<DrawingBuffer> pixelOperateCallback, ushort x, ushort y, ushort width, ushort height, ImagePixelPackEnum bpp = ImagePixelPackEnum.BPP4, ImageEndianTypeEnum endian = ImageEndianTypeEnum.BigEndian, ImageRotateEnum rotate = ImageRotateEnum.Rotate0, DisplayModeEnum mode = DisplayModeEnum.GC16)
        {
            var p = new DrawingBuffer( x, y, width, height,bpp);
            pixelOperateCallback(p);
            device.LoadImageArea(bpp, endian, rotate, p.Buffer.Span, x, y, width, height);
            device.RefreshArea(mode, x, y, width, height);
        }

        public static void ClearScreen(this IT8951SPIDevice device, DisplayModeEnum mode = DisplayModeEnum.INIT)
        {
            switch (mode)
            {
                case DisplayModeEnum.INIT:
                case DisplayModeEnum.GC16:
                    device.Draw(b =>
                    {
                        b.Buffer.Span.Fill(0xff);
                    },mode:mode);
                    break;
                case DisplayModeEnum.A2:
                    device.Draw(b =>
                    {
                        b.Buffer.Span.Fill(0xff);
                    },bpp:ImagePixelPackEnum.BPP1,
                    mode:DisplayModeEnum.A2);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }
    }
}
