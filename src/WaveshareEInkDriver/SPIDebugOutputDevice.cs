using System;
using System.Collections.Generic;
using System.Device.Spi;
using System.Diagnostics;
using System.Text;

namespace WaveshareEInkDriver
{
    public class SPIDebugOutputDevice:SpiDevice
    {
        SpiDevice spi;
        
        public override SpiConnectionSettings ConnectionSettings => spi.ConnectionSettings;
        public SPIDebugOutputDevice(SpiDevice spiDevice)
        {
            spi = spiDevice;
        }
        public override void Read(Span<byte> buffer)
        {
            spi.Read(buffer);
            Trace.TraceInformation($"SPI Read[{buffer.Length}]:{BitConverter.ToString(buffer.ToArray())}");
        }

        public override byte ReadByte()
        {
            byte result = spi.ReadByte();
            Trace.TraceInformation($"SPI ReadByte:{result:X}");
            return result;
        }

        public override void TransferFullDuplex(ReadOnlySpan<byte> writeBuffer, Span<byte> readBuffer)
        {
            spi.TransferFullDuplex(writeBuffer, readBuffer);
            Trace.TraceInformation($"SPI Duplex-Write[{writeBuffer.Length}]:{BitConverter.ToString(writeBuffer.ToArray())}");
            Trace.TraceInformation($"SPI Duplex-Read[{readBuffer.Length}]:{BitConverter.ToString(readBuffer.ToArray())}");
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            spi.Write(buffer);
            Trace.TraceInformation($"SPI Write[{buffer.Length}]:{BitConverter.ToString(buffer.ToArray())}");
        }

        public override void WriteByte(byte value)
        {
            spi.WriteByte(value);
            Trace.TraceInformation($"SPI WriteByte:{value:X}");
        }
    }
}
