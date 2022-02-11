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
    public enum ImageEndianTypeEnum: ushort
    {
        LittleEndian=0,
        BigEndian=0b_1_0000_0000
    }
    public enum ImagePixelPackEnum : ushort
    {
        BPP2=0,
        BPP3=0b_0001_0000,
        BPP4=0b_0010_0000,
        BPP8=0b_0011_0000
    }
    public enum ImageRotateEnum : ushort
    {
        Rotate0=0,
        Rotate90=0b_0000_0001,
        Rotate180=0b_0000_0010,
        Rorate270=0b_0000_0011
    }

    public enum DisplayModeEnum : ushort 
    {
        INIT=0,
        GC16=2,
        A2=4
    }
    public class IT8951SPIDevice
    {

        readonly IT8951SPIDeviceIO io;
        public DeviceInfo DeviceInfo{ get; private set; }
        public IT8951SPIDevice(IT8951SPIDeviceIO deviceIO)
        {
            io = deviceIO;
        }

        public void Init()
        {
            io.Reset();
            io.SendCommand(0x01);
            io.WaitReady();
            DeviceInfo = GetDeviceInfo();
            if (DeviceInfo.LUTVersion!="M641")
            {
                throw new InvalidOperationException("Invalid device info");
            }
            EnablePackedWrite();
        }
        public DeviceInfo GetDeviceInfo()
        {
            io.SendCommand(0x0302);
            Span<byte> buffer = stackalloc byte[40];
            io.ReadData(buffer);
            DeviceInfo result = new DeviceInfo();
            int width = BinaryPrimitives.ReadUInt16BigEndian(buffer);
            int height = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(2));
            int addL = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(4));
            int addH = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(6));
            result.Version = readDeviceBigEndianString(buffer.Slice(8, 16));
            result.LUTVersion = readDeviceBigEndianString(buffer.Slice(24, 16));
            result.ScreenSize = new Size(width, height);
            result.BufferAddress = addH << 16 | addL;
            return result;
        }

        private void reverseWord(Span<byte> data)
        {
            for (int i = 0; i < data.Length; i += 2)
            {
                data.Slice(i, 2).Reverse();
            }
        }
        public void SendBuffer(Span<ushort>buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                io.SendData(buffer[i]);
            }
        }
        public void DisplayArea(ushort x, ushort y, ushort width, ushort height,DisplayModeEnum mode)
        {
            io.SendCommand(0x0034, x, y, width, height, (ushort)mode);
        }

        public void LoadImageEnd()
        {
            io.SendCommand(0x0022);
        }
        private string readDeviceBigEndianString(Span<byte> data)
        {
            //reverse byte order of words
            for (int i = 0; i < data.Length; i += 2)
            {
                data.Slice(i, 2).Reverse();
            }
            var output = data.Slice(0, data.IndexOf(byte.MinValue));
            return System.Text.Encoding.ASCII.GetString(output);
        }
        /// <summary>
        /// Enable packed write
        /// </summary>
        public void EnablePackedWrite()
        {
            WriteRegister(0x04, 1);
        }

        public void SetTargetMemoryAddress(int address)
        {
            ushort LISAR = 0x2008;
            ushort valueH = (ushort)((address >> 16) & 0x0000ffff);
            ushort valueL = (ushort)(address & 0x0000ffff);
            WriteRegister((ushort)(LISAR + 2), valueH);
            WriteRegister(LISAR, valueL);
        }

        public void LoadImageStart(ImageEndianTypeEnum endtype,ImagePixelPackEnum pixelPack,ImageRotateEnum rotate)
        {
            ushort v = (ushort)((ushort)endtype | (ushort)pixelPack | (ushort)rotate);
            io.SendCommand(0x0020,v);
        }
        public ushort GetVCom()
        {
            io.SendCommand(0x0039, 0);
            return io.ReadData();
        }
        public void SetVCom(float value)
        {
            ushort v = (ushort)(Math.Abs(value) * 1000);
            io.SendCommand(0x0039, 1, v);
        }

        public ushort ReadRegister(ushort address)
        {
            io.SendCommand(0x0010);
            io.SendData(address);
            return io.ReadData();
        }

        public void WaitForDisplayReady(TimeSpan timeout)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (ReadRegister(0x1224) != 0)
            {
                if (sw.Elapsed > timeout)
                {
                    throw new InvalidOperationException("Wait for display timed out");
                }
                Thread.Sleep(10);
            }
            sw.Stop();
            Trace.TraceInformation($"Wait for display ready in {sw.ElapsedMilliseconds}ms");
        }

        public void WriteRegister(ushort address, ushort value)
        {
            io.SendCommand(0x0011);
            io.SendData(address);
            io.SendData(value);
        }
    }


}
