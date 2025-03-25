using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace AngelicaArchiveManager
{
    public static class StaticConverter
    {
        static StaticConverter()
        {
            // Register CodePages encoding provider to support GB2312 (code page 936)
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        
        public static int ToInt32(this string value)
        {
            int.TryParse(value, out int res);
            return res;
        }

        public static string ToGBK(this byte[] value) => Encoding.GetEncoding(936).GetString(value)
                .Split(new string[] { "\0" }, StringSplitOptions.RemoveEmptyEntries)[0];
        public static string RemoveFirstSeparator(this string value)
        {
            if (value.Length > 2 && value.StartsWith("\\"))
                value = value.Remove(0, 1);
            return value;
        }

        public static string RemoveFirst(this string value, string pattern)
        {
            int index = value.IndexOf(pattern);
            if (index > -1)
                value = value.Remove(index, pattern.Length);
            return value;
        }

        public static byte[] FromGBK(this string value) => Encoding.GetEncoding(936).GetBytes(value + '\0');

        public static BitmapImage ToImage(this byte[] array)
        {
            using (var ms = new MemoryStream(array))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = ms;
                image.EndInit();
                return image;
            }
        }
    }

    public class StringFormatToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string)
            {
                if (value.ToString().Contains(",") || value.ToString().Contains("."))
                    return float.TryParse(value.ToString(), out float number) ? number : 0;
                else
                    return int.TryParse(value.ToString(), out int number) ? number : 0;
            }
            else
            {
                return value;
            }
        }
    }
}
