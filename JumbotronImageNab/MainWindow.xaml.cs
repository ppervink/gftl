using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;

namespace JumbotronImageNab
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int JumbotronWidth = 64;
        private const int JumbotronHeight = 32;
        private const int PixelSize = 3;

        [StructLayout(LayoutKind.Sequential, Size = PixelSize, Pack = 1)]
        private struct Rgb24
        {
            public byte B;
            public byte G;
            public byte R;

            public override string ToString () => $"0x{R:X2}, 0x{G:X2}, 0x{B:X2}";
        }

        public MainWindow ()
        {
            InitializeComponent();
        }

        private string GetCFormattedJumbotronBitmapData (string bmpPath)
        {
            const int BitsPerPixel = PixelSize * 8;
            const int stride = JumbotronWidth * PixelSize;
            const int size = JumbotronHeight * stride;

            var decoder = new BmpBitmapDecoder(File.OpenRead(bmpPath), BitmapCreateOptions.None, BitmapCacheOption.None);

            var frame = decoder.Frames.Count != 1 ? null : decoder.Frames[0];

            if (frame is null ||
                frame.PixelHeight != JumbotronHeight ||
                frame.PixelWidth != JumbotronWidth ||
                frame.Format.BitsPerPixel != BitsPerPixel)
            {
                throw new FormatException($"Expected a single frame {JumbotronWidth}px by {JumbotronHeight}px bitmap (.bmp) with {BitsPerPixel} bits per pixel.");
            }

            var builder = new StringBuilder("{" + Environment.NewLine + "\t");

            var pData = Marshal.AllocHGlobal(size);
            var colors = EnumeratePixelData(pData).GetEnumerator();

            try
            {
                frame.CopyPixels(Int32Rect.Empty, pData, size, stride);

                var more = true;
                for (int y = 0, offset = 0; y < JumbotronHeight && more; y++)
                {
                    for (int x = 0; x < JumbotronWidth; x++, offset += PixelSize)
                    {
                        more = colors.MoveNext();
                        if (!more)
                            break;

                        var color = colors.Current;
                        builder.Append(color);
                        builder.Append(x == JumbotronWidth - 1 ? "," + Environment.NewLine + "\t" : ", ");
                    }
                }

            }
            finally
            {
                Marshal.FreeHGlobal(pData);
                colors.Dispose();
            }

            var result = builder.ToString().TrimEnd(',', '\t') + "}";
            return result;
        }

        private static IEnumerable<Rgb24> EnumeratePixelData (IntPtr pData)
        {
            for (int x = 0, offset = 0; x < JumbotronWidth; x++)
            {
                for (var y = 0; y < JumbotronHeight; y++, offset += PixelSize)
                {
                    var color = (Rgb24)Marshal.PtrToStructure(IntPtr.Add(pData, offset), typeof(Rgb24));
                    yield return color;
                }
            }
        }

        private void OnSelectFileClick (object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                CheckFileExists = true,
                Filter = "Bitmap Files|*.bmp"
            };

            if (ofd.ShowDialog(this) ?? false)
            {
                try
                {
                    ImageDataText.Text = GetCFormattedJumbotronBitmapData(ofd.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred reading {ofd.FileName}: {ex.Message}");
                    return;
                }

                FileNameTextBox.Text = ofd.FileName;
                BmpImage.Source = new BitmapImage(new Uri(ofd.FileName));
            }

        }
    }
}
