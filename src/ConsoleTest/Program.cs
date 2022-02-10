using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics;
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

            Test1(device);
            
            Console.WriteLine("done");
        }
        private static void Test1(IT8951SPIDevice device)
        {

            using (new Operation("Read DeviceInfo"))
            {
                var r = device.GetDeviceInfo();
                var s=System.Text.Json.JsonSerializer.Serialize(r);
                Console.WriteLine(s);
            }
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
