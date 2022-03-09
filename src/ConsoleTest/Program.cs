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
            SpiConnectionSettings settings = new SpiConnectionSettings(0, 0);
            settings.ClockFrequency = 12000000; //suggested 12MHZ in doc
            settings.Mode = SpiMode.Mode0;
            settings.ChipSelectLineActiveState = PinValue.Low;
            settings.DataFlow = DataFlow.MsbFirst;
            SpiDevice spi = SpiDevice.Create(settings);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(settings)); 
            var device = new IT8951SPIDevice(new IT8951SPIDeviceIO(spi, readyPin: 24, resetPin: 17));
            System.Diagnostics.Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Console.WriteLine($"IsLittleEndian:{BitConverter.IsLittleEndian} ");
            //Console.WriteLine("Waiting for debugger attach, press ENTER continue");
            //Console.ReadLine();
            using (new Operation("Init"))
            {
                device.Init();
            }
            ReadTempratureTest(device);
            ReadInfoTest(device);
            RWRegisterTest(device);
            RWVComTest(device);
            testClearScreen(device, DisplayModeEnum.INIT);
            //specialTest(device, "Images/t1.bmp", true);
            testClearScreen(device, DisplayModeEnum.GC16);
            foreach (var f in Directory.GetFiles("Images"))
            {
                
                testdrawImage(device, f,true,DisplayModeEnum.A2);
                
            }
            foreach (var f in Directory.GetFiles("Images"))
            {
                testClearScreen(device, DisplayModeEnum.GC16);
                testdrawImage(device, f, true, DisplayModeEnum.GC16);

            }

            //testClearScreen(device, false);
            //testdrawImage(device, "Images/3.jpg");
            //for (int i = 100; i < 601; i+=100)
            //{
            //    var area = new Rectangle(0, 0, i, i*3/4);
            //    testClearArea(device, area);
            //    testDrawPartial(device, "Images/4.jpg", area, true);
            //}


            testClearScreen(device, DisplayModeEnum.INIT);
            Console.WriteLine("done");
        }

        private static void specialTest(IT8951SPIDevice device, string imagePath, bool waitEnter = false)
        {
            using (new Operation("specialTest"))
            {
                var img = Image.Load<L8>(imagePath);
                if (img.Width != device.DeviceInfo.ScreenSize.Width || img.Height != device.DeviceInfo.ScreenSize.Height)
                {
                    img.Mutate(opt =>
                    {
                        ResizeOptions o = new ResizeOptions();
                        o.Size = new Size(device.DeviceInfo.ScreenSize.Width, device.DeviceInfo.ScreenSize.Height);
                        o.Mode = ResizeMode.Pad;
                        opt.Resize(o);
                        Color[] palette = new Color[16];
                        for (int i = 0; i < palette.Length; i++)
                        {
                            palette[i] = new Color(new L16((ushort)(i * 4096)));
                        }
                        //opt.Dither(KnownDitherings.FloydSteinberg, palette);
                    });
                }
                device.Draw(x =>
                {
                    img.ProcessPixelRows(acc =>
                    {

                        for (int i = 0; i < acc.Height / 2; i++)
                        {
                            var rowBytes = acc.GetRowSpan(i);
                            var targetRow = PixelBuffer.GetRowBuffer(x, i).Span;
                            setDeviceStride(rowBytes, targetRow, x.PixelPerByte, x.GapLeft, x.GapRight, img.Width);
                        }
                        for (int i = acc.Height / 2; i < acc.Height; i++)
                        {
                            var rowBytes = acc.GetRowSpan(i);
                            var targetRow = PixelBuffer.GetRowBuffer(x, i);
                            int index = 0;
                            for (int j = 0; j < rowBytes.Length; j += 2)
                            {
                                byte output = (byte)(rowBytes[j].PackedValue & 0xf0);//from L8 to L4 is value/16(>>4), then make it high bits(<<4) which is AND 0xf0
                                output = (byte)(output | (byte)(rowBytes[j + 1].PackedValue >> 4));//second L4 bit value,output byte= 2nd value /16 | first L4 value
                                targetRow.Span[index++] = output;
                            }
                        }
                    });
                });
                if (waitEnter)
                {
                    Console.WriteLine($"Image {imagePath} Ready, Press ENTER to continue");
                    Console.ReadLine();
                }
            }
        }

        private static void ReadTempratureTest(IT8951SPIDevice device)
        {
            var data = device.GetTemprature();
            Console.WriteLine($"Temprature User={data.user},System={data.system}");
        }

        private static void testDrawPartial(IT8951SPIDevice device, string imagePath, Rectangle area, bool waitEnter = false, DisplayModeEnum mode = DisplayModeEnum.GC16)
        {
            using (new Operation($"Test draw partial"))
            {
                var img = Image.Load<L8>(imagePath);

                img.Mutate(opt =>
                {
                    ResizeOptions o = new ResizeOptions();
                    o.Size = new Size(area.Width, area.Height);
                    o.Mode = ResizeMode.Pad;
                    opt.Resize(o);
                    Color[] palette = new Color[16];
                    for (int i = 0; i < palette.Length; i++)
                    {
                        palette[i] = new Color(new L16((ushort)(i * 4096)));
                    }
                    opt.Dither(KnownDitherings.FloydSteinberg, palette);
                });
                device.DrawArea(x =>
                {
                    img.ProcessPixelRows(acc =>
                    {

                        for (int i = 0; i < acc.Height; i++)
                        {
                            var rowBytes = acc.GetRowSpan(i);
                            var targetRow = PixelBuffer.GetRowBuffer(x, i).Span;
                            setDeviceStride(rowBytes, targetRow, x.PixelPerByte, x.GapLeft, x.GapRight, img.Width);
                        }
                    });
                }, (ushort)area.X, (ushort)area.Y, (ushort)area.Width, (ushort)area.Height);
                if (waitEnter)
                {
                    Console.WriteLine("Partial update complete, Press ENTER to continue");
                    Console.ReadLine();
                }
            }
        }

        private static void setDeviceStride(Span<L8> L8Strinde, Span<byte> deviceStride, int pixelPerByte, int gapLeft, int gapRight, int width)
        {
            int pixelSize = 8 / pixelPerByte;
            for (int i = 0; i < deviceStride.Length; i++)
            {
                int pixelIndex = i * pixelPerByte - gapLeft;
                for (int p = 0; p < pixelPerByte; p++)
                {

                    if (pixelIndex >= 0 && pixelIndex < width)
                    {
                        byte value = (byte)(L8Strinde[pixelIndex].PackedValue >> (8 - pixelSize)); //shrink to target size
                        value = (byte)(value << (pixelSize * (pixelPerByte - p - 1)));//shift bits to correct pixel position
                        deviceStride[i] |= value;
                    }
                    pixelIndex++;
                }
            }
        }

        private static void testClearScreen(IT8951SPIDevice device, DisplayModeEnum mode = DisplayModeEnum.GC16)
        {
            using (new Operation("testClearScreen"))
            {
                device.Draw(x =>
                {
                    x.Buffer.Span.Fill(0xff);
                }, mode: mode
                , bpp: mode == DisplayModeEnum.A2 ? ImagePixelPackEnum.BPP1 : ImagePixelPackEnum.BPP4);
            }
        }
        private static void testClearArea(IT8951SPIDevice device, Rectangle area)
        {
            using (new Operation("testClearScreen"))
            {
                device.DrawArea(x =>
                {
                    x.Buffer.Span.Fill(0xff);
                }, (ushort)area.X, (ushort)area.Y, (ushort)area.Width, (ushort)area.Height);
            }
        }
        private static void testdrawImage(IT8951SPIDevice device, string imagePath, bool waitEnter = false,DisplayModeEnum mode=DisplayModeEnum.GC16)
        {
            using (new Operation("testdrawImage"))
            {
                var img = Image.Load<L8>(imagePath);
                
                device.DrawImage(image: img,
                    bpp: mode==DisplayModeEnum.A2?ImagePixelPackEnum.BPP1:ImagePixelPackEnum.BPP4,
                    displayMode:mode);
                if (waitEnter)
                {
                    Console.WriteLine($"Image {imagePath} Ready, Press ENTER to continue");
                    Console.ReadLine();
                }
            }
        }
        private static void testBlackScreen(IT8951SPIDevice device)
        {
            using (new Operation("testBlackScreen"))
            {
                device.Draw(x =>
                {
                    x.Buffer.Span.Fill(0x00);
                });
            }
        }
        private static void displayBuffer(IT8951SPIDevice device, ImageEndianTypeEnum endian, ImagePixelPackEnum pixelPack, ImageRotateEnum rotate, DisplayModeEnum mode, Span<ushort> buffer)
        {
            device.WaitForDisplayReady(TimeSpan.FromSeconds(5));
            device.SetTargetMemoryAddress(device.DeviceInfo.BufferAddress);
            device.LoadImageStart(endian, pixelPack, rotate);
            //device.SendBuffer(buffer);
            device.SendBuffer(MemoryMarshal.AsBytes(buffer));
            device.LoadImageEnd();
            device.DisplayArea(0, 0, 800, 600, mode);
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
            Trace.TraceInformation($"Register 0x{address:X2}= {device.ReadRegister(address)}");
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
