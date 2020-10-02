using System;
using System.Buffers;
using System.Device.Gpio;
using System.Device.Spi;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Threading;

namespace WaveshareEInkDriver
{
    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Ansi, Size = 40)]
    public struct DeviceInfo
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
    public class IT8951SPIDevice
    {
        SpiDevice spi;
        ManualResetEvent mre = new ManualResetEvent(true);
        private const int MAX_SEND_BUFFER_SIZE = 2048;
        private const int MAX_RECEIVE_BUFFER_SIZE = 2048;

        private byte[] sendDataBuffer = new byte[MAX_SEND_BUFFER_SIZE + 2];
        private byte[] receiveDataBuffer = new byte[MAX_RECEIVE_BUFFER_SIZE + 2];
        private byte[] sendCommandBuffer = new byte[4] { 0x60, 0x00, 0x00, 0x00 };
        private readonly byte[] receiveDataPrebeam = new byte[2] { 0x10, 0x00 };


        public IT8951SPIDevice(int spiDeviceId, int CSPin, int HRDYpin)
        {
            GpioController gpioController = new GpioController();
            gpioController.OpenPin(HRDYpin, PinMode.Input);
            gpioController.RegisterCallbackForPinValueChangedEvent(HRDYpin, PinEventTypes.Rising, onDeviceReady);
            gpioController.RegisterCallbackForPinValueChangedEvent(HRDYpin, PinEventTypes.Falling, onDeviceBusy);

            gpioController.OpenPin(17, PinMode.Output);
            gpioController.Write(17, PinValue.High);

            SpiConnectionSettings settings = new SpiConnectionSettings(spiDeviceId, CSPin);
            settings.ClockFrequency = 2000000; //RP4 must use 2M clock
            settings.Mode = SpiMode.Mode0;
            settings.ChipSelectLineActiveState = PinValue.Low;
            settings.DataFlow = DataFlow.MsbFirst;
            spi = SpiDevice.Create(settings);
        }

        private void onDeviceReady(object sender, PinValueChangedEventArgs pinValueChangedEventArgs)
        {
            Console.WriteLine("HRDY-High");
            mre.Set();
        }

        private void onDeviceBusy(object sender, PinValueChangedEventArgs pinValueChangedEventArgs)
        {
            Console.WriteLine("HRDY-Low");
            mre.Reset();
        }

        //public void SendCommand(ReadOnlySpan<byte> command)
        //{
        //    sendCommand(command);
        //}

        //public T SendCommand<T>(ReadOnlySpan<byte> command, ReadOnlySpan<byte> parameter) where T : struct
        //{

        //    sendCommand(command);
        //    if (parameter != null)
        //    {
        //        sendData(parameter);
        //    }
        //    return readData<T>();
        //}

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
            sendData(address);
            return readData<byte>();
        }

        public DeviceInfo GetDeviceInfo()
        {
            sendCommand(0x0203); //BUGBUG: need to add reverse byte order for words
            return readData<DeviceInfo>();
        }




        private void sendCommand(ushort cmd)
        {
            MemoryMarshal.Write(sendCommandBuffer.AsSpan(2, 2), ref cmd);
            mre.WaitOne();
            Console.WriteLine($"SPI Send:{BitConverter.ToString(sendCommandBuffer)}");
            spi.Write(sendCommandBuffer);
        }

        private void sendData<T>(T data) where T : struct
        {
            var buffer = MemoryMarshal.AsBytes<T>(new T[] { data });
            sendData(buffer);
        }

        private void sendData(ReadOnlySpan<byte> data)
        {
            int size = data.Length;
            int pos = 0;
            Console.Write("SPI Send:");
            while (size > 0)
            {
                int sendSize = Math.Min(MAX_SEND_BUFFER_SIZE, size);
                var sendBuffer = sendDataBuffer.AsSpan(0, sendSize + 2);
                data.Slice(pos, sendSize).CopyTo(sendBuffer.Slice(2, sendSize));
                pos += sendSize;
                size -= sendSize;
                mre.WaitOne();
                spi.Write(sendBuffer);
                Console.Write(BitConverter.ToString(sendBuffer.ToArray()));
            }
            Console.WriteLine();

        }

        private void readData(Span<byte> buffer)
        {
            int size = buffer.Length;
            int pos = 0;
            while (size > 0)
            {
                int receiveSize = Math.Min(size, MAX_RECEIVE_BUFFER_SIZE);
                var receiveBuffer = receiveDataBuffer.AsSpan(0, receiveSize + 2);
                Span<byte> sendBuffer = new byte[receiveSize + 2];
                receiveDataPrebeam.CopyTo(sendBuffer.Slice(0, 2));
                mre.WaitOne();
                receiveBuffer.Fill(0);
                spi.TransferFullDuplex(sendBuffer, receiveBuffer);
                receiveBuffer.Slice(2, receiveSize).CopyTo(buffer.Slice(pos, receiveSize));
                pos += receiveSize;
                size -= receiveSize;
                Console.WriteLine($"SPI Receive [{receiveBuffer.Length}]:{BitConverter.ToString(receiveBuffer.ToArray())}");
            }
        }

        private T readData<T>() where T : struct
        {
            byte[] buffer = new byte[Marshal.SizeOf<T>()];
            readData(buffer);
            Console.WriteLine($"data read[{buffer.Length}]:  {BitConverter.ToString(buffer)}");
            var handle = GCHandle.Alloc(buffer,GCHandleType.Pinned);
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
    }

}
