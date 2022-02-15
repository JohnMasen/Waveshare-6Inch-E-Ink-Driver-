using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WaveshareEInkDriver
{
    public class IT8951SPIDeviceIO
    {
        public enum DeviceCommand : ushort
        {
            SYSN_RUN = 0x01,
            SLEEP = 0x03,
            REG_RD = 0x10,
            REG_WR = 0x11,
            MEM_BST_RD_T = 0x12,
            MEM_BST_RD_S = 0x13,
            MEM_BST_WR = 0x14,
            MEM_BST_END = 0x15,
            LD_IMG = 0x20,
            LD_IMG_AREA = 0x21,
            LD_IMG_END = 0x22,
            GET_DEV_INFO = 0x0302,
            DPY_AREA = 0x34,
            DPY_BUF_AREA=0x37,
            PWD_ON_OFF_SEQ = 0x38,
            VCOM_RW = 0x39,
            TEMPERATURE_RW = 0x40   
        }
        private SpiDevice spi;
        private  byte[] cmdBuffer = { 0x60, 0x00,0x00,0x00 };
        private  byte[] sendDataBuffer = { 0x00, 0x00, 0x00, 0x00 };
        public int MaxDumpDataSize { get; set; } = 100;

        private readonly int readyPin, resetPin;
        GpioController gpio;
        /// <summary>
        /// Global wait time out setting, default is 1 second
        /// </summary>
        public TimeSpan WaitTimeOut { get; set; } = TimeSpan.FromSeconds(1);
        private object syncRoot = new object();
        public IT8951SPIDeviceIO(SpiDevice spiDevice,int readyPin, int resetPin)
        {
            spi = spiDevice;
            this.readyPin = readyPin;
            this.resetPin = resetPin;
            gpio = new GpioController();
            gpio.OpenPin(this.readyPin, PinMode.Input);
            gpio.OpenPin(resetPin, PinMode.Output,PinValue.High);
        }

        /// <summary>
        /// Wait for the busy pin(HRDY) to idle (Low=idle,High=busy)
        /// </summary>
        /// <param name="timeout">Wait time out, use This.WaitTimeOut if parameter is null</param>
        public void WaitReady(TimeSpan? timeout=null)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (gpio.Read(readyPin)!=PinValue.High)
            {
                if (sw.Elapsed>timeout)
                {
                    throw new InvalidOperationException("HRDY wait time out");
                }
                Thread.Sleep(100);
            }
            sw.Stop();
            Trace.TraceInformation($"Wait ready in {sw.ElapsedMilliseconds}ms");
        }

        public void Reset()
        {
            gpio.Write(resetPin, PinValue.High);
            Thread.Sleep(200);
            gpio.Write(resetPin, PinValue.Low);
            Thread.Sleep(10);
            gpio.Write(resetPin, PinValue.High);
            Thread.Sleep(200);
            WaitReady();
        }


        public void SendCommand(DeviceCommand cmd,params ushort[] args)
        {
            
            lock (syncRoot)
            {
                BinaryPrimitives.WriteUInt16BigEndian(cmdBuffer.AsSpan(2), (ushort)cmd);
                WaitReady();
                spi.Write(cmdBuffer);
                Trace.TraceInformation(dumpBuffer("SPI WRITE", cmdBuffer));
                foreach (var item in args)
                {
                    SendData(item);
                }
            }
        }

        public void SendData(ushort data)
        {
            lock (syncRoot)
            {
                BinaryPrimitives.WriteUInt16BigEndian(sendDataBuffer.AsSpan(2), data);
                WaitReady();
                spi.Write(sendDataBuffer);
                Trace.TraceInformation(dumpBuffer("SPI WRITE", sendDataBuffer));
            }
        }

        public void SendData(Span<byte> data,bool swapByteOrder=false,int sendBufferSize=2048)
        {
            if (data.Length==0)
            {
                throw new ArgumentOutOfRangeException("data size must not be 0");
            }
            if (data.Length%2!=0)
            {
                throw new ArgumentException("data size should be multiples of 2");
            }
            if (sendBufferSize<4 ||sendBufferSize>2048)
            {
                throw new ArgumentOutOfRangeException(nameof(sendBufferSize), "sendBufferSize must between 4 and 2048");
            }
            if (sendBufferSize%2!=0)
            {
                throw new ArgumentException(nameof(sendBufferSize), "sendBufferSize must be multiples of 2");
            }
            Span<byte> sendBuffer = stackalloc byte[sendBufferSize];
            sendBuffer.Slice(0, 2).Fill(0);
            int index = 0;
            while (index<data.Length)
            {
                int sendSize = Math.Min(sendBufferSize-2, data.Length - index);//buffer size includes 2 bytes (0x00 0x00) prebeam
                data.Slice(index, sendSize).CopyTo(sendBuffer.Slice(2));
                if (swapByteOrder)
                {
                    swapDataByteOrder(sendBuffer, 2..);
                }
                WaitReady();
                spi.Write(sendBuffer.Slice(0,sendSize + 2));
                index += sendSize;
            }
            Trace.TraceInformation($"Batch data sent, size={data.Length}");
        }

        private void swapDataByteOrder(Span<byte> data,Range range)
        {
            var r = range.GetOffsetAndLength(data.Length);
            for (int i = r.Offset; i < r.Length; i+=2)
            {
                data.Slice(i, 2).Reverse();
            }
        }

        public void ReadData(Span<byte> resultBuffer)
        {

            int size = resultBuffer.Length + 4;
            Span<byte> sendBuffer = stackalloc byte[size];
            Span<byte> receiveBuffer = stackalloc byte[size];
            sendBuffer[0] = 0x10;
            WaitReady();
            spi.TransferFullDuplex(sendBuffer, receiveBuffer);
            Trace.TraceInformation(dumpBuffer("SPI Duplex - Write", sendBuffer));
            Trace.TraceInformation(dumpBuffer("SPI Duplex - Read", receiveBuffer));
            receiveBuffer.Slice(4).CopyTo(resultBuffer);
        }

        public ushort ReadData()
        {
            Span<byte> buffer = stackalloc byte[2];
            ReadData(buffer);
            return BinaryPrimitives.ReadUInt16BigEndian(buffer);
        }
        private string dumpBuffer(string prefix, ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length <= MaxDumpDataSize)
            {
                return $"{prefix}[{buffer.Length}]:{BitConverter.ToString(buffer.ToArray())}";
            }
            else
            {
                return $"{prefix}[{buffer.Length}]:{BitConverter.ToString(buffer.Slice(0, 10).ToArray())} ...";
            }
        }
    }
}
