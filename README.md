# Waveshare-6Inch-E-Ink-Driver-
C# driver for waveshare 6inch e-ink paper driver

This software is created with netstandard 2.1, ideally it should run on any boards with SPI device. 

## **Important Notice**
**Make sure set the VCOM correctly before calling the draw API. setting VCOM incorrectly may brake your hardware.**

**VCOM is printed on the flat cable of the screen device**

### Supported hardware
Waveshare 6inch e-Paper HAT (https://www.waveshare.net/wiki/6inch_e-Paper_HAT)

### Supported Boards and OS
Raspbian on Raspberrypi 4B (Tested)

Ideally this library supports any boards with SPI and GPIO pins 

which is abel to run dotnet application(linux or windows). 

### Wiring
Attach the HAT to your raspberry pi and connect it to your screen.

you can find the wiring sample at https://www.waveshare.net/wiki/6inch_e-Paper_HAT

## Sample Code
Add **WaveshareEInkDriver.ImageSharp** package to your project


```csharp
//SPI settings for respberry pi 4B
SpiConnectionSettings settings = new SpiConnectionSettings(0, 0);
settings.ClockFrequency = 12000000; //suggested 12MHZ in datasheet
settings.Mode = SpiMode.Mode0;
settings.ChipSelectLineActiveState = PinValue.Low;
settings.DataFlow = DataFlow.MsbFirst;
SpiDevice spi = SpiDevice.Create(settings);

//init the device class with HAT wiring
var device = new IT8951SPIDevice(new IT8951SPIDeviceIO(spi, readyPin: 24, resetPin: 17));

//init device hardware
device.Init();
device.SetVCom(-1.91f);//change this to your device VCOM value

//Load image
var img = Image.Load<L8>("myimage.jpg");

//display image to E-Ink screen
device.DrawImage(img);

//done
```

Partial update

```csharp
//display image to E-Ink screen
//draw the image block (0,0,96,96) to screen position (0,0)
device.DrawImagePartial(image, new Rectangle(0, 0, 96, 96), new Point(0, 0));

```

## Tricks to save your time
1. Do not mixed up 2A and GC16 mode, for example: drawing images in the following sequence may corrupt device display buffer.  
GC16->2A(__*buffer corrupt*__)->GC16->2A    
<br>
 Always use ```device.ClearScreen(INIT)```  to clear the screen and display buffer before you switching to 2A or GC16 mode. 

2. Ghosting: Clear the screen with ```device.ClearScreen()``` before drawing next frame can prevent the ghosting, however it may take extra 200~400ms.

3. Use ```device.ClearScreen(INIT)``` before shutdown. it is better to clear the device screen before long term storage.

4. Dither before partial update. dither can greatly improve the visual on grey scale screen if the original image is a true color(RGB24) photo.   
```DrawImage()``` ditheres on the original image by default to improve the output quality.  You can turn off this behavior if you want to reuse the image object.
```DrawImagePartial()``` does not dither the original image and there's no parameter to turn on this behavior. the ConsoleSample includes the sample code how to dither before the partial update.