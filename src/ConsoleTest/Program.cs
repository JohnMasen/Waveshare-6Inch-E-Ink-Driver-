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
            SpiConnectionSettings settings = new SpiConnectionSettings(0, 0);
            settings.ClockFrequency = 2000000; //safe clock time
            settings.Mode = SpiMode.Mode0;
            settings.ChipSelectLineActiveState = PinValue.Low;
            settings.DataFlow = DataFlow.MsbFirst;
            SpiDevice spi = SpiDevice.Create(settings);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(settings));

            spi = new SPIDebugOutputDevice(spi);
            System.Diagnostics.Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            Console.WriteLine($"IsLittleEndian:{BitConverter.IsLittleEndian} ");


            IT8951SPIDevice device = testDevice(spi);
            Image<Bgra32> img = Image.Load<Bgra32>("ori.bmp");
            img.Mutate(op =>
            {
                op.Grayscale();
                //op.Resize(30, 30);
            });
            byte[] data = new byte[img.Width * img.Height / 2];
            int pos = 0;
            for (int row = 0; row < img.Height; row++)
            {
                var rowData = img.GetPixelRowSpan(row);
                for (int column = 0; column < rowData.Length/2; column++)
                {
                    int y1 = column*2;
                    int y2 = y1 + 1;
                    byte p1 = (byte)(rowData[y1].B >>4<<4 );
                    byte p2=(byte)(rowData[y2].B >> 4);
                    data[pos++] = (byte)( p1 | p2);
                }
            }
            byte[] blank = new byte[data.Length];
            blank.AsSpan().Fill(0x66);
            //device.RegWrite(0x04, 0);
            //Console.WriteLine($"0x04={device.RegRead(0x04)}");
            var add = device.GetDeviceInfo().BufferAddress;
            using (new Operation("RegRead MCSR 1"))
            {
                Console.WriteLine($"MCSR[0x0200]={device.RegRead(0x0200):X}");
            }
            using (new Operation("Memory Dump"))
            {
                Console.WriteLine($"MemoryDump[{add:X}]:{BitConverter.ToString(device.MemoryRead(add, 10))}");
            }

            //(spi as SPIDebugOutputDevice).EnableDumpRawData = false;
            using (new Operation("Load Image"))
            {
                device.LoadImage(IT8951SPIDevice.EndianType.LittleEndian, IT8951SPIDevice.BitPerPixel.BPP4, IT8951SPIDevice.ImageRoatation.None, blank);
            }

            //(spi as SPIDebugOutputDevice).EnableDumpRawData = true;
            using (new Operation("RegRead MCSR 2"))
            {
                Console.WriteLine($"MCSR[0x0200]={device.RegRead(0x0200):X}");
            }
            //device.LoadImageArea(IT8951SPIDevice.EndianType.LittleEndian, IT8951SPIDevice.BitPerPixel.BPP4, IT8951SPIDevice.ImageRoatation.None, 0, 0, (short)img.Width, (short)img.Height, blank);
            //Console.WriteLine($"LUT0IMXY[0X1180]={device.RegRead(0x1180):X}");
            //Console.WriteLine($"image size ={img.Width}:{img.Height}");

            //var info = device.GetDeviceInfo();
            //device.Display(0, 0, (short)img.Width, (short)img.Height, 0);
            using (new Operation("Display image"))
            {
                device.Display(0, 0, (short)img.Width, (short)img.Height, 2);
            }
            
            //device.LoadImage(IT8951SPIDevice.EndianType.BigEndian, IT8951SPIDevice.BitPerPixel.BPP4, IT8951SPIDevice.ImageRoatation.None, data);
            //device.LoadImageArea(IT8951SPIDevice.EndianType.LittleEndian, IT8951SPIDevice.BitPerPixel.BPP4, IT8951SPIDevice.ImageRoatation.None, 0, 0, (short)img.Width, (short)img.Height, data);
            //device.Display(0, 0, (short)img.Width, (short)img.Height, 2);


            Console.WriteLine("done");
        }
        private static IT8951SPIDevice testDevice(SpiDevice spi)
        {
            


            IT8951SPIDevice device = new IT8951SPIDevice(spi, 24,17);
            //using (new Operation("Reset device"))
            //{
            //    device.Reset();
            //}


            //using (new Operation("Enable Device"))
            //{
            //    device.EnalbeDevice();
            //}
            DeviceInfo result;
            using (new Operation("GetDeviceInfo"))
            {
                result = device.GetDeviceInfo();
                Console.WriteLine($"{System.Text.Json.JsonSerializer.Serialize(result)}");
            }
            if (result.ScreenSize.Width!=800)
            {
                throw new InvalidOperationException("Device Info invalid!");
            }
            //using (new Operation("Enable pack write"))
            //{
            //    device.EnablePackWrite();
            //}
            using (new Operation("check pack write"))
            {
                Console.WriteLine($"REG_0x04={device.RegRead(0x04)}");
            }

            using (new Operation("Read VCom 1"))
            {
                Console.WriteLine($"Vcom={device.GetVCOM()}");
            }
            using (new Operation("Set VCom 1910"))
            {
                device.SetVCOM(1910);
                //Thread.Sleep(1000);
            }
            using (new Operation("Read VCom 2"))
            {
                Console.WriteLine($"Vcom={device.GetVCOM()}");
            }
            using (new Operation("Read Image base address 1"))
            {
                Console.WriteLine($"Get ImageBaseAddress={device.GetImageBaseAddress()}");
            }
            using (new Operation($"Set Image baseaddress to {result.BufferAddress}[{result.BufferAddress:X8}]"))
            {
                device.SetImageBaseAddress(result.BufferAddress);
            }
            using (new Operation("Read Image base address 2"))
            {
                Console.WriteLine($"Get ImageBaseAddress={device.GetImageBaseAddress()}");
            }
            return device;
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
