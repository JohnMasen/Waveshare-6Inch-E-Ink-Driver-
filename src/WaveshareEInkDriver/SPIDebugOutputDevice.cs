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
        public int MaxDumpDataSize { get; set; } = 100;
        public SPIDebugOutputDevice(SpiDevice spiDevice)
        {
            spi = spiDevice;
        }
        public override void Read(Span<byte> buffer)
        {
            spi.Read(buffer);
            Trace.TraceInformation(dumpBuffer("SPI READ",buffer));
        }

        private string dumpBuffer(string prefix, ReadOnlySpan<byte> buffer)
        {
            if ( buffer.Length<=MaxDumpDataSize)
            {
                return $"{prefix}[{buffer.Length}]:{BitConverter.ToString(buffer.ToArray())}";
            }
            else
            {
                return $"{prefix}[{buffer.Length}]:{BitConverter.ToString(buffer.Slice(0,10).ToArray())} ...";
            }
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
            Trace.TraceInformation(dumpBuffer("SPI Duplex - Write", writeBuffer));
            Trace.TraceInformation(dumpBuffer("SPI Duplex - Read", readBuffer));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            spi.Write(buffer);
            Trace.TraceInformation(dumpBuffer("SPI WRITE", buffer));
        }

        public override void WriteByte(byte value)
        {
            spi.WriteByte(value);
            Trace.TraceInformation($"SPI WriteByte:{value:X}");
        }
    }
}
