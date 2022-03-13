using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Text;

namespace WaveshareEInkDriver.ImageSharp
{
    //TODO: implement drawing session and drawing surface
    public class DrawingSurface
    {
        public IT8951SPIDevice Device { get; private set; }
        public Image<L8> Image { get; private set; }
        public DrawingSurface(IT8951SPIDevice device)
        {
            Device = device;
            Image = new Image<L8>(device.DeviceInfo.ScreenSize.Width, device.DeviceInfo.ScreenSize.Height);
        }
    }
}
