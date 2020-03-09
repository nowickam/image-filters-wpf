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
        private bool tableChanged, gammaCorrect;


        private List<Tuple<int,int>> errors;

        public MainWindow()
        {
            InitializeComponent();
            kernelFactory = new KernelFactory();
            kernelList = new List<Kernel>();
            checkedCustom = false;
            checkedNew = false;
            settingsUserChange = false;
            tableChanged = false;
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
    
            ComputeBtn.IsEnabled = true;
            SaveFilterBtn.IsEnabled = true;
            ApplyFilterBtn.IsEnabled = true;
            errors.Clear();

            CustomConv.Visibility = Visibility.Collapsed;
            CustomConvBorder.Visibility = Visibility.Collapsed;
            GammaPanel.Visibility = Visibility.Collapsed;

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
                gammaCorrect = false;
            }
            else
            {
                setError(tb, false);
                gammaCorrect = true;
            }
        }

        private void GammaCB_Checked(object sender, RoutedEventArgs e)
        {
            if (editedImg != null)
            {
                GammaPanel.Visibility = Visibility.Visible;
                GammaText.Text = (0.45).ToString();
            }
        }

        private void GammaApply(object sender, RoutedEventArgs e)
        {
            if (editedImg != null && gammaCorrect)
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
                        currentKernel = kernelFactory.Create(((Button)e.Source).Name, (int)ColsSlider.Value, (int)RowsSlider.Value, (int)ColsSlider.Value/2, (int)RowsSlider.Value/2, 0, 0);
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
                if (tb.Text != "-" && tb.Text!="") Keyboard.ClearFocus();
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
                if (settingsUserChange && checkedNew == false && checkedCustom==false && tableChanged == false)
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
            int col=0, row=0, anchorx=0, anchory=0;
            bool larger=false;

            int[] values=setControls(o, ref col, ref row, ref anchorx, ref anchory, ref larger);

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
