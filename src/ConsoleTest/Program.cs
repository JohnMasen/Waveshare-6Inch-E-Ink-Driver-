using Iot.Device.Ads1115;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics;
using System.IO;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using WaveshareEInkDriver;
using static WaveshareEInkDriver.IT8951SPIDeviceExtension;

namespace ConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {
            //var pp = new IT8951SPIDeviceExtension.PixelBuffer(0, 0, 800, 600, ImagePixelPackEnum.BPP1);
            var config = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();
            bool waitEnter = config["wait"]?.ToLower() == "true";
            Console.WriteLine($"WaitEnter={waitEnter}");
            //SPI settings for raspberry pi 4B
            SpiConnectionSettings settings = new SpiConnectionSettings(0, 0);
            settings.ClockFrequency = 12000000; //suggested 12MHZ in doc
            settings.Mode = SpiMode.Mode0;
            settings.ChipSelectLineActiveState = PinValue.Low;
            settings.DataFlow = DataFlow.MsbFirst;
            SpiDevice spi = SpiDevice.Create(settings);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(settings));

            var device = new IT8951SPIDevice(new IT8951SPIDeviceIO(spi, readyPin: 24, resetPin: 17));
            
            //uncomment line below to output debug info
            //System.Diagnostics.Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Console.WriteLine($"IsLittleEndian:{BitConverter.IsLittleEndian} ");

            //uncomment following line if remote debugging
            //Console.WriteLine("Waiting for debugger attach, press ENTER continue");
            //Console.ReadLine();


            using (new Operation("Init"))
            {
                device.Init();
                device.SetVCom(-1.91f);//change this to your device VCOM value
            }
            ReadTempratureTest(device);
            ReadInfoTest(device);
            RWRegisterTest(device);
            RWVComTest(device);
            testClearScreen(device, DisplayModeEnum.INIT);


            drawImagePartialTest(device, "Images/3.jpg", new Size(96, 96), DisplayModeEnum.A2, waitEnter);
            
            testClearScreen(device, DisplayModeEnum.INIT);
            drawImagePartialTest(device, "Images/3.jpg", new Size(96, 96), DisplayModeEnum.GC16, waitEnter);

            testClearScreen(device, DisplayModeEnum.INIT);
            foreach (var f in Directory.GetFiles("Images"))
            {
                testClearScreen(device, DisplayModeEnum.A2);
                testdrawImage(device, f, waitEnter, DisplayModeEnum.A2);

            }

            testClearScreen(device, DisplayModeEnum.INIT);
            foreach (var f in Directory.GetFiles("Images"))
            {

                testdrawImage(device, f, waitEnter, DisplayModeEnum.GC16);
                testClearScreen(device, DisplayModeEnum.GC16);
            }


            testClearScreen(device, DisplayModeEnum.INIT);
            Console.WriteLine("done");
        }

        private static void drawImagePartialTest(IT8951SPIDevice device, string imagePath, Size gridSize, DisplayModeEnum displayMode = DisplayModeEnum.GC16, bool waitEnter = true)
        {
            Image<L8> image = Image.Load<L8>(imagePath);
            var o = new ResizeOptions();
            o.Size = new Size(device.DeviceInfo.ScreenSize.Width, device.DeviceInfo.ScreenSize.Height);
            o.Mode = ResizeMode.Pad;
            image.Mutate(opt =>
            {
                opt.Resize(o);
                if (displayMode == DisplayModeEnum.A2)
                {
                    opt.BinaryDither(KnownDitherings.FloydSteinberg);
                }
            });
            
            int y = 0;
            while (y < image.Height)
            {
                int h = Math.Min(gridSize.Height, image.Height - y);
                int x = 0;
                while (x < image.Width)
                {
                    int w = Math.Min(gridSize.Width, image.Width - x );
                    Console.WriteLine($"x={x},y={y},w={w},h={h}");
                    device.DrawImagePartial(image, new Rectangle(x, y, w, h), new Point(x, y),
                        displayMode == DisplayModeEnum.GC16 ? ImagePixelPackEnum.BPP4 : ImagePixelPackEnum.BPP1,
                        displayMode);
                    x += w;
                }
                y += h;
            }
            if (waitEnter)
            {
                Console.WriteLine("Partial test completed, press ENTER to continue");
                Console.ReadLine();
            }
        }


        private static void ReadTempratureTest(IT8951SPIDevice device)
        {
            var data = device.GetTemprature();
            Console.WriteLine($"Temprature User={data.user},System={data.system}");
        }

        
        private static void testClearScreen(IT8951SPIDevice device, DisplayModeEnum mode = DisplayModeEnum.GC16)
        {
            using (new Operation("testClearScreen"))
            {
                device.ClearScreen(mode);
            }


        }
        
        private static void testdrawImage(IT8951SPIDevice device, string imagePath, bool waitEnter = false, DisplayModeEnum mode = DisplayModeEnum.GC16)
        {
            using (new Operation("testdrawImage"))
            {
                var img = Image.Load<L8>(imagePath);

                device.DrawImage(image: img,
                    bpp: mode == DisplayModeEnum.A2 ? ImagePixelPackEnum.BPP1 : ImagePixelPackEnum.BPP4,
                    displayMode: mode);
                if (waitEnter)
                {
                    Console.WriteLine($"Image {imagePath} Ready, Press ENTER to continue");
                    Console.ReadLine();
                }
                else
                {
                    Console.WriteLine($"Image {imagePath} Ready");
                }
            }
        }
        

        private static void ReadInfoTest(IT8951SPIDevice device)
        {

            using (new Operation("Read DeviceInfo"))
            {
                var r = device.DeviceInfo;
                var s = System.Text.Json.JsonSerializer.Serialize(r);
                Console.WriteLine(s);
            }
        }
        private static void RWRegisterTest(IT8951SPIDevice device)
        {
            using (new Operation("Write Register value=0"))
            {
                device.WriteRegister(0x04, 0);
            }
            using (new Operation("Read Register"))
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
        private static void DisplayRegister(IT8951SPIDevice device, ushort address)
        {
            Console.WriteLine($"Register 0x{address:X2}= {device.ReadRegister(address)}");
        }

    }

    class Operation : IDisposable
    {
        public Operation([CallerMemberName] string name = "")
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
