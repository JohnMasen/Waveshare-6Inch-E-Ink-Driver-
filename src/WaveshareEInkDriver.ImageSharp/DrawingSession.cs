using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Text;

namespace WaveshareEInkDriver.ImageSharp
{
    public abstract class DrawingSession
    {
        protected DrawingSurface surface;
        public DrawingSession(DrawingSurface surface)
        {
            this.surface = surface;
        }
        
        public void Draw()
        {
        }

        public void Refresh()
        {

        }

        public void Refresh(int x,int y,int width,int height)
        {

        }
    }
}
