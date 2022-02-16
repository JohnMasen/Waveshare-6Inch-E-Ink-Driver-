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
            public ImagePixelPackEnum Bpp;
            public readonly int BitsPerPixel;
            public readonly int PixelPerByte;
            public readonly int GapLeft;
            public readonly int GapRight;
            public readonly int PixelAlignmentWidth;
            /// <summary>
            /// Buffer size in pixel for each line
            /// </summary>
            public int Stride;
            public PixelBuffer(int x, int y, int width, int height, ImagePixelPackEnum bpp)
            {
                Bpp = bpp;
                PixelPerByte = bpp switch
                {
                    ImagePixelPackEnum.BPP2 => 4,
                    ImagePixelPackEnum.BPP3 => 2,
                    ImagePixelPackEnum.BPP4 => 2,
                    ImagePixelPackEnum.BPP8 => 1,
                    ImagePixelPackEnum.BPP1 => 8,
                    _ => throw new ArgumentOutOfRangeException(nameof(bpp)),
                };
                BitsPerPixel = bpp switch
                {
                    ImagePixelPackEnum.BPP2 => 2,
                    ImagePixelPackEnum.BPP3 => 4,
                    ImagePixelPackEnum.BPP4 => 4,
                    ImagePixelPackEnum.BPP8 => 8,
                    ImagePixelPackEnum.BPP1 => 1,
                    _ => throw new ArgumentOutOfRangeException(nameof(bpp)),
                };
                PixelAlignmentWidth = bpp switch
                {
                    ImagePixelPackEnum.BPP2 => 16,
                    ImagePixelPackEnum.BPP3 => 16,
                    ImagePixelPackEnum.BPP4 => 16,
                    ImagePixelPackEnum.BPP8 => 16,
                    ImagePixelPackEnum.BPP1 => 32,
                    _ => throw new ArgumentOutOfRangeException(nameof(bpp))
                };
                int x1 = x;
                int x2 = x1 + width;

                int pixelsPerPack = PixelAlignmentWidth / BitsPerPixel;
                GapLeft = x1 % pixelsPerPack;
                GapRight = x2 % pixelsPerPack == 0 ? 0 : pixelsPerPack - x2 % pixelsPerPack;

                Stride = (width + GapLeft + GapRight) / PixelPerByte;
                Buffer = new byte[Stride * height].AsMemory();
            }
            public static Memory<byte> GetRowBuffer(PixelBuffer pixelBuffer, int row)
            {
                return pixelBuffer.Buffer.Slice(pixelBuffer.Stride * row,pixelBuffer.Stride);
            }
        }


        public static PixelBuffer PrepareBuffer(this IT8951SPIDevice device, ImagePixelPackEnum bpp)
            => PrepareBuffer(device, bpp, 0, 0, device.DeviceInfo.ScreenSize.Width, device.DeviceInfo.ScreenSize.Height);

        public static PixelBuffer PrepareBuffer(this IT8951SPIDevice device, ImagePixelPackEnum bpp, int x, int y, int width, int height)
        {
            return new PixelBuffer(x, y, width, height, bpp);
        }

        public static void LoadImage(this IT8951SPIDevice device, ImagePixelPackEnum bpp, ImageEndianTypeEnum endian, ImageRotateEnum rotate, Span<byte> pixelBuffer)
        {
            device.WaitForDisplayReady(TimeSpan.FromSeconds(5));
            device.SetTargetMemoryAddress(device.DeviceInfo.BufferAddress);
            device.LoadImageStart(endian, bpp, rotate);
            device.SendBuffer(pixelBuffer);
            device.LoadImageEnd();
        }
        public static void LoadImageArea(this IT8951SPIDevice device, ImagePixelPackEnum bpp, ImageEndianTypeEnum endian, ImageRotateEnum rotate, Span<byte> pixelBuffer, ushort x, ushort y, ushort width, ushort height)
        {
            device.WaitForDisplayReady(TimeSpan.FromSeconds(5));
            device.SetTargetMemoryAddress(device.DeviceInfo.BufferAddress);
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


        public static void Draw(this IT8951SPIDevice device, Action<PixelBuffer> pixelOperateCallback, ImagePixelPackEnum bpp = ImagePixelPackEnum.BPP4, ImageEndianTypeEnum endian = ImageEndianTypeEnum.BigEndian, ImageRotateEnum rotate = ImageRotateEnum.Rotate0, DisplayModeEnum mode = DisplayModeEnum.GC16)
        {
            var p = device.PrepareBuffer(bpp);
            pixelOperateCallback(p);
            device.LoadImage(bpp, endian, rotate, p.Buffer.Span);
            device.RefreshScreen(mode);
        }

        public static void DrawArea(this IT8951SPIDevice device, Action<PixelBuffer> pixelOperateCallback, ushort x, ushort y, ushort width, ushort height, ImagePixelPackEnum bpp = ImagePixelPackEnum.BPP4, ImageEndianTypeEnum endian = ImageEndianTypeEnum.BigEndian, ImageRotateEnum rotate = ImageRotateEnum.Rotate0, DisplayModeEnum mode = DisplayModeEnum.GC16)
        {
            var p = device.PrepareBuffer(bpp, x, y, width, height);
            pixelOperateCallback(p);
            device.LoadImageArea(bpp, endian, rotate, p.Buffer.Span, x, y, width, height);
            device.RefreshArea(mode, x, y, width, height);
        }
    }
}
