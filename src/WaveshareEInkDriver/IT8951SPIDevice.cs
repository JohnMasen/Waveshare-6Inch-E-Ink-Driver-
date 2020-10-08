using System;
using System.Buffers;
using System.Data;
using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics;
using System.Drawing;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace WaveshareEInkDriver
{
    public class DeviceInfo
    {
        public Size ScreenSize { get; set; }
        public int BufferAddress { get; set; }
        public string Version { get; set; }
        public string LUTVersion { get; set; }
    }

    public class IT8951SPIDevice
    {
        public enum BitPerPixel 
        {
            BPP2 = 0,
            BPP3 = 0b_01_0000,
            BPP4 = 0b_10_0000,
            BPP8 = 0b_11_0000
        }

        public enum ImageRoatation 
        {
            None = 0,
            R90 = 0b_01,
            R180 = 0b_10,
            R270 = 0b_11
        }

        public enum EndianType
        {
            LittleEndian=0,
            BigEndian=0b_1_00000000
        }



        SpiDevice spi;
        private const int MAX_SEND_BUFFER_SIZE = 2046;
        private const int MAX_RECEIVE_BUFFER_SIZE = 2044;

        private byte[] sendDataBuffer = new byte[MAX_SEND_BUFFER_SIZE + 2];
        private byte[] receiveDataBuffer = new byte[MAX_RECEIVE_BUFFER_SIZE + 4];
        private byte[] sendCommandBuffer = new byte[4] { 0x60, 0x00, 0x00, 0x00 };
        private readonly byte[] receiveDataPrebeam = new byte[2] { 0x10, 0x00 };
        GpioController controller;
        private int readyPin;
        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Ansi, Size = 40)]
        private struct DeviceInfoInternal
        {
            [FieldOffset(0)]
            public ushort Width;
            [FieldOffset(2)]
            public ushort Height;
            [FieldOffset(4)]
            public ushort bufferAddressL;
            [FieldOffset(6)]
            public ushort bufferAddressH;
            [FieldOffset(8)]
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
            public string Version;
            [FieldOffset(24)]
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
            public string LUTVersion;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct DisplayInfo
        {
            [FieldOffset(0)]
            public short X;
            [FieldOffset(2)]
            public short Y;
            [FieldOffset(4)]
            public short Width;
            [FieldOffset(6)]
            public short Height;
            [FieldOffset(8)]
            public short Mode;

        }
        [StructLayout(LayoutKind.Explicit)]
        private struct LoadImageAreaInfo
        {
            [FieldOffset(0)]
            public short MemoryConvertSetting;
            [FieldOffset(2)]
            public short X;
            [FieldOffset(4)]
            public short Y;
            [FieldOffset(6)]
            public short Width;
            [FieldOffset(8)]
            public short Height;
        }

        public IT8951SPIDevice(int spiDeviceId, int CSPin, int HRDYpin)
        {
            SpiConnectionSettings settings = new SpiConnectionSettings(spiDeviceId, CSPin);
            settings.ClockFrequency = 2000000; //safe clock time
            settings.Mode = SpiMode.Mode0;
            settings.ChipSelectLineActiveState = PinValue.Low;
            settings.DataFlow = DataFlow.MsbFirst;
            initInternal(SpiDevice.Create(settings), HRDYpin);
        }
        public IT8951SPIDevice(SpiDevice spiDevice, int HRDYPin)
        {
            initInternal(spiDevice, HRDYPin);
        }

        private void initInternal(SpiDevice spiDevice, int HRDYPin)
        {
            controller = new GpioController();
            controller.OpenPin(HRDYPin, PinMode.Input);
            readyPin = HRDYPin;
            spi = spiDevice;
        }

        public void EnalbeDevice()
        {
            sendCommand(0x01);
        }

        public void Sleep()
        {
            sendCommand(0x03);
        }


        public DeviceInfo GetDeviceInfo()
        {
            sendCommand(0x0302);
            var tmp = readData<DeviceInfoInternal>(true, null);
            return new DeviceInfo()
            {
                ScreenSize = new Size(tmp.Width, tmp.Height),
                Version = tmp.Version,
                LUTVersion = tmp.LUTVersion,
                BufferAddress = (tmp.bufferAddressH << 16) + tmp.bufferAddressL
            };
        }
        public void SetVCOM(ushort value)
        {
            sendCommand(0x0039);
            ushort para = 1;
            sendData(para);
            sendData(value);
        }

        public ushort GetVCOM()
        {
            sendCommand(0x0039);
            ushort para = 0;
            sendData(para);
            var result = readData<ushort>(true, null);
            return result;
        }

        private void sendCommand(ushort cmd)
        {
            MemoryMarshal.Write(sendCommandBuffer.AsSpan(2, 2), ref cmd);
            waitForReady();
            revertByteIfNeeded(sendCommandBuffer, true);
            //Console.WriteLine($"SPI Send:{BitConverter.ToString(sendCommandBuffer)}");
            spi.Write(sendCommandBuffer);
        }
        public ushort RegRead(ushort address)
        {
            sendCommand(0x0010);
            sendData(address);
            return readData<ushort>(true, null);
        }

        public void EnablePackWrite()
        {
            RegWrite(0x04, 1);
        }
        public void RegWrite(ushort address, ushort value)
        {
            sendCommand(0x0011);
            sendData(address);
            sendData(value);
        }

        public int GetImageBaseAddress()
        {
            ushort h, l;
            l = RegRead(0x208);
            h = RegRead(0x20A);
            return (h << 16) + l;
        }

        public void SetImageBaseAddress(int address)
        {
            ushort h, l;
            h = (ushort)(address >> 16);
            l = (ushort)address;
            RegWrite(0x208, l);
            RegWrite(0x20A, h);
        }


        private ReadOnlySpan<byte> revertByteIfNeeded(Span<byte> data, bool skipFirstWord = false)
        {
            if (!BitConverter.IsLittleEndian)
            {
                return data;
            }

            if (data.Length % 2 != 0)
            {
                throw new ArgumentException(nameof(data), "data size must be multiple of 2");
            }

            for (int i = 0; i < data.Length; i += 2)
            {
                if (i > 0 || !skipFirstWord)
                {
                    data.Slice(i, 2).Reverse();
                }
            }
            return data;

        }

        public void LoadImage(EndianType endian, BitPerPixel bpp, ImageRoatation rotation,Span<byte> data)
        {
            waitForDisplayReady();
            short para = (short)((int)endian |(int) bpp |(int) rotation);
            sendCommand(0x0020);
            sendData(para);
            sendData(data);

            sendCommand(0x0022);//LD_IMG_END
        }

        public void LoadImageArea(EndianType endian, BitPerPixel bpp, ImageRoatation rotation, short x, short y, short width, short height, Span<byte> data)
        {
            waitForDisplayReady();
            LoadImageAreaInfo para;
            para.MemoryConvertSetting= (short)((int)endian | (int)bpp | (int)rotation);
            para.X = x;
            para.Y = y;
            para.Width = width;
            para.Height = height;
            sendCommand(0x0021);
            sendData(para);
            sendData(data);

            sendCommand(0x0022);
        }

        private void sendData<T>(T data) where T : struct
        {
            var buffer = MemoryMarshal.AsBytes<T>(new T[] { data });
            sendData(buffer);
        }

        private void sendData(Span<byte> data)
        {
            int size = data.Length;
            int pos = 0;
            //Console.Write("SPI Send:");

            revertByteIfNeeded(data);

            while (size > 0)
            {
                int sendSize = Math.Min(MAX_SEND_BUFFER_SIZE, size);
                var sendBuffer = sendDataBuffer.AsSpan(0, sendSize + 2);
                data.Slice(pos, sendSize).CopyTo(sendBuffer.Slice(2, sendSize));
                pos += sendSize;
                size -= sendSize;
                waitForReady();
                spi.Write(sendBuffer);
                //Console.Write(BitConverter.ToString(sendBuffer.ToArray()));
            }
            //Console.WriteLine();

        }

        public void Display(short x, short y,short width,short height,short mode)
        {
            DisplayInfo info;
            info.X = x;
            info.Y = y;
            info.Width = width;
            info.Height = height;
            info.Mode = mode;
            sendCommand(0x0034);
            sendData(info);
            waitForReady();
        }

        private void readData(Span<byte> buffer)
        {
            int size = buffer.Length;
            int pos = 0;
            while (size > 0)
            {
                int receiveSize = Math.Min(size, MAX_RECEIVE_BUFFER_SIZE);
                var receiveBuffer = receiveDataBuffer.AsSpan(0, receiveSize + 4);
                Span<byte> sendBuffer = new byte[receiveSize + 4];
                receiveDataPrebeam.CopyTo(sendBuffer.Slice(0, 2));
                waitForReady();
                receiveBuffer.Fill(0);
                spi.TransferFullDuplex(sendBuffer, receiveBuffer);
                receiveBuffer.Slice(4, receiveSize).CopyTo(buffer.Slice(pos, receiveSize));
                pos += receiveSize;
                size -= receiveSize;
                //Console.WriteLine($"SPI Receive [{receiveBuffer.Length}]:{BitConverter.ToString(receiveBuffer.ToArray())}");
            }

            revertByteIfNeeded(buffer);


        }

        private T readData<T>(bool reverseWords, bool[] wordMask) where T : struct
        {
            byte[] buffer = new byte[Marshal.SizeOf<T>()];
            readData(buffer);
            //Console.WriteLine($"data read[{buffer.Length}]:  {BitConverter.ToString(buffer)}");
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var result = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
                return result;
            }
            catch (Exception)
            {

                throw;
            }
            finally
            {
                handle.Free();
            }

            //return MemoryMarshal.Read<T>(buffer);
        }

        private void waitForReady()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            while (controller.Read(readyPin) != PinValue.High)
            {
                if (sw.ElapsedMilliseconds > 1000)
                {
                    throw new InvalidOperationException("wait time out on SPI device");
                }
            }
        }

        private void waitForDisplayReady()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            while(RegRead(0x1224)!=0)
            {
                if (sw.ElapsedMilliseconds>10000)
                {
                    throw new InvalidOperationException("wait display ready time out");
                }
            }
        }
    }

}
