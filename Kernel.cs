using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _CG_Filters
{
    public abstract class Kernel
    {
        protected int[] values;
        private int sizex, sizey,sum,anchorx,anchory,offset,divisor;

        public Kernel(int sizex, int sizey,int anchorx,int anchory,int offset)
        {
            SizeX = sizex;
            SizeY = sizey;
            AnchorX = anchorx;
            AnchorY = anchory;
            Offset = offset;
        }

        public int[] Values
        {
            get { return values; }
        }

        public int Sum
        {
            get { return sum; }
            set { sum = value; }
        }

        public int Divisor
        {
            get { return divisor; }
            set { divisor = value; }
        }

        public int Offset
        {
            get { return offset; }
            set { offset = value; }
        }

        public int SizeX
        {
            get { return sizex; }
            set { sizex = value; }
        }

        public int SizeY
        {
            get { return sizey; }
            set { sizey = value; }
        }

        public int AnchorX
        {
            get { return anchorx; }
            set { anchorx=value; }
        }

        public int AnchorY
        {
            get { return anchory; }
            set { anchory = value; }
        }

        public abstract Kernel Refactor(int sizex, int sizey, int anchorx, int anchory, int offset, int divisor);


        private static double PixelValue(int height, int stride, byte[] pixels, int[] kernel, int kernelStride, int i, int k, int j, int l, int rgb, int anchorx, int anchory)
        {
            int a = i + k;
            int b = j + 4 * l + rgb;
            a=(int)MainWindow.Clamp(a, 0, height - 1);
            b =(int)MainWindow.Clamp(b, rgb, stride - 4 + rgb);
            return pixels[(a * stride) + b] * kernel[(k + anchory) * kernelStride + l + anchorx];

        }

        public void applyKernel(int height, int width, byte[] pixels, int stride)
        {
            byte[] pixelsTemp = new byte[pixels.Length];
            pixels.CopyTo(pixelsTemp, 0);
            double valR, valG, valB;
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < stride; j += 4)
                {
                    valR = 0;
                    valG = 0;
                    valB = 0;
                    for (int k = 0 - AnchorY; k < SizeY - AnchorY; k++)
                    {
                        for (int l = 0 - AnchorX; l < SizeX - AnchorX; l++)
                        {
                            valB += PixelValue(height, stride, pixelsTemp, Values, SizeX, i, k, j, l, 0,AnchorX,AnchorY);
                            valG += PixelValue(height, stride, pixelsTemp, Values, SizeX, i, k, j, l, 1, AnchorX, AnchorY);
                            valR += PixelValue(height, stride, pixelsTemp, Values, SizeX, i, k, j, l, 2, AnchorX, AnchorY);

                        }
                    }

                    pixels[i * stride + j] = (byte)(Offset + MainWindow.Clamp(valB / Divisor, 0, 255));
                    pixels[i * stride + j + 1] = (byte)(Offset + MainWindow.Clamp(valG / Divisor, 0, 255));
                    pixels[i * stride + j + 2] = (byte)(Offset + MainWindow.Clamp(valR / Divisor, 0, 255));
                }
            }
        }
    }

    public class BlurKernel : Kernel
    {
        public BlurKernel(int sizex, int sizey, int anchorx, int anchory, int offset, int divisor) : base(sizex,sizey,anchorx,anchory,offset)
        {
            Sum = 0;
            values = new int[sizex * sizey];

            for(int i = 0; i < sizex * sizey; i++)
            {
                values[i] = 1;
                Sum += values[i];
            }
            if (divisor != Sum && divisor != 0) Divisor = divisor;
            else Divisor = Sum;
        }

        public override Kernel Refactor(int sizex, int sizey, int anchorx, int anchory, int offset, int divisor)
        {
            return new BlurKernel(sizex, sizey, anchorx, anchory,offset,divisor);
        }
    }

    public class GaussKernel : Kernel
    {
        public GaussKernel(int sizex, int sizey, int anchorx, int anchory, int offset, int divisor) : base(sizex, sizey, anchorx, anchory, offset)
        {
            Sum = 0;
            values = new int[sizex * sizey];

            for (int i = 0; i < sizey ; i++)
            {
                for(int j = 0; j < sizex; j++)
                {
                    values[i * sizex + j] = (int)(MainWindow.Gauss(1, i - sizey/2, j - sizex/2)*sizex*sizey*5);
                    Sum += values[i * sizex + j];
                }
            }
            if (divisor != Sum && divisor != 0) Divisor = divisor;
            else Divisor = Sum;
        }

        public override Kernel Refactor(int sizex, int sizey, int anchorx, int anchory, int offset, int divisor)
        {
            return new GaussKernel(sizex, sizey, anchorx, anchory, offset, divisor);
        }
    }

    public class SharpenKernel : Kernel
    {
        public SharpenKernel(int sizex, int sizey, int anchorx, int anchory, int offset, int divisor) : base(sizex, sizey, anchorx, anchory, offset)
        {
            Sum = 1;
            values = new int[sizex * sizey];
            for (int i = 0; i < sizey; i++)
            {
                for (int j = 0; j < sizex; j++)
                {
                    values[i * sizex + j] = -1;
                    if (i == sizey/2 && j == sizex/2) values[i * sizex + j] = (sizex*sizey);
                }
            }
            if (divisor != Sum && divisor != 0) Divisor = divisor;
            else Divisor = Sum;
        }

        public override Kernel Refactor(int sizex, int sizey, int anchorx, int anchory, int offset, int divisor)
        {
            return new SharpenKernel(sizex, sizey, anchorx, anchory, offset, divisor);
        }
    }

    //hardcoded laplacian detection
    public class EdgeKernel : Kernel
    {
        public EdgeKernel(int sizex, int sizey, int anchorx, int anchory, int offset, int divisor) : base(sizex, sizey, anchorx, anchory, offset)
        {
            Sum = 1;
            values = new int[sizex * sizey];
            for (int i = 0; i < sizey; i++)
            {
                for (int j = 0; j < sizex; j++)
                {
                    values[i * sizex + j] = -1;
                    if (i == 1 && j == 1) values[i * sizex + j] = 8;
                    if (i >= 3 || j >= 3) values[i * sizex + j] = 0;
                  
                }
            }
            if (divisor != Sum && divisor != 0) Divisor = divisor;
            else Divisor = Sum;
        }

        public override Kernel Refactor(int sizex, int sizey, int anchorx, int anchory, int offset, int divisor)
        {
            return new EdgeKernel(sizex, sizey, anchorx, anchory, offset, divisor);
        }
    }

    public class EmbossKernel : Kernel
    {
        public EmbossKernel(int sizex, int sizey, int anchorx, int anchory, int offset, int divisor) : base(sizex, sizey, anchorx, anchory, offset)
        {
            Sum = 0;
            int k = 0;
            values = new int[sizex * sizey];
            int[] kernel = new int[] { -1, 0, 1, -1, 1, 1, -1, 0, 1 };
            for (int i = 0; i < sizey; i++)
            {
                for (int j = 0; j < sizex; j++)
                {
                    if (i >= 3 || j >= 3) values[i * sizex + j] = 0;
                    else
                    {
                        values[i * sizex + j] = kernel[k];
                        k++;
                    }
                    Sum += values[i * sizex + j];
                }
            }
            if (divisor != Sum && divisor != 0) Divisor = divisor;
            else Divisor = Sum;
        }

        public override Kernel Refactor(int sizex, int sizey, int anchorx, int anchory, int offset, int divisor)
        {
            return new EmbossKernel(sizex, sizey, anchorx, anchory, offset, divisor);
        }
    }

    public class CustomKernel : Kernel
    {
        public CustomKernel(int[] val, int sizex, int sizey, int anchorx, int anchory, int offset, int divisor) : base(sizex, sizey, anchorx, anchory, offset)
        {
            Sum = 1;
            values = new int[sizex * sizey];

            for (int i = 0; i < sizex * sizey; i++)
            {
                values[i] = val[i];
            }
            if (divisor != Sum && divisor != 0) Divisor = divisor;
            else Divisor = Sum;
        }
        public override Kernel Refactor(int sizex, int sizey, int anchorx, int anchory, int offset, int divisor)
        {
            throw new NotImplementedException();
        }
    }

    public class KernelFactory
    {
        public Kernel Create(object o, int sizex, int sizey,int anchorx,int anchory,int offset, int divisor)
        {
            if (o is string type)
            {
                if (type.ToUpper().Contains("BLUR"))
                {
                    return new BlurKernel(sizex, sizey, anchorx, anchory, offset, divisor);
                }
                else if (type.ToUpper().Contains("GAUSS"))
                {
                    return new GaussKernel(sizex, sizey, anchorx, anchory, offset, divisor);
                }
                else if (type.ToUpper().Contains("SHARP"))
                {
                    return new SharpenKernel(sizex, sizey, anchorx, anchory, offset, divisor);
                }
                else if (type.ToUpper().Contains("EDGE"))
                {
                    return new EdgeKernel(sizex, sizey, anchorx, anchory, offset, divisor);
                }
                else if (type.ToUpper().Contains("EMBOSS"))
                {
                    return new EmbossKernel(sizex, sizey, anchorx, anchory, offset, divisor);
                }
                else
                {
                    return null;
                }
            }
            else if (o is int[] values)
            {
                return new CustomKernel(values, sizex, sizey, anchorx, anchory, offset, divisor);
            }
            else
            {
                return null;
            }
        }

        //refactor pre-existing, non-custom kernel
        public void Refactor( ref Kernel kernel, int sizex, int sizey, int anchorx, int anchory, int offset, int divisor)
        {
            kernel= kernel.Refactor(sizex, sizey, anchorx, anchory, offset,divisor);
        }
    }
}
