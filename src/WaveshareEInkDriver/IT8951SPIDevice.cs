using System;
using System.Buffers;
using System.Device.Gpio;
using System.Device.Spi;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Threading;

namespace WaveshareEInkDriver
{
    public class IT8951SPIDevice
    {
        SpiDevice spi;
        ManualResetEvent mre = new ManualResetEvent(true);
        private const int MAX_SEND_BUFFER_SIZE = 2048;
        private const int MAX_RECEIVE_BUFFER_SIZE = 2048;

        private byte[] sendDataBuffer = new byte[MAX_SEND_BUFFER_SIZE+2];    
        private byte[] receiveDataBuffer = new byte[MAX_RECEIVE_BUFFER_SIZE+2]; 
        private byte[] sendCommandBuffer = new byte[4] { 0x60, 0x00, 0x00, 0x00 };
        private readonly byte[] receiveDataPrebeam = new byte[2] { 0x10, 0x00 };
        

        public IT8951SPIDevice(int spiDeviceId, int CSPin, int HRDYpin)
        {
            GpioController gpioController = new GpioController();
            gpioController.RegisterCallbackForPinValueChangedEvent(HRDYpin, PinEventTypes.Rising, onDeviceReady);
            gpioController.RegisterCallbackForPinValueChangedEvent(HRDYpin, PinEventTypes.Falling, onDeviceBusy);
            SpiConnectionSettings settings = new SpiConnectionSettings(spiDeviceId, CSPin);
            settings.ClockFrequency = 12000000;
            settings.Mode = SpiMode.Mode0;
            settings.ChipSelectLineActiveState = PinValue.Low;
            settings.DataFlow = DataFlow.MsbFirst;
            spi = SpiDevice.Create(settings);
        }

        private void onDeviceReady(object sender, PinValueChangedEventArgs pinValueChangedEventArgs)
        {
            mre.Set();
        }

        private void onDeviceBusy(object sender, PinValueChangedEventArgs pinValueChangedEventArgs)
        {
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


        private void sendCommand(ushort cmd)
        {
            MemoryMarshal.Write(sendCommandBuffer.AsSpan(2, 2), ref cmd);
            mre.WaitOne();
            spi.Write(sendCommandBuffer);
        }

        private void sendData<T>(T data) where T:struct
        {
            var buffer = MemoryMarshal.AsBytes<T>(new T[] { data });
            sendData(buffer);
        }

        private void sendData(ReadOnlySpan<byte> data)
        {
            int size = data.Length;
            int pos = 0;
            while (size>0)
            {
                int sendSize = Math.Min(MAX_SEND_BUFFER_SIZE, size);
                var sendBuffer = sendDataBuffer.AsSpan(0, sendSize + 2);
                data.Slice(pos,sendSize).CopyTo(sendBuffer.Slice(2, sendSize));
                pos += sendSize;
                size -= sendSize;
                mre.WaitOne();
                spi.Write(sendBuffer);
            }
            
        }

        private void readData(Span<byte> buffer)
        {
            int size = buffer.Length;
            int pos = 0;
            while (size>0)
            {
                int receiveSize = Math.Min(size, MAX_RECEIVE_BUFFER_SIZE);
                var receiveBuffer = receiveDataBuffer.AsSpan(0, receiveSize + 2);
                receiveDataPrebeam.CopyTo(receiveBuffer.Slice(0, 2));
                mre.WaitOne();
                spi.TransferFullDuplex(receiveBuffer, receiveBuffer);
                receiveBuffer.Slice(2, buffer.Length).CopyTo(buffer.Slice(0,receiveSize));
                pos += receiveSize;
                size -= receiveSize;
            }
        }

        private T readData<T>()where T:struct
        {
            byte[] buffer = new byte[Marshal.SizeOf<T>()];
            readData(buffer);
            return MemoryMarshal.Read<T>(buffer);
        }
    }

}
