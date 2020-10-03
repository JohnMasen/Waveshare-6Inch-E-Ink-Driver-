using System;
using System.Buffers;
using System.Data;
using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics;
using System.Drawing;
using System.Linq.Expressions;
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
        SpiDevice spi;
        private const int MAX_SEND_BUFFER_SIZE = 2048;
        private const int MAX_RECEIVE_BUFFER_SIZE = 2048;

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

        public IT8951SPIDevice(int spiDeviceId, int CSPin, int HRDYpin)
        {
            controller = new GpioController();
            controller.OpenPin(HRDYpin, PinMode.Input);
            readyPin = HRDYpin;

            controller.OpenPin(17, PinMode.Output);
            controller.Write(17, PinValue.High);

            SpiConnectionSettings settings = new SpiConnectionSettings(spiDeviceId, CSPin);
            settings.ClockFrequency = 2000000; //TODO: find the best clock time
            settings.Mode = SpiMode.Mode0;
            settings.ChipSelectLineActiveState = PinValue.Low;
            settings.DataFlow = DataFlow.MsbFirst;
            spi = SpiDevice.Create(settings);
        }

        public void EnalbeDevice()
        {
            sendCommand(0x01);
        }

        public void Sleep()
        {
            sendCommand(0x03);
        }

        public byte RegRead(byte address)
        {
            sendCommand(0x10);
            sendData(address, false, null);
            return readData<byte>(false, null);
        }

        public DeviceInfo GetDeviceInfo()
        {
            sendCommand(0x0302);
            var tmp= readData<DeviceInfoInternal>(true, null);
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
            sendData(para, true, null);
            sendData(value, true, null);
        }

        public ushort GetVCOM()
        {
            sendCommand(0x0039);
            ushort para = 0;
            sendData(para, false, null);
            return readData<ushort>(true,null);
        }

        private void sendCommand(ushort cmd)
        {
            MemoryMarshal.Write(sendCommandBuffer.AsSpan(2, 2), ref cmd);
            waitForReady();
            revertByteIfNeeded(sendCommandBuffer,new bool[] { false, true });
            Console.WriteLine($"SPI Send:{BitConverter.ToString(sendCommandBuffer)}");
            spi.Write(sendCommandBuffer);
        }



        private ReadOnlySpan<byte> revertByteIfNeeded(Span<byte> data, bool[] mask = null)
        {
            if (!BitConverter.IsLittleEndian)
            {
                return data;
            }

            if (data.Length % 2 != 0)
            {
                throw new ArgumentException(nameof(data), "data size must be multiple of 2");
            }
            if (mask == null)
            {
                for (int i = 0; i < data.Length; i += 2)
                {
                    data.Slice(i, 2).Reverse();
                }
                return data;
            }
            if (mask.Length > data.Length / 2)
            {
                throw new ArgumentOutOfRangeException(nameof(mask), "mask length cannot longer than half size of data length");
            }
            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i])
                {
                    data.Slice(i * 2, 2).Reverse();
                }
            }
            return data;

        }

        private void sendData<T>(T data, bool revertBytes, bool[] wordMask) where T : struct
        {
            var buffer = MemoryMarshal.AsBytes<T>(new T[] { data });
            sendData(buffer, revertBytes, wordMask);
        }

        private void sendData(Span<byte> data, bool revertBytes, bool[] wordMask)
        {
            int size = data.Length;
            int pos = 0;
            Console.Write("SPI Send:");
            if (revertBytes)
            {
                revertByteIfNeeded(data, wordMask);
            }
            while (size > 0)
            {
                int sendSize = Math.Min(MAX_SEND_BUFFER_SIZE, size);
                var sendBuffer = sendDataBuffer.AsSpan(0, sendSize + 2);
                data.Slice(pos, sendSize).CopyTo(sendBuffer.Slice(2, sendSize));
                pos += sendSize;
                size -= sendSize;
                waitForReady();
                spi.Write(sendBuffer);
                Console.Write(BitConverter.ToString(sendBuffer.ToArray()));
            }
            Console.WriteLine();

        }

        private void readData(Span<byte> buffer, bool reverseWords, bool[] wordMask)
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
                Console.WriteLine($"SPI Receive [{receiveBuffer.Length}]:{BitConverter.ToString(receiveBuffer.ToArray())}");
            }
            if (reverseWords)
            {
                revertByteIfNeeded(buffer, wordMask);
            }
            
        }

        private T readData<T>(bool reverseWords, bool[] wordMask) where T : struct
        {
            byte[] buffer = new byte[Marshal.SizeOf<T>()];
            readData(buffer, reverseWords, wordMask);
            Console.WriteLine($"data read[{buffer.Length}]:  {BitConverter.ToString(buffer)}");
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
            while (controller.Read(readyPin)!=PinValue.High)
            {
                if (sw.ElapsedMilliseconds>1000)
                {
                    throw new InvalidOperationException("wait time out on SPI device");
                }
            }
        }
    }

}
