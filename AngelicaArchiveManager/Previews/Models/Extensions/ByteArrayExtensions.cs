using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AngelicaArchiveManager.Previews.Models.Extensions
{
    public static class ByteArrayExtensions
    {
        public static ImageSource ToImage(this byte[] bytes)
        {
            using (MemoryStream stream = new MemoryStream(bytes))
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze(); // Important for cross-thread operations
                return image;
            }
        }
    }
} 