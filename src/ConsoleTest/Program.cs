using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using WaveshareEInkDriver;

namespace ConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {
            SpiConnectionSettings settings = new SpiConnectionSettings(0,0);
            settings.ClockFrequency = 12000000; //suggested 12MHZ in doc
            settings.Mode = SpiMode.Mode0;
            settings.ChipSelectLineActiveState = PinValue.Low;
            settings.DataFlow = DataFlow.MsbFirst;
            SpiDevice spi = SpiDevice.Create(settings);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(settings));
            var device = new IT8951SPIDevice(new IT8951SPIDeviceIO(spi,readyPin: 24,resetPin: 17));
            System.Diagnostics.Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Console.WriteLine($"IsLittleEndian:{BitConverter.IsLittleEndian} ");
            using (new Operation("Init"))
            {
                device.Init();
            }
            ReadInfoTest(device);
            RWRegisterTest(device);
            RWVComTest(device);
            testClearScreen(device);
            testBlackScreen(device);
            testClearScreen(device);
            Console.WriteLine("done");
        }
        private static void testClearScreen(IT8951SPIDevice device)
        {
            using (new Operation("testClearScreen"))
            {
                int bufferSizeX = (int)Math.Ceiling(device.DeviceInfo.ScreenSize.Width * 4.0d / 8); //imageWidth*4(Bits per pixel)/8(Bits per byte)
                int bufferSizeY = device.DeviceInfo.ScreenSize.Height;
                int bufferSize = bufferSizeX * bufferSizeY / 2;//buffer is ushort(2 byte) 
                var buffer = new ushort[bufferSize].AsSpan();
                buffer.Fill(0xffff);
                displayBuffer(device, ImageEndianTypeEnum.LittleEndian, ImagePixelPackEnum.BPP4, ImageRotateEnum.Rotate0, DisplayModeEnum.INIT, buffer);
            }
        }
        private static void testBlackScreen(IT8951SPIDevice device)
        {
            using (new Operation("testBlackScreen"))
            {
                int bufferSizeX = (int)Math.Ceiling(device.DeviceInfo.ScreenSize.Width * 4.0d / 8); //imageWidth*4(Bits per pixel)/8(Bits per byte)
                int bufferSizeY = device.DeviceInfo.ScreenSize.Height;
                int bufferSize = bufferSizeX * bufferSizeY / 2;//buffer is ushort(2 byte) 
                var buffer = new ushort[bufferSize].AsSpan();
                buffer.Fill(0x0000);
                displayBuffer(device, ImageEndianTypeEnum.LittleEndian, ImagePixelPackEnum.BPP4, ImageRotateEnum.Rotate0, DisplayModeEnum.GC16, buffer);
            }
        }
        private static void displayBuffer(IT8951SPIDevice device,ImageEndianTypeEnum endian,ImagePixelPackEnum pixelPack,ImageRotateEnum rotate, DisplayModeEnum mode,Span<ushort> buffer)
        {
            device.WaitForDisplayReady(TimeSpan.FromSeconds(5));
            device.SetTargetMemoryAddress(device.DeviceInfo.BufferAddress);
            device.LoadImageStart(endian, pixelPack, rotate);
            device.SendBuffer(buffer);
            device.LoadImageEnd();
            device.DisplayArea(0, 0, 800, 600, mode);
        }
        private void standardTest(IT8951SPIDevice device)
        {
            device.Init();
            //device.EnablePackedWrite();
            device.SetVCom(-1.91f);
        }
        private static void ReadInfoTest(IT8951SPIDevice device)
        {

            using (new Operation("Read DeviceInfo"))
            {
                var r = device.GetDeviceInfo();
                var s=System.Text.Json.JsonSerializer.Serialize(r);
                Console.WriteLine(s);
            }
        }
        private static void RWRegisterTest(IT8951SPIDevice device)
        {
            using(new Operation("Write Register value=0"))
            {
                device.WriteRegister(0x04, 0);
            }
            using(new Operation("Read Register"))
            {
                DisplayRegister(device, 0x04);
            }
            using (new Operation("Write Register value=1"))
            {
                device.WriteRegister(0x04, 1);
            }
            using (new Operation("Read Register"))
            {
                DisplayRegister(device, 0x04);
            }
        }

        private static void RWVComTest(IT8951SPIDevice device)
        {
            using (new Operation("GetVCom"))
            {
                Console.WriteLine($"VCom={device.GetVCom()}");
            }
            using (new Operation("SetVCom"))
            {
                device.SetVCom(-1.91f);
            }
            using (new Operation("GetVCom"))
            {
                Console.WriteLine($"VCom={device.GetVCom()}");
            }
        }
        private static void DisplayRegister(IT8951SPIDevice device,ushort address)
        {
            Trace.TraceInformation($"Register 0x{address:X2}= {device.ReadRegister(address)}");
        }

    }

    class Operation : IDisposable
    {
        public Operation([CallerMemberName] string name="")
        {
            Trace.WriteLine(name);
            Trace.Indent();
        }

        public void Dispose()
        {
            Trace.Unindent();
        }
    }
}
