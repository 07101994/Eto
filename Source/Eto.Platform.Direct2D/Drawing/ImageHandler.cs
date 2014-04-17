using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eto.Drawing;
using s = SharpDX;
using sd = SharpDX.Direct2D1;
using sw = SharpDX.WIC;
using System.IO;
using System.Diagnostics;
#if WINFORMS
using Eto.Platform.Windows.Drawing;
#endif

namespace Eto.Platform.Direct2D.Drawing
{
	public interface ID2DBitmapHandler
	{
		sd.Bitmap GetBitmap(sd.RenderTarget target);
	}

	public class ImageHandler<TWidget> : WidgetHandler<sw.Bitmap, TWidget>, IImage, ID2DBitmapHandler
#if WINFORMS		
		, IWindowsImageSource
#endif
		where TWidget: Image
    {
		sd.Bitmap targetBitmap;
		public sw.Bitmap[] Frames { get; private set; }
		public sd.Bitmap GetBitmap(sd.RenderTarget target)
		{
			if (targetBitmap == null || !ReferenceEquals(targetBitmap.Tag, target))
			{
				targetBitmap = CreateDrawableBitmap(target);
			}
			return targetBitmap;
		}

		protected virtual sd.Bitmap CreateDrawableBitmap(sd.RenderTarget target)
		{
			return sd.Bitmap.FromWicBitmap(target, Control);
		}

		void Initialize(s.WIC.BitmapDecoder decoder)
		{
			Frames = Enumerable.Range(0, decoder.FrameCount).Select(r => decoder.GetFrame(r).ToBitmap()).ToArray();
			// find largest frame (e.g. when loading icons)
			Control = Frames.Aggregate((x,y) => x.Size.Width > y.Size.Width || x.Size.Height > y.Size.Height ? x : y);
		}

		public void Create(string filename)
        {
			using (var decoder = new s.WIC.BitmapDecoder(
				SDFactory.WicImagingFactory,
				filename,
				s.WIC.DecodeOptions.CacheOnDemand))
				Initialize(decoder);
		}

        public void Create(System.IO.Stream stream)
        {
			using (var decoder = new s.WIC.BitmapDecoder(
				SDFactory.WicImagingFactory,
				stream,
				s.WIC.DecodeOptions.CacheOnDemand))
				Initialize(decoder);
		}

        public void Create(int width, int height, PixelFormat pixelFormat)
        {
			Control = new sw.Bitmap(SDFactory.WicImagingFactory, width, height, pixelFormat.ToWic(), sw.BitmapCreateCacheOption.CacheOnLoad);
        }

        public void Create(int width, int height, Graphics graphics)
        {
			Create(width, height, PixelFormat.Format32bppRgba);
		}

        public void Save(Stream stream, ImageFormat format)
        {
			using (var encoder = new s.WIC.BitmapEncoder(
				SDFactory.WicImagingFactory,
				format.ToWic()))
			{
				encoder.Initialize(stream);
				using (var frameEncoder = new s.WIC.BitmapFrameEncode(encoder))
				{
					frameEncoder.WriteSource(Control);
					frameEncoder.Commit();
				}
				encoder.Commit();
			}
        }

		public Bitmap Clone(Rectangle? rectangle = null)
        {
			sw.Bitmap bmp = Control;
			if (rectangle != null)
				bmp = new sw.Bitmap(SDFactory.WicImagingFactory, bmp, rectangle.Value.ToDx());
			else
				bmp = new sw.Bitmap(SDFactory.WicImagingFactory, bmp, sw.BitmapCreateCacheOption.CacheOnLoad);

			return new Bitmap(Generator, new BitmapHandler { Control = bmp });
        }

        public Color GetPixel(int x, int y)
        {
			try
			{
				var output = new uint[1];
				Control.CopyPixels(new s.Rectangle(x, y, 1, 1), output);
				return new s.Color4(new s.ColorBGRA(output[0]).ToRgba()).ToEto();
			}
			catch (s.SharpDXException ex)
			{
				Debug.WriteLine("GetPixel: {0}", ex.ToString());
				throw;
			}			
        }

        public Size Size
        {
            get { return Control.Size.ToEto(); }
        }

		public void Create(Image image, int width, int height, ImageInterpolation interpolation)
		{
			var imageHandler = (ImageHandler<TWidget>)image.Handler;

			Control = new sw.Bitmap(SDFactory.WicImagingFactory, width, height, imageHandler.Control.PixelFormat, sw.BitmapCreateCacheOption.CacheOnLoad);
			using (var graphics = new Graphics(Widget as Bitmap))
			{
				graphics.ImageInterpolation = interpolation;
				var rect = new Rectangle(0, 0, width, height);
				graphics.FillRectangle(Colors.Transparent, rect);
				graphics.DrawImage(image, rect);
			}
		}

		public virtual void Reset()
		{
			if (targetBitmap != null)
			{
				targetBitmap.Dispose();
				targetBitmap = null;
			}
#if WINFORMS
			if (sdimage != null)
			{
				sdimage.Dispose();
				sdimage = null;
			}
#endif
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				Reset();
				if (Frames != null)
				{
					foreach (var frame in Frames)
					{
						frame.Dispose();
					}
					Control = null;
				}
			}
			base.Dispose(disposing);
		}

#if WINFORMS
		System.Drawing.Image sdimage;

		public virtual System.Drawing.Image GetImageWithSize(int? size)
		{
			if (size != null && Frames != null && Control.Size.Width > size.Value)
			{
				var src = Frames.Aggregate((x, y) => Math.Abs(x.Size.Width - size.Value) < Math.Abs(y.Size.Width - size.Value) ? x : y);
				if (src != null)
				{
					return src.ToBitmap().ToSD();
				}
			}
			return sdimage ?? (sdimage = Control.ToSD());
		}
#endif
	}
}
