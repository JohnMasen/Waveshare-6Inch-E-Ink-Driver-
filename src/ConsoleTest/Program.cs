using System;
using WaveshareEInkDriver;

namespace ConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Console.WriteLine($"Is IsLittleEndian:{BitConverter.IsLittleEndian} ");
            IT8951SPIDevice device = new IT8951SPIDevice(0, 0, 24);
            var result = device.GetDeviceInfo();
            Console.WriteLine($"{System.Text.Json.JsonSerializer.Serialize(result)}");
            device.SetVCOM(1910);
            Console.WriteLine($"Vcom={device.GetVCOM()}");
            
        }
    }
}
