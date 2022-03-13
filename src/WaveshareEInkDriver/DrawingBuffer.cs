using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace WaveshareEInkDriver
{
    public class DrawingBuffer
    {

        public Memory<byte> Buffer { get; }
        public ImagePixelPackEnum Bpp { get; }
        public int BitsPerPixel { get; }
        public int PixelPerByte => 8 / BitsPerPixel;
        public int GapLeft { get; }
        public int GapRight { get; }
        public int PixelAlignmentWidth { get; }
        public Rectangle BufferArea { get; }
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        /// <summary>
        /// Buffer size in pixel for each line
        /// </summary>
        public int Stride;
        public DrawingBuffer(int x, int y, int width, int height, ImagePixelPackEnum bpp)
        {
            Bpp = bpp;
            X = x;
            Y = y;
            Width = width;
            Height = height;
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
            BufferArea = new Rectangle(x - GapLeft, y, width + GapLeft + GapRight, height);
            Buffer = new byte[Stride * height].AsMemory();
        }

        public DrawingBuffer(IT8951SPIDevice device, ImagePixelPackEnum bpp)
            : this(0, 0, (ushort)device.DeviceInfo.ScreenSize.Width, (ushort)device.DeviceInfo.ScreenSize.Height, bpp)
        {}
        public Memory<byte> GetRowBuffer(int row)
        {
            return Buffer.Slice(Stride * row, Stride);
        }

        public void WriteRow<T>(Span<T> sourceRow,int row,Func<T,byte>valueConverter)
        {
            var target = GetRowBuffer(row).Span;
            bool reverseBitOrder = Bpp == ImagePixelPackEnum.BPP1;

            for (int i = 0; i < target.Length; i++)//scan target buffer
            {
                int pixelIndex = i * PixelPerByte - GapLeft; //try find the corresponding pixel
                for (int p = 0; p < PixelPerByte; p++) //fill byte with pixels 
                {

                    if (pixelIndex >= 0 && pixelIndex < Width) //if pixel is in transfer range
                    {
                        byte value = (byte)(valueConverter(sourceRow[pixelIndex]) >> (8 - BitsPerPixel)); //shrink to target size
                        if (!reverseBitOrder)
                        {
                            value = (byte)(value << (BitsPerPixel * (PixelPerByte - p - 1)));//shift bits to correct pixel position
                        }
                        else
                        {
                            value = (byte)(value << (BitsPerPixel * p));//shift bits to correct pixel position,1bpp uses reversed bit order
                        }
                        target[i] |= value;
                    }
                    pixelIndex++;
                }
            }
        }
    }
}
