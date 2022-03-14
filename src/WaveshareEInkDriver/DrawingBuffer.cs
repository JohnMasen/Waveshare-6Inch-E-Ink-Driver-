using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;

namespace WaveshareEInkDriver
{
    /// <summary>
    /// A block memory stores data should be transfered to IT8951
    /// includes display position, pixel alignment info
    /// </summary>
    public class DrawingBuffer
    {
        /// <summary>
        ///Memory buffer
        /// </summary>
        public Memory<byte> Buffer { get; }
        /// <summary>
        /// Bits per pixel (Device mask code format)
        /// </summary>
        public ImagePixelPackEnum Bpp { get; }

        /// <summary>
        /// pixel length in bit
        /// </summary>
        public int BitsPerPixel { get; }
        /// <summary>
        /// How many pixels per byte
        /// </summary>
        public int PixelPerByte => 8 / BitsPerPixel;
        /// <summary>
        /// how many empty pixels at the start of each row
        /// these pixels are required for pixel alignment, device will ignore these pixels 
        /// </summary>
        public int GapLeft { get; }
        /// <summary>
        /// how many empty pixels at the end of each row
        /// these pixels are required for pixel alignment, device will ignore these pixels 
        /// </summary>
        public int GapRight { get; }
        /// <summary>
        /// minimum buffer unit in bytes, buffer size must be multiple of this value
        /// </summary>
        public int PixelAlignmentWidth { get; }
        /// <summary>
        /// The actual buffer area(including GapLeft and GapRight) , this may not equal to ImageArea due to byte alignment
        /// </summary>
        public Rectangle BufferArea { get; }

        /// <summary>
        /// The image area of this buffer
        /// </summary>
        public Rectangle ImageArea { get; }

        /// <summary>
        /// Buffer size in pixel for each line
        /// </summary>
        public int Stride;
        public DrawingBuffer(int x, int y, int width, int height, ImagePixelPackEnum bpp)
        {
            Bpp = bpp;
            ImageArea = new Rectangle(x, y, width, height);
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

        public IEnumerable<Memory<byte>> RowBuffers
        {
            get
            {
                for (int i = 0; i < ImageArea.Height; i++)
                {
                    yield return GetRowBuffer(i);
                }
            }
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

                    if (pixelIndex >= 0 && pixelIndex < ImageArea.Width) //if pixel is in transfer range
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
