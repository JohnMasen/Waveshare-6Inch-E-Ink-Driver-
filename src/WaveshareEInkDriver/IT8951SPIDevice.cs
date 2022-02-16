using SixLabors.ImageSharp.ColorSpaces.Conversion;
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
        BPP8=0b_0011_0000,
        BPP1=0b_1111_0000//special mode, need to handle this mode manually
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
        public IT8951SPIDevice()
        {
            SpiConnectionSettings settings = new SpiConnectionSettings(0, 0);
            settings.ClockFrequency = 12000000; //suggested 12MHZ in doc
            settings.Mode = SpiMode.Mode0;
            settings.ChipSelectLineActiveState = PinValue.Low;
            settings.DataFlow = DataFlow.MsbFirst;
            SpiDevice spi = SpiDevice.Create(settings);
            io=new IT8951SPIDeviceIO(spi, 24, 17);
        }
        public IT8951SPIDevice(SpiDevice spi, int readyPin=24, int busyPin=17)
        {
            io = new IT8951SPIDeviceIO(spi, readyPin, busyPin);
        }
        public IT8951SPIDevice(IT8951SPIDeviceIO deviceIO)
        {
            io = deviceIO;
        }

        public void Set1BPPMode(bool value)
        {
            ushort tmp = ReadRegister(0x1140);//18th bit of 0x1138 is 2nd bit of 0x1140 in MSBEndian
            if (value)
            {
                tmp = (ushort)(0b_0000_0100 | tmp);//set 2nd bit value to 1 (MSBEndian)
                WriteRegister(0x1250, 0x00F0);//foreground=G0(0x00 Black) background=G15(0xF0 White)
            }
            else
            {
                tmp = (ushort)(0b_1111_1011 & tmp);//set 2nd bit value to 0
            }
            
            WriteRegister(0x1140, tmp); //write back
        }
        /// <summary>
        /// Reset device and perform init settings
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void Init()
        {
            io.Reset();
            io.SendCommand(IT8951SPIDeviceIO.DeviceCommand.SYSN_RUN);//enable device
            io.WaitReady();
            DeviceInfo = GetDeviceInfo();
            if (DeviceInfo.LUTVersion!="M641")
            {
                throw new InvalidOperationException("Invalid device info");
            }
            EnablePackedWrite();
        }
        /// <summary>
        /// Get Device Info
        /// </summary>
        /// <returns></returns>
        private DeviceInfo GetDeviceInfo()
        {
            io.SendCommand(IT8951SPIDeviceIO.DeviceCommand.GET_DEV_INFO);
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

        public void DisplayArea(ushort x, ushort y, ushort width, ushort height,DisplayModeEnum mode)
        {
            io.SendCommand(IT8951SPIDeviceIO.DeviceCommand.DPY_AREA, x, y, width, height, (ushort)mode);
        }
        public void DisplayArea(ushort x, ushort y, ushort width, ushort height, DisplayModeEnum mode,int memoryAddress)
        {
            io.SendCommand(IT8951SPIDeviceIO.DeviceCommand.DPY_BUF_AREA, x, y, width, height, (ushort)mode,(ushort)memoryAddress,(ushort)(memoryAddress>>16) );
        }

        public (ushort user,ushort system) GetTemprature()
        {
            io.SendCommand(IT8951SPIDeviceIO.DeviceCommand.TEMPERATURE_RW, 0);
            ushort u =io.ReadData();
            ushort s = io.ReadData();
            return (user: u, system: s);
        }

        public void LoadImageEnd()
        {
            io.SendCommand(IT8951SPIDeviceIO.DeviceCommand.LD_IMG_END);
        }
        private string readDeviceBigEndianString(Span<byte> data)
        {
            //reverse byte order of words
            for (int i = 0; i < data.Length; i += 2)
            {
                data.Slice(i, 2).Reverse();
            }
            var output = data.Slice(0, data.IndexOf(byte.MinValue));//unix string ends with char(0)
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
        public void SendBuffer(Span<byte> buffer)
        {
            io.SendData(buffer);
        }

        public void LoadImageStart(ImageEndianTypeEnum endtype,ImagePixelPackEnum pixelPack,ImageRotateEnum rotate)
        {
            ushort v = (ushort)((ushort)endtype | (ushort)pixelPack | (ushort)rotate);
            io.SendCommand(IT8951SPIDeviceIO.DeviceCommand.LD_IMG, v);
        }

        public void LoadImageAreaStart(ImageEndianTypeEnum endtype, ImagePixelPackEnum pixelPack, ImageRotateEnum rotate,ushort x,ushort y,ushort width,ushort height)
        {
            ushort v = (ushort)((ushort)endtype | (ushort)pixelPack | (ushort)rotate);
            io.SendCommand(IT8951SPIDeviceIO.DeviceCommand.LD_IMG_AREA, v,x,y,width,height);
        }
        public ushort GetVCom()
        {
            io.SendCommand(IT8951SPIDeviceIO.DeviceCommand.VCOM_RW, 0);
            return io.ReadData();
        }
        public void SetVCom(float value)
        {
            ushort v = (ushort)(Math.Abs(value) * 1000);
            io.SendCommand(IT8951SPIDeviceIO.DeviceCommand.VCOM_RW, 1, v);
        }

        public ushort ReadRegister(ushort address)
        {
            io.SendCommand(IT8951SPIDeviceIO.DeviceCommand.REG_RD);
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
            io.SendCommand(IT8951SPIDeviceIO.DeviceCommand.REG_WR);
            io.SendData(address);
            io.SendData(value);
        }
    }


}
