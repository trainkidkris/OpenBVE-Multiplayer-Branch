using System;
using System.Drawing;
using System.Drawing.Imaging;
using OpenBveApi.Colors;
using OpenBveApi.Textures;
using OpenBveApi.Hosts;
using OpenBveApi.Math;

namespace Plugin {
	public partial class Plugin {
		
		/// <summary>Loads a texture from the specified file.</summary>
		/// <param name="file">The file that holds the texture.</param>
		/// <param name="texture">Receives the texture.</param>
		/// <returns>Whether loading the texture was successful.</returns>
		private bool Parse(string file, out Texture texture) {
			/*
			 * Read the bitmap. This will be a bitmap of just
			 * any format, not necessarily the one that allows
			 * us to extract the bitmap data easily.
			 * */
			using (var image = Image.FromFile(file))
			{
				int width, height;
				Color24[] palette;
				if (image.RawFormat.Equals(ImageFormat.Gif))
				{
					GifDecoder decoder = new GifDecoder();
					decoder.Read(file);
					int frameCount = decoder.GetFrameCount();
					int duration = 0;
					if (frameCount != 1)
					{
						Vector2 frameSize = decoder.GetFrameSize();
						byte[][] frameBytes = new byte[frameCount][];
						for (int i = 0; i < frameCount; i++)
						{
							int[] framePixels = decoder.GetFrame(i);
							frameBytes[i] = new byte[framePixels.Length * sizeof(int)];
							Buffer.BlockCopy(framePixels, 0, frameBytes[i], 0, frameBytes[i].Length);
							duration += decoder.GetDuration(i);
						}
						texture = new Texture((int)frameSize.X, (int)frameSize.Y, 32, frameBytes, ((double)duration / frameCount) / 10000000.0);
						return true;
					}
				}
				Bitmap bitmap = new Bitmap(file);
				byte[] raw = GetRawBitmapData(bitmap, out width, out height, out palette);
				if (raw != null)
				{
					texture = new Texture(width, height, 32, raw, palette);
					return true;
				}
			}
			texture = null;
			return false;
		}

		private byte[] GetRawBitmapData(Bitmap bitmap, out int width, out int height, out Color24[] p)
		{
			p = null;
			if (bitmap.PixelFormat != PixelFormat.Format32bppArgb && bitmap.PixelFormat != PixelFormat.Format24bppRgb)
			{
				/* Only store the color palette data for
				 * textures using a restricted palette
				 * With a large number of textures loaded at
				 * once, this can save a decent chunk of memory
				 * */
				p = new Color24[bitmap.Palette.Entries.Length];
				for (int i = 0; i < bitmap.Palette.Entries.Length; i++)
				{
					p[i] = bitmap.Palette.Entries[i];
				}
			}
			Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
			/* 
			 * If the bitmap format is not already 32-bit BGRA,
			 * then convert it to 32-bit BGRA.
			 * */
			if (bitmap.PixelFormat != PixelFormat.Format32bppArgb) {
				Bitmap compatibleBitmap = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
				Graphics graphics = Graphics.FromImage(compatibleBitmap);
				graphics.DrawImage(bitmap, rect, rect, GraphicsUnit.Pixel);
				graphics.Dispose();
				bitmap.Dispose();
				bitmap = compatibleBitmap;
			}
			/*
			 * Extract the raw bitmap data.
			 * */
			BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);
			if (data.Stride == 4 * data.Width) {
				/*
				 * Copy the data from the bitmap
				 * to the array in BGRA format.
				 * */
				byte[] raw = new byte[data.Stride * data.Height];
				System.Runtime.InteropServices.Marshal.Copy(data.Scan0, raw, 0, data.Stride * data.Height);
				bitmap.UnlockBits(data);
				width = bitmap.Width;
				height = bitmap.Height;
				
				/*
				 * Change the byte order from BGRA to RGBA.
				 * */
				for (int i = 0; i < raw.Length; i += 4) {
					byte temp = raw[i];
					raw[i] = raw[i + 2];
					raw[i + 2] = temp;
				}

				return raw;
			} else {
				/*
				 * The stride is invalid. This indicates that the
				 * CLI either does not implement the conversion to
				 * 32-bit BGRA correctly, or that the CLI has
				 * applied additional padding that we do not
				 * support.
				 * */
				bitmap.UnlockBits(data);
				bitmap.Dispose();
				CurrentHost.ReportProblem(ProblemType.InvalidOperation, "Invalid stride encountered.");
				width = 0;
				height = 0;
				return null;
			}
		}
		
	}
}
