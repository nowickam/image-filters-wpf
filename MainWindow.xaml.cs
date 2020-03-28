using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace _CG_Filters
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private BitmapSource currentImg = null;
        private WriteableBitmap editedImg = null;
        private string name;
        private int height, width, stride;
        private byte[] pixels;

        private KernelFactory kernelFactory;
        private Kernel currentKernel;
        private List<Kernel> kernelList;

        private static bool checkedCustom, checkedNew;
        private bool settingsUserChange;
        private bool tableChanged, gammaError, ditheringError, medianError, grayscale;


        private List<Tuple<int, int>> errors;

        public MainWindow()
        {
            InitializeComponent();
            kernelFactory = new KernelFactory();
            kernelList = new List<Kernel>();
            checkedCustom = false;
            checkedNew = false;
            settingsUserChange = false;
            tableChanged = false;
            grayscale = false;
            ditheringError = false;
            errors = new List<Tuple<int, int>>();
        }

        public static double Clamp(double x, double min, double max)
        {
            return (x < min) ? min : ((x > max) ? max : x);
        }

        public static double Gauss(double sd, int x, int y)
        {
            double coeff = 1 / (2 * Math.PI * Math.Pow(sd, 2));

            double e = Math.Exp(-((Math.Pow(x, 2) + Math.Pow(y, 2)) / (2 * Math.Pow(sd, 2))));
            return coeff * e;
        }
        private void LoadBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "All (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|" + "JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|" + "PNG (*.png)|*.png";
            if (dialog.ShowDialog() == true)
            {
                tableChanged = false;
                checkedCustom = false;
                checkedNew = false;
                grayscale = false;
                ditheringError = false;
                gammaError = false;
                medianError = false;

                SaveBtn.IsEnabled = true;
                ResetBtn.IsEnabled = true;

                name = dialog.SafeFileName;
                name = name.Substring(0, name.LastIndexOf("."));
                currentImg = new BitmapImage(new Uri(dialog.FileName));
                editedImg = new WriteableBitmap(currentImg);
                height = editedImg.PixelHeight;
                width = editedImg.PixelWidth;
                stride = 4 * width;
                pixels = new byte[height * stride];
                OriginalImg.Source = currentImg;
                EditedImg.Source = editedImg;
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            FileStream stream = new FileStream("../../../" + name + "_edited.png", FileMode.Create);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(editedImg));
            encoder.Save(stream);
            stream.Close();
            MessageBox.Show("Image saved!");
        }

        private void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            checkedCustom = false;
            checkedNew = false;
            tableChanged = false;
            grayscale = false;

            ComputeBtn.IsEnabled = true;
            SaveFilterBtn.IsEnabled = true;
            ApplyFilterBtn.IsEnabled = true;

            errors.Clear();
            ditheringError = false;
            medianError = false;
            gammaError = false;

            CustomConv.Visibility = Visibility.Collapsed;
            CustomConvBorder.Visibility = Visibility.Collapsed;
            GammaBorder.Visibility = Visibility.Collapsed;
            DitheringBorder.Visibility = Visibility.Collapsed;
            MedianBorder.Visibility = Visibility.Collapsed;

            currentImg.CopyPixels(pixels, stride, 0);
            editedImg.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
        }

        private void InvCB_Checked(object sender, RoutedEventArgs e)
        {
            if (editedImg != null)
            {
                editedImg.CopyPixels(pixels, stride, 0);
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    pixels[i] = (byte)(255 - pixels[i]);
                    pixels[i + 1] = (byte)(255 - pixels[i + 1]);
                    pixels[i + 2] = (byte)(255 - pixels[i + 2]);
                }
                editedImg.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
            }
        }

        private void BrightCB_Checked(object sender, RoutedEventArgs e)
        {
            if (editedImg != null)
            {
                double val, inc = 20;
                int i = 0;
                editedImg.CopyPixels(pixels, stride, 0);
                while (i < pixels.Length)
                {
                    //if not alpha
                    if ((i + 1) % 4 != 0)
                    {
                        val = Clamp(pixels[i] + inc, 0, 255);
                        pixels[i] = (byte)(val);
                    }
                    i++;

                }
                editedImg.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
            }
        }

        private void GammaTextChange(object sender, TextChangedEventArgs e)
        {
            TextBox tb = (TextBox)e.Source;
            Decimal res;
            bool parsed = Decimal.TryParse(tb.Text, out res);
            if (tb.Text == null || !parsed)
            {
                setError(tb, true);
                gammaError = true;
            }
            else
            {
                setError(tb, false);
                gammaError = false;
            }
        }

        private void GammaCB_Checked(object sender, RoutedEventArgs e)
        {
            if (editedImg != null)
            {
                GammaBorder.Visibility = Visibility.Visible;
                GammaText.Text = (0.45).ToString();
            }
        }

        private void GammaApply(object sender, RoutedEventArgs e)
        {
            if (editedImg != null && gammaError)
            {
                double gamma = double.Parse(GammaText.Text);
                editedImg.CopyPixels(pixels, stride, 0);
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    pixels[i] = (byte)(255 * Math.Pow((double)pixels[i] / 255, gamma));
                    pixels[i + 1] = (byte)(255 * Math.Pow((double)pixels[i + 1] / 255, gamma));
                    pixels[i + 2] = (byte)(255 * Math.Pow((double)pixels[i + 2] / 255, gamma));
                }
                editedImg.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
            }
        }

        private void ContrCB_Checked(object sender, RoutedEventArgs e)
        {
            if (editedImg != null)
            {
                double val, mult = 1.5;
                int i = 0;
                editedImg.CopyPixels(pixels, stride, 0);
                while (i < pixels.Length)
                {
                    //if not alpha
                    if ((i + 1) % 4 != 0)
                    {
                        val = Clamp((pixels[i] - 127.5) * mult + 127.5, 0, 255);
                        pixels[i] = (byte)(val);
                    }
                    i++;

                }
                editedImg.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
            }
        }

        private void ApplyFilterBtnClick(object sender, RoutedEventArgs e)
        {
            //custom filter || predefined filter changed || custom filter in use
            //then just get the values from the gui and replace the current kernel with it 
            if (checkedCustom == true || tableChanged || checkedNew == true)
            {
                int[] values = ValuesFromTable();
                int[] trimmedValues = new int[values.Length - 1];
                Array.Copy(values, 1, trimmedValues, 0, trimmedValues.Length);
                currentKernel = kernelFactory.Create(trimmedValues, (int)ColsSlider.Value, (int)RowsSlider.Value, (int)AnchorX.Value, (int)AnchorY.Value, int.Parse(Offset.Text), int.Parse(Divisor.Text));
            }

            editedImg.CopyPixels(pixels, stride, 0);
            currentKernel.applyKernel(height, width, pixels, stride);
            editedImg.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
        }

        private void Conv_Checked(object sender, RoutedEventArgs e)
        {
            if (editedImg != null)
            {
                if (((Button)e.Source).Name.ToUpper().Contains("NEW"))
                {
                    checkedNew = true;
                    checkedCustom = false;
                }
                else if (((Button)e.Source).Name.ToUpper().Contains("CUSTOM"))
                {
                    checkedNew = false;
                    checkedCustom = true;
                }
                else
                {
                    checkedNew = false;
                    checkedCustom = false;
                }

                //variation of an existing kernel => created with preexisting constructors
                if (!checkedNew)
                {
                    //custom kernel => get it from the list
                    if (checkedCustom)
                    {
                        string name = ((Button)e.Source).Name;
                        int index = int.Parse(name.Substring(name.LastIndexOf("_") + 1));
                        currentKernel = kernelList.ElementAt(index);
                        Console.WriteLine(currentKernel);
                    }
                    //predefined kernel => get it from the factory
                    else
                    {
                        //values of sliders=3
                        currentKernel = kernelFactory.Create(((Button)e.Source).Name, (int)ColsSlider.Value, (int)RowsSlider.Value, (int)ColsSlider.Value / 2, (int)RowsSlider.Value / 2, 0, 0);
                    }
                    DisplayKernel(currentKernel);
                }
                //new kernel => no creation of kernel yet
                else
                {
                    DisplayKernel((int[])null);
                }
                CustomConv.Visibility = Visibility.Visible;
                CustomConvBorder.Visibility = Visibility.Visible;
            }
        }

        private void DitheringBtn_Click(object sender, RoutedEventArgs e)
        {
            if (editedImg != null)
            {
                DitheringBorder.Visibility = Visibility.Visible;
                AddChannels();
            }
        }

        private void PowerOfTwoCheck(object sender, TextChangedEventArgs e)
        {
            TextBox tb = (TextBox)e.Source;
            int res;
            bool parsed = int.TryParse(tb.Text, out res);
            if (tb.Text == null || !parsed || (res & (res - 1)) != 0)
            {
                setError(tb, true);
                if (tb.Name != "MedianColorNo") ditheringError = true;
                else medianError = true;
            }
            else
            {
                setError(tb, false);
                if (tb.Name != "MedianColorNo") ditheringError = false;
                else medianError = false;
            }
        }

        private void AddChannels()
        {
            ChannelsContainer.Children.Clear();
            int count = 0;
            if (grayscale) count = 1;
            else count = 3;
            for (int i = 0; i < count; i++)
            {
                TextBox txt = new TextBox();
                txt.MinWidth = 20;
                txt.Height = 20;
                txt.TextAlignment = TextAlignment.Center;
                txt.TextChanged += PowerOfTwoCheck;
                Label l = new Label();
                l.FontSize = 10;
                if (grayscale) { l.Content = "B/W"; }
                else { switch (i) { case 0: l.Content = "R"; break; case 1: l.Content = "G"; break; case 2: l.Content = "B"; break; default: break; } }
                ChannelsContainer.Children.Add(l);
                ChannelsContainer.Children.Add(txt);
            }
        }

        private void GrayscaleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (editedImg != null)
            {
                editedImg.CopyPixels(pixels, stride, 0);
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    int val = (pixels[i] + pixels[i + 1] + pixels[i + 2]) / 3;
                    pixels[i] = (byte)(val);
                    pixels[i + 1] = (byte)(val);
                    pixels[i + 2] = (byte)(val);
                }
                editedImg.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
                grayscale = true;
                AddChannels();
            }
        }

        private void findBreakpoint(int levels, int rgb, int start, int end, ref List<double> breakpoints)
        {
            if (levels < 1)
            {
                //Console.WriteLine("STOP: " + levels);
                return;
            }
            else
            {
                //Console.WriteLine("LEVEL: " + levels + "start,stop: "+start+" "+end);
                int sum = 0, count = 0;
                for (int i = rgb; i < pixels.Length; i += 4)
                {
                    if (pixels[i] < end && pixels[i] >= start)
                    {
                        sum += pixels[i];
                        count++;
                    }
                }
                sum = (int)(1.0 * sum / count);
                breakpoints.Add(sum);
                //Console.WriteLine(sum);
                levels--;
                findBreakpoint(levels, rgb, start, sum, ref breakpoints);
                findBreakpoint(levels, rgb, sum, end, ref breakpoints);
            }
        }

        private void ApplyDithering_Click(object sender, RoutedEventArgs e)
        {
            if (editedImg != null && !ditheringError)
            {
                editedImg.CopyPixels(pixels, stride, 0);

                int colors_count = ChannelsContainer.Children.Count / 2;
                List<List<double>> breakpoints = new List<List<double>>();
                int[] dithering_channels = new int[colors_count];
                int levels;
                for (int i = 0; i < colors_count; i++)
                {
                    int res;
                    if (int.TryParse(((TextBox)ChannelsContainer.Children[2 * i + 1]).Text, out res))
                        dithering_channels[i] = res;
                    else return;
                    List<double> l = new List<double>();
                    //depth of the recursion tree
                    levels = (int)(Math.Log(dithering_channels[i], 2));
                    //2-i because of bgra format
                    findBreakpoint(levels, 2 - i, 0, 256, ref l);
                    l.Sort();
                    l.Add(255);
                    breakpoints.Add(l);
                }

                for (int i = 0; i < breakpoints.Count; i++)
                {
                    Console.WriteLine(breakpoints[i].Count - 1);
                    for (int j = 0; j < pixels.Length; j++)
                    {
                        int k = 0;
                        while (pixels[j] > breakpoints[i][k])
                            k++;

                        pixels[j] = (byte)(1.0 * 255 * k / (breakpoints[i].Count - 1));
                    }
                }

                editedImg.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
            }
        }

        private void MedianBtn_Click(object sender, RoutedEventArgs e)
        {
            if (editedImg != null)
            {
                MedianBorder.Visibility = Visibility.Visible;
            }
        }

        private static void swap(ref byte b1, ref byte b2)
        {
            byte temp = b1;
            b1 = b2;
            b2 = temp;
        }

        private static bool inCube(byte[] colors, int i, int rs, int re,  int gs, int ge, int bs, int be)
        {
            if (colors[i] < be && colors[i] >= bs && colors[i + 1] < ge && colors[i + 1] >= gs && colors[i + 2] < re && colors[i + 2] >= rs)
                return true;
            return false;
        }

        private void medianCut(ref byte[] colorCube, int rs, int re, int gs, int ge, int bs, int be, int cuts, ref int over)
        {
            //Console.WriteLine("\nCUTS: " + cuts);
            //Console.WriteLine("DIMS: " + rs + " " + re + " " + gs + " " + ge + " " + bs + " " + be);
            if (cuts == 0 && over==0)
            {
                //color the boxes
                //determine the color - average
                int sumR = 0, sumG = 0, sumB = 0, count = 0,countSum=0;
                for (int i = 0; i < colorCube.Length; i += 4)
                {
                    if (inCube(colorCube, i, rs, re,  gs, ge, bs, be))
                    {
                        sumR += colorCube[i + 2];
                        sumG += colorCube[i + 1];
                        sumB += colorCube[i];
                        count++;
                    }
                    countSum++;
                }

                int averageR = (int)(1.0 * sumR / count);
                int averageG = (int)(1.0 * sumG / count);
                int averageB = (int)(1.0 * sumB / count);

                
                //Console.WriteLine("AVERAGES: "+averageR + " " + averageG + " " + averageB);
                //Console.WriteLine("COUNT COUNTSUM: " + count+" "+countSum);

                for (int i = 0; i < colorCube.Length; i += 4)
                {
                    if (inCube(colorCube, i, rs, re,  gs, ge, bs, be))
                    {
                        colorCube[i + 2] = (byte)averageR;
                        colorCube[i + 1] = (byte)averageG;
                        colorCube[i] = (byte)averageB;
                    }
                }
                //Console.WriteLine("COLOR");
                return;
            }

            int minR = 256, minG = 256, minB = 256, maxR = -1, maxB = -1, maxG = -1, ccount=0;

            //search for min and max value in every channel for a color subcube
            for (int i = 0; i < colorCube.Length; i += 4)
            {
                if (inCube(colorCube, i, rs, re, gs, ge, bs, be))
                {
                    if (colorCube[i] < minB) minB = colorCube[i];
                    else if (colorCube[i] > maxB) maxB = colorCube[i];

                    if (colorCube[i + 1] < minG) minG = colorCube[i + 1];
                    else if (colorCube[i + 1] > maxG) maxG = colorCube[i + 1];

                    if (colorCube[i + 2] < minR) minR = colorCube[i + 2];
                    else if (colorCube[i + 2] > maxR) maxR = colorCube[i + 2];

                    ccount++;
                }
            }
            //calculate the range
            int distR = maxR - minR, distB = maxB - minB, distG = maxG - minG;
            //Console.WriteLine("DIST: " + distR + " " + distG + " " + distB + " ");

            //determine max range
            int medianChannel;
            if (distR >= distG && distR >= distB) medianChannel = 2;
            else if (distG >= distR && distG >= distB) medianChannel = 1;
            else medianChannel = 0;

            //help sort to get the median
            List<byte> sortedPixels = new List<byte>();
            for(int i = 0; i < colorCube.Length; i+=4)
            {
                if (inCube(colorCube, i, rs, re, gs, ge, bs, be)) sortedPixels.Add(colorCube[i+medianChannel]);
            }
            sortedPixels.Sort();

            //determine the splitting value of the color and alternative colors
            int medianColor;
            if (sortedPixels.Count > 0) medianColor = sortedPixels[sortedPixels.Count / 2];
            else medianColor = 0;

            if(cuts>0)cuts--;
            if(cuts==0 && over>0)over--;
            //System.Console.WriteLine("OVER: " + over);

            switch (medianChannel)
            {
                case 0:
                    medianCut(ref colorCube, rs, re, gs, ge, bs, medianColor, cuts, ref over);
                    medianCut(ref colorCube, rs, re, gs, ge, medianColor, be, cuts, ref over);
                    break;
                case 1:
                    medianCut(ref colorCube, rs, re, gs, medianColor, bs, be, cuts, ref over);
                    medianCut(ref colorCube, rs, re, bs, be, medianColor, ge, cuts, ref over);
                    break;
                case 2:
                    medianCut(ref colorCube, rs, medianColor, gs, ge, bs, be, cuts, ref over);
                    medianCut(ref colorCube, medianColor, re, gs, ge, bs, be, cuts, ref over);
                    break;
                default:
                    return;
            }
        }

        private void ApplyMedian_Click(object sender, RoutedEventArgs e)
        {
            if (editedImg != null && !medianError)
            {
                editedImg.CopyPixels(pixels, stride, 0);

                int colors = int.Parse(MedianColorNo.Text);
                int cuts = (int)Math.Log(colors, 2);
                int over = (int)(colors - Math.Pow(2, cuts)+1);

                byte[] colorCube = new byte[pixels.Length];
                Array.Copy(pixels, 0, colorCube, 0, pixels.Length);
                medianCut(ref colorCube, 0, 255, 0, 255, 0, 255, cuts, ref over);

                for (int i = 0; i < pixels.Length; i += 4)
                {
                    pixels[i] = colorCube[i];
                    pixels[i + 1] = colorCube[i + 1];
                    pixels[i + 2] = colorCube[i + 2];
                }

                editedImg.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
            }
        }

        private void TableChange(object sender, TextChangedEventArgs e)
        {
            TextBox tb = (TextBox)e.Source;
            Decimal res;
            bool parsed = Decimal.TryParse(tb.Text, out res);
            int row = Grid.GetRow((TextBox)sender);
            int col = Grid.GetColumn((TextBox)sender);
            Tuple<int, int> t = new Tuple<int, int>(row, col);
            if (tb.Text != null && parsed)
            {
                errors.Remove(t);

                setError(tb, false);
                tableChanged = true;
            }
            else
            {
                if (!errors.Contains(t)) errors.Add(t);
                setError(tb, true);
            }
        }

        private int[] ValuesFromTable()
        {
            int[] values = new int[KernelTable.Children.Count + 1];
            values[0] = KernelTable.ColumnDefinitions.Count;
            int i = 1;
            foreach (TextBox txt in KernelTable.Children)
            {
                values[i] = int.Parse(txt.Text);
                i++;
            }
            return values;
        }

        private void ControlsChanged(object sender, RoutedEventArgs e)
        {
            //changing the predefined kernel accroding to the kernel algorithm
            if (settingsUserChange && checkedNew == false && checkedCustom == false && tableChanged == false)
            {
                kernelFactory.Refactor(ref currentKernel, (int)ColsSlider.Value, (int)RowsSlider.Value, (int)AnchorX.Value, (int)AnchorY.Value, int.Parse(Offset.Text), int.Parse(Divisor.Text));
                DisplayKernel(currentKernel);
            }
            //changing custom kernel
            else if ((checkedCustom == true || checkedNew == true || tableChanged == true) && settingsUserChange)
            {
                DisplayKernel(ValuesFromTable());
            }
        }


        private void setError(TextBox tb, bool set)
        {
            if (set)
            {
                int res;
                bool parsed = int.TryParse(tb.Text, out res);
                if (tb.Text != "-" && tb.Text != "" && !parsed) Keyboard.ClearFocus();
                tb.BorderThickness = new Thickness(2);
                tb.BorderBrush = Brushes.Red;

                if (errors.Count > 0)
                {
                    ComputeBtn.IsEnabled = false;
                    SaveFilterBtn.IsEnabled = false;
                    ApplyFilterBtn.IsEnabled = false;
                }
            }
            else
            {
                tb.Focus();
                tb.BorderThickness = new Thickness(1);
                tb.BorderBrush = Brushes.Gray;

                if (errors.Count == 0)
                {
                    ComputeBtn.IsEnabled = true;
                    SaveFilterBtn.IsEnabled = true;
                    ApplyFilterBtn.IsEnabled = true;
                }
            }
        }



        //refactor the kernels, the custom take these values later
        private void DivOffChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tb = (TextBox)e.Source;
            Decimal res;
            if (tb.Text != null && Decimal.TryParse(tb.Text, out res))
            {
                setError(tb, false);
                if (settingsUserChange && checkedNew == false && checkedCustom == false && tableChanged == false)
                {
                    kernelFactory.Refactor(ref currentKernel, (int)ColsSlider.Value, (int)RowsSlider.Value, (int)AnchorX.Value, (int)AnchorY.Value, int.Parse(Offset.Text), int.Parse(Divisor.Text));
                }
            }
            else
            {
                setError(tb, true);
            }
        }

        private void ComputeClick(object sender, RoutedEventArgs e)
        {
            setError(Divisor, false);
            int[] values = ValuesFromTable();
            Divisor.Text = (values.Sum() - values[0]).ToString();
        }

        private void ResetOffClick(object sender, RoutedEventArgs e)
        {
            setError(Offset, false);
            Offset.Text = "0";
        }

        private void SaveFilterBtnClick(object sender, RoutedEventArgs e)
        {
            Button checkBox = new Button();
            checkBox.Name = "Custom__" + kernelList.Count.ToString();
            checkBox.Content = checkBox.Name;
            checkBox.Click += Conv_Checked;
            int[] values = ValuesFromTable();
            int[] trimmedValues = new int[values.Length - 1];
            Array.Copy(values, 1, trimmedValues, 0, trimmedValues.Length);
            Kernel kernel = kernelFactory.Create(trimmedValues, (int)ColsSlider.Value, (int)RowsSlider.Value, (int)AnchorX.Value, (int)AnchorY.Value, int.Parse(Offset.Text), int.Parse(Divisor.Text));
            kernelList.Add(kernel);
            ConvFilters.Children.Add(checkBox);
        }

        private int[] setControls(object o, ref int col, ref int row, ref int anchorx, ref int anchory, ref bool larger)
        {
            int[] values;
            if (o is Kernel kernel)
            {
                col = (int)kernel.SizeX;
                row = (int)kernel.SizeY;
                anchorx = kernel.AnchorX;
                anchory = kernel.AnchorY;
                values = kernel.Values;
                Offset.Text = kernel.Offset.ToString();
                Divisor.Text = kernel.Divisor.ToString();
                ColsSlider.Value = col;
                RowsSlider.Value = row;
            }
            else
            {
                col = (int)ColsSlider.Value;
                row = (int)RowsSlider.Value;
                anchory = (int)AnchorY.Value;
                anchorx = (int)AnchorX.Value;
                values = (int[])o;
                Offset.Text = "0";
                Divisor.Text = "1";

                if (values != null && (values[0] > col || ((values.Length - 1) / values[0]) > row)) larger = false;
                else larger = true;
            }

            settingsUserChange = false;
            AnchorX.Value = anchorx;
            AnchorY.Value = anchory;
            AnchorX.Minimum = AnchorY.Minimum = 0;
            AnchorX.Maximum = col - 1;
            AnchorY.Maximum = row - 1;
            settingsUserChange = true;

            return values;
        }

        private void DisplayKernel(object o)
        {
            int col = 0, row = 0, anchorx = 0, anchory = 0;
            bool larger = false;

            int[] values = setControls(o, ref col, ref row, ref anchorx, ref anchory, ref larger);

            KernelTable.Children.Clear();
            KernelTable.RowDefinitions.Clear();
            KernelTable.ColumnDefinitions.Clear();

            for (int i = 0; i < row; i++)
            {
                KernelTable.RowDefinitions.Add(new RowDefinition());
            }
            for (int i = 0; i < col; i++)
            {
                KernelTable.ColumnDefinitions.Add(new ColumnDefinition());
            }
            int k = 0, l = 0;
            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    TextBox txt = new TextBox();
                    if (i == anchory && j == anchorx)
                    {
                        txt.BorderThickness = new Thickness(2);
                    }
                    if (o is Kernel)
                    {
                        txt.Text = values[i * col + j].ToString();
                    }
                    else
                    {
                        if (values == null || (values[0] != 0 && (values[0] <= j || i >= ((values.Length - 1) / values[0])))) txt.Text = "0";
                        else
                        {
                            txt.Text = (values[k * values[0] + l + 1]).ToString();
                            if ((++l % values[0] == 0) && larger) { k++; l = 0; }

                        }
                    }
                    txt.Width = 20;
                    txt.Height = 20;
                    txt.TextAlignment = TextAlignment.Center;
                    txt.Margin = new Thickness(5);
                    txt.TextChanged += TableChange;
                    Grid.SetRow(txt, i);
                    Grid.SetColumn(txt, j);
                    KernelTable.Children.Add(txt);
                }
                if (!(o is Kernel))
                {
                    if (!larger)
                    {
                        k++;
                        l = 0;
                    }
                }
            }
        }

    }
}
