using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics;
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
            //settings.ClockFrequency = 5000000; //safe clock time
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
                device.Reset();
            }

            ReadInfoTest(device);
            RWRegisterTest(device);
            RWVComTest(device);
            Console.WriteLine("done");
        }

        private void standardTest(IT8951SPIDevice device)
        {
            device.Reset();
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
        public Operation(string name)
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
