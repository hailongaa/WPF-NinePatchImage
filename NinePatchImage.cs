using SkiaSharp.Views.WPF;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Controls;
using SkiaSharp.Views.Desktop;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;

namespace 你的工程名字
{
    /// <summary>
    /// C#中用于实时展示 Android 9-patch 图片的wpf控件
    /// Android 9-patch 图片在上边和左边定义了拉伸区域，
    /// 本类仅使用上边和左边的信息来处理。
    /// 
    /// 使用方法：
    /// 
    /// ExportToPng(string path)  导出当前渲染好的png
    /// 
    /// </summary>
    internal class NinePatchImage : UserControl, IDisposable
    {
        private Bitmap ninePatchBitmap;
        private List<int> topPatches;
        private List<int> leftPatches;

        private Dictionary<(int, int), SKBitmap> cache = new Dictionary<(int, int), SKBitmap>();

        private const int BYTES_PER_PIXEL = 4;

        private SKElement skCanvas;
        public NinePatchImage()
        {
            // 初始化 SKElement
            skCanvas = new SKElement();
            skCanvas.PaintSurface += OnPaintSurface;
            this.Content = skCanvas;

            this.SizeChanged += (s, e) => skCanvas.InvalidateVisual();
        }
        #region Source DependencyProperty

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(
                nameof(Source),
                typeof(object),
                typeof(NinePatchImage),
                new PropertyMetadata(null, OnSourceChanged));

        /// <summary>
        /// 图片源，可以是 string(path) 或 ImageSource
        /// </summary>
        public object Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NinePatchImage control)
            {
                control.LoadSource(e.NewValue);
            }
        }

        private void LoadSource(object value)
        {
            if (value == null)
            {
                ninePatchBitmap?.Dispose();
                ninePatchBitmap = null;
                cache.Clear();
                skCanvas.InvalidateVisual();
                return;
            }

            if (value is string path)
            {
                Bitmap bitmap = null;

                // 尝试识别是否是资源路径
                if (!System.IO.File.Exists(path))
                {
                    // 使用 Pack URI
                    try
                    {
                        var uri = new Uri(path, UriKind.RelativeOrAbsolute);
                        var streamResource = Application.GetResourceStream(uri)?.Stream;
                        if (streamResource != null)
                        {
                            bitmap = new Bitmap(streamResource);
                        }
                        else
                        {
                            throw new Exception($"Cannot load resource: {path}");
                        }
                    }
                    catch
                    {
                        throw new Exception($"Cannot load image from path: {path}");
                    }
                }
                else
                {
                    bitmap = new Bitmap(path);
                }

                SetImage(bitmap);
            }
            else if (value is ImageSource imgSource)
            {
                Bitmap bitmap = null;
                if (imgSource is BitmapImage bmpImg && bmpImg.UriSource != null)
                {
                    bitmap = new Bitmap(bmpImg.UriSource.LocalPath);
                }
                else
                {
                    // 将 ImageSource 转 Bitmap
                    var width = (int)imgSource.Width;
                    var height = (int)imgSource.Height;
                    var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(new System.Windows.Controls.Image { Source = imgSource });
                    bitmap = BitmapFromSource(rtb);
                }

                SetImage(bitmap);
            }
            else
            {
                throw new ArgumentException("Source must be string path or ImageSource");
            }
        }

        private Bitmap BitmapFromSource(BitmapSource bitmapsource)
        {
            Bitmap bmp;
            using (var ms = new System.IO.MemoryStream())
            {
                BitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapsource));
                encoder.Save(ms);
                bmp = new Bitmap(ms);
            }
            return bmp;
        }

        #endregion
        /// <summary>
        /// 设置 .9.png 图片
        /// </summary>
        /// <param name="bitmap"></param>
        public void SetImage(Bitmap bitmap)
        {
            ninePatchBitmap = bitmap;
            ParseNinePatch();
            skCanvas.InvalidateVisual();
        }
        /// <summary>
        /// 设置 .9.png 图片
        /// </summary>
        /// <param name="path"></param>
        public void SetImage(string path)
        {
            ninePatchBitmap = new Bitmap(path);
            ParseNinePatch();
            skCanvas.InvalidateVisual();

        }
        /// <summary>
        /// 解析 .9.png 的边界伸缩信息
        /// </summary>
        private void ParseNinePatch()
        {
            topPatches = new List<int>();
            leftPatches = new List<int>();

            BitmapData srcData = ninePatchBitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, ninePatchBitmap.Width, ninePatchBitmap.Height),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            byte[] buf = new byte[ninePatchBitmap.Width * ninePatchBitmap.Height * BYTES_PER_PIXEL];
            Marshal.Copy(srcData.Scan0, buf, 0, buf.Length);
            ninePatchBitmap.UnlockBits(srcData);

            // 上边界
            for (int x = 1; x < ninePatchBitmap.Width - 1; x++)
            {
                int index = x * BYTES_PER_PIXEL;
                byte b = buf[index];
                byte g = buf[index + 1];
                byte r = buf[index + 2];
                byte a = buf[index + 3];
                if (r == 0 && g == 0 && b == 0 && a == 255)
                    topPatches.Add(x - 1);
            }

            // 左边界
            for (int y = 1; y < ninePatchBitmap.Height - 1; y++)
            {
                int index = y * ninePatchBitmap.Width * BYTES_PER_PIXEL;
                byte b = buf[index];
                byte g = buf[index + 1];
                byte r = buf[index + 2];
                byte a = buf[index + 3];
                if (r == 0 && g == 0 && b == 0 && a == 255)
                    leftPatches.Add(y - 1);
            }
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            if (ninePatchBitmap == null)
                return;

            int targetWidth = Math.Max(1, (int)this.ActualWidth);
            int targetHeight = Math.Max(1, (int)this.ActualHeight);

            var key = (targetWidth, targetHeight);
            if (!cache.TryGetValue(key, out var skBitmap))
            {
                skBitmap = BuildNinePatchBitmap(targetWidth, targetHeight);
                cache[key] = skBitmap;
            }

            canvas.DrawBitmap(skBitmap, new SKRect(0, 0, e.Info.Width, e.Info.Height));
        }

        private SKBitmap BuildNinePatchBitmap(int targetWidth, int targetHeight)
        {
            // 去掉1px边框
            Bitmap src = new Bitmap(ninePatchBitmap.Width - 2, ninePatchBitmap.Height - 2);
            using (Graphics g = Graphics.FromImage(src))
            {
                g.DrawImage(ninePatchBitmap, new System.Drawing.Rectangle(0, 0, src.Width, src.Height),
                    new System.Drawing.Rectangle(1, 1, src.Width, src.Height), GraphicsUnit.Pixel);
            }

            int sourceWidth = src.Width;
            int sourceHeight = src.Height;

            targetWidth = Math.Max(sourceWidth, targetWidth);
            targetHeight = Math.Max(sourceHeight, targetHeight);

            BitmapData srcData = src.LockBits(new System.Drawing.Rectangle(0, 0, sourceWidth, sourceHeight),
                ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            byte[] srcBuf = new byte[sourceWidth * sourceHeight * BYTES_PER_PIXEL];
            Marshal.Copy(srcData.Scan0, srcBuf, 0, srcBuf.Length);
            src.UnlockBits(srcData);

            int[] xMap = BuildMapping(sourceWidth, targetWidth, topPatches);
            int[] yMap = BuildMapping(sourceHeight, targetHeight, leftPatches);

            SKBitmap skBmp = new SKBitmap(targetWidth, targetHeight, true);
            IntPtr dstPtr = skBmp.GetPixels();

            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    int srcX = xMap[x];
                    int srcY = yMap[y];
                    int srcIndex = (srcY * sourceWidth + srcX) * BYTES_PER_PIXEL;
                    int dstIndex = (y * targetWidth + x) * BYTES_PER_PIXEL;
                    Marshal.Copy(srcBuf, srcIndex, dstPtr + dstIndex, BYTES_PER_PIXEL);
                }
            }

            src.Dispose();
            return skBmp;
        }

        private int[] BuildMapping(int sourceSize, int targetSize, List<int> patches)
        {
            List<int> result = new List<int>(targetSize);
            int diff = targetSize - sourceSize;
            int src = 0;

            while (result.Count < targetSize)
            {
                if (patches.Contains(src))
                {
                    int repeatCount = diff / patches.Count + 1;
                    if (patches.IndexOf(src) < diff % patches.Count)
                        repeatCount++;
                    for (int i = 0; i < repeatCount; i++)
                    {
                        if (result.Count < targetSize)
                            result.Add(src);
                    }
                }
                else
                {
                    result.Add(src);
                }
                src++;
            }
            return result.ToArray();
        }
        /// <summary>
        /// 导出当前拉伸结果为 PNG 文件
        /// </summary>
        /// <param name="path">保存路径</param>
        public void ExportToPng(string path)
        {
            if (ninePatchBitmap == null)
                throw new InvalidOperationException("No image set.");

            int targetWidth = Math.Max(1, (int)this.ActualWidth);
            int targetHeight = Math.Max(1, (int)this.ActualHeight);

            var key = (targetWidth, targetHeight);
            if (!cache.TryGetValue(key, out var skBitmap))
            {
                skBitmap = BuildNinePatchBitmap(targetWidth, targetHeight);
                cache[key] = skBitmap;
            }

            using (var image = SKImage.FromBitmap(skBitmap))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            using (var stream = System.IO.File.OpenWrite(path))
            {
                data.SaveTo(stream);
            }
        }
        public void Dispose()
        {
            foreach (var kv in cache)
                kv.Value.Dispose();
            cache.Clear();
            ninePatchBitmap?.Dispose();
        }
    }
}
