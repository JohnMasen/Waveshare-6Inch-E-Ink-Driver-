using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics;
using WaveshareEInkDriver;

namespace ConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {
            IT8951SPIDevice device = testDevice();
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
            blank.AsSpan().Fill(0xff);
            device.LoadImageArea(IT8951SPIDevice.EndianType.LittleEndian, IT8951SPIDevice.BitPerPixel.BPP4, IT8951SPIDevice.ImageRoatation.None, 0, 0, (short)img.Width, (short)img.Height, blank);
            device.Display(0, 0, (short)img.Width, (short)img.Height, 0);
            //device.LoadImage(IT8951SPIDevice.EndianType.BigEndian, IT8951SPIDevice.BitPerPixel.BPP4, IT8951SPIDevice.ImageRoatation.None, data);
            device.LoadImageArea(IT8951SPIDevice.EndianType.LittleEndian, IT8951SPIDevice.BitPerPixel.BPP4, IT8951SPIDevice.ImageRoatation.None, 0, 0, (short)img.Width, (short)img.Height, data);
            device.Display(0, 0, (short)img.Width, (short)img.Height, 2);
            Console.WriteLine("done");
        }
        private static IT8951SPIDevice testDevice()
        {
            SpiConnectionSettings settings = new SpiConnectionSettings(0, 0);
            settings.ClockFrequency = 2000000; //safe clock time
            settings.Mode = SpiMode.Mode0;
            settings.ChipSelectLineActiveState = PinValue.Low;
            settings.DataFlow = DataFlow.MsbFirst;
            SpiDevice spi = SpiDevice.Create(settings);

            spi = new SPIDebugOutputDevice(spi);
            System.Diagnostics.Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            Console.WriteLine($"IsLittleEndian:{BitConverter.IsLittleEndian} ");


            IT8951SPIDevice device = new IT8951SPIDevice(spi, 24);

            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(settings));
            
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
            //using (new Operation("Enable pack write"))
            //{
            //    device.EnablePackWrite();
            //}

            using (new Operation("Read VCom 1"))
            {
                Console.WriteLine($"Vcom={device.GetVCOM()}");
            }
            using (new Operation("Set VCom 1910"))
            {
                device.SetVCOM(1910);
            }
            using (new Operation("Read VCom 2"))
            {
                Console.WriteLine($"Vcom={device.GetVCOM()}");
            }
            using (new Operation($"Set Image baseaddress to {result.BufferAddress}[{result.BufferAddress:X8}]"))
            {
                device.SetImageBaseAddress(result.BufferAddress);
            }
            using (new Operation("Read Image base address"))
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
