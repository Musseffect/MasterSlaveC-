using System;
using System.Collections.Generic;
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

namespace FractalRenderTask
{
    /// <summary>
    /// Логика взаимодействия для taskWindow.xaml
    /// </summary>
    public partial class taskWindow : Window
    {
        public taskWindow(byte[] image,int width,int height)
        {
            InitializeComponent();
            this.image.Source = BitmapSource.Create(width, height, 300, 300, PixelFormats.Rgb24, BitmapPalettes.Gray256, image, (width * PixelFormats.Rgb24.BitsPerPixel + 7) / 8);
        }
    }
}
