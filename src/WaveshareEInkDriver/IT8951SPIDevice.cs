using System;
using System.Buffers;
using System.Buffers.Binary;
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
        readonly IT8951SPIDeviceIO io;
        public IT8951SPIDevice(IT8951SPIDeviceIO deviceIO)
        {
            io = deviceIO;
        }

        public void Reset()
        {
            io.Reset();
            io.SendCommand(0x01);
            io.WaitReady();
        }
        public DeviceInfo GetDeviceInfo()
        {
            io.SendCommand(0x0302);
            Span<byte> buffer = stackalloc byte[40];
            io.ReadData(buffer);
            DeviceInfo result= new DeviceInfo();
            int width = BinaryPrimitives.ReadUInt16BigEndian(buffer);
            int height = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(2));
            int addL = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(4));
            int addH = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(6));
            reverseWord(buffer.Slice(8, 32));
            result.Version = System.Text.Encoding.ASCII.GetString(buffer.Slice(8, 16));
            result.LUTVersion = System.Text.Encoding.ASCII.GetString(buffer.Slice(24, 16));
            result.Version = result.Version.Replace("\0", string.Empty);
            result.LUTVersion = result.LUTVersion.Replace("\0", string.Empty);
            result.ScreenSize = new Size(width, height);
            result.BufferAddress = addH << 16 | addL;
            return result;
        }
        private void reverseWord(Span<byte> data)
        {
            for (int i = 0; i < data.Length; i+=2)
            {
                data.Slice(i, 2).Reverse();
            }
        }
    }


}
