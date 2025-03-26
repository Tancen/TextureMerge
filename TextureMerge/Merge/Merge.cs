using System;
using System.IO;
using System.Threading.Tasks;
using ImageMagick;

namespace TextureMerge
{
    internal class Merge
    {
        TMImage red = null, green = null, blue = null, alpha = null;
        readonly object redLock = new object(), greenLock = new object(), blueLock = new object(), alphaLock = new object();
        private Channel redChSource = Channel.Red, greenChSource = Channel.Green,
            blueChSource = Channel.Blue, alphaChSource = Channel.Red;

        public bool IsResultImageHasAlpha()
        {
            return alpha != null;
        }

        public Task<TMImage> DoMergeAsync(MagickColor fillColor, uint depth = 0)
        {
            return Task.Run(() =>
            {
                lock (redLock) lock (greenLock) lock (blueLock) lock (alphaLock)
                                return DoMerge(fillColor, depth);
            });
        }

        public TMImage DoMerge(MagickColor fillColor, uint depth = 0)
        {
            if (red is null && green is null && blue is null && alpha is null)
                throw new InvalidOperationException("No image loaded");

            if (!CheckResolution(out uint width, out uint height))
                throw new InvalidOperationException("Resolution missmatch");

            var result = new TMImage(new MagickImage(fillColor, width, height));
            result.Image.Depth = depth == 0 ? GetHighestDepth() : depth;

            if (alpha is null)
                result.Image.Alpha(AlphaOption.Off);
            else
                result.Image.Alpha(AlphaOption.On);

            var redPixels = red is null ? CreateArrayWithColor(width * height * 3, fillColor.R) : ExtractSingleChannel(red, redChSource);
            var greenPixels = green is null ? CreateArrayWithColor(width * height * 3, fillColor.G) : ExtractSingleChannel(green, greenChSource);
            var bluePixels = blue is null ? CreateArrayWithColor(width * height * 3, fillColor.B) : ExtractSingleChannel(blue, blueChSource);
            var alphaPixels = alpha is null ? CreateArrayWithColor(width * height * 4, fillColor.A) : ExtractSingleChannel(alpha, alphaChSource);

            var NUM_DST_PIXEL_BYTES = result.Image.HasAlpha ? 4 : 3;
            var resultPixels = new ushort[width * height * NUM_DST_PIXEL_BYTES];

            for (int i = 0; i < width * height; i++)
            {
                resultPixels[i * NUM_DST_PIXEL_BYTES + 0] = redPixels[i];
                resultPixels[i * NUM_DST_PIXEL_BYTES + 1] = greenPixels[i];
                resultPixels[i * NUM_DST_PIXEL_BYTES + 2] = bluePixels[i];
                if (NUM_DST_PIXEL_BYTES == 4)
                    resultPixels[i * NUM_DST_PIXEL_BYTES + 3] = alphaPixels[i];
            }
            result.SetPixels(resultPixels);

            return result;
        }

        private ushort[] ExtractSingleChannel(TMImage image, Channel channel)
        {
            var NUM_PIXEL_BYTES = image.Image.HasAlpha ? 4 : 3;
            var dstPixels = new ushort[image.Image.Width * image.Image.Height];

            var srcPixels = image.GetPixelArray();
            for (int i = 0; i < dstPixels.Length; i++)
            {
                dstPixels[i] = srcPixels[i * NUM_PIXEL_BYTES + (int)channel];
            }
            return dstPixels;
        }

        private void WriteUShortArrayToFile(ushort[] data, string path)
        {
            FileStream fs = new FileStream(path, FileMode.Create);

            byte[] bytes = new byte[data.Length * 2];
            Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
            fs.Write(bytes, 0, bytes.Length);

            fs.Close();
        }

        private ushort[] CreateArrayWithColor(uint capacity, ushort color)
        {
            var arr = new ushort[capacity];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = color;
            return arr;
        }

        public bool IsGrayScale(Channel channel)
        {
            TMImage img;
            switch (channel)
            {
                case Channel.Red: img = red; break;
                case Channel.Green: img = green; break;
                case Channel.Blue: img = blue; break;
                case Channel.Alpha: img = alpha; break;
                default: throw new ArgumentException("Invalid channel");
            }

            if (img == null)
            {
                throw new NullReferenceException("Cannot check grayscale on empty image");
            }

            var pixels = img.GetPixelArray();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (i % 3 != 0)
                    continue;
                if (pixels[i] != pixels[i + 1] || pixels[i] != pixels[i + 2])
                    return false;
            }
            return true;
        }

        public bool IsDepthSame(out uint depth)
        {
            depth = 0;

            foreach (TMImage image in new TMImage[] { red, green, blue, alpha })
            {
                if (image != null)
                {
                    if (depth == 0)
                        depth = image.Image.Depth;
                    else if (depth != image.Image.Depth)
                        return false;
                }
            }

            return depth != 0;
        }

        private uint GetHighestDepth()
        {
            uint max = 0;
            if (red != null)
                max = red.Image.Depth > max ? red.Image.Depth : max;
            if (green != null)
                max = green.Image.Depth > max ? green.Image.Depth : max;
            if (blue != null)
                max = blue.Image.Depth > max ? blue.Image.Depth : max;
            if (alpha != null)
                max = alpha.Image.Depth > max ? alpha.Image.Depth : max;

            return max;
        }

        public bool CheckResolution(out uint width, out uint height)
        {
            width = height = 0;

            if (red != null)
            {
                width = red.Image.Width;
                height = red.Image.Height;
            }
            if (green != null)
            {
                if (width != 0)
                {
                    if (width != green.Image.Width || height != green.Image.Height)
                        return false;
                }
                else
                {
                    width = green.Image.Width;
                    height = green.Image.Height;
                }
            }
            if (blue != null)
            {
                if (width != 0)
                {
                    if (width != blue.Image.Width || height != blue.Image.Height)
                        return false;
                }
                else
                {
                    width = blue.Image.Width;
                    height = blue.Image.Height;
                }
            }
            if (alpha != null)
            {
                if (width != 0)
                {
                    if (width != alpha.Image.Width || height != alpha.Image.Height)
                        return false;
                }
                else
                {
                    width = alpha.Image.Width;
                    height = alpha.Image.Height;
                }
            }
            return true;
        }

        public bool CheckResolution() => CheckResolution(out _, out _);

        public Task<Merge> ResizeAsync(uint width, uint height, bool stretch, MagickColor fillColor = null)
        {
            return Task.Run(() =>
            {
                lock (redLock) lock (greenLock) lock (blueLock) lock (alphaLock)
                                return Resize(width, height, stretch, fillColor);
            });
        }

        public Merge Resize(uint width, uint height, bool stretch, MagickColor fillColor = null)
        {
            if (width < 1 || height < 1)
                throw new ArgumentException("width and height must be greater than 0");

            Merge merge = this;
            var newInst = new Merge()
            {
                redChSource = merge.redChSource,
                greenChSource = merge.greenChSource,
                blueChSource = merge.blueChSource,
                alphaChSource = merge.alphaChSource,
            };

            if (red != null)
                newInst.red = ResizeImage(red, width, height, stretch, fillColor);
            if (green != null)
                newInst.green = ResizeImage(green, width, height, stretch, fillColor);
            if (blue != null)
                newInst.blue = ResizeImage(blue, width, height, stretch, fillColor);
            if (alpha != null)
                newInst.alpha = ResizeImage(alpha, width, height, stretch, fillColor);

            return newInst;
        }

        private static TMImage ResizeImage(TMImage source, uint width, uint height, bool stretch, MagickColor fillColor = null)
        {
            var result = source.Clone();
            if (result.Image.Width != width || result.Image.Height != height)
            {
                if (stretch)
                {
                    var geo = new MagickGeometry(width, height)
                    {
                        IgnoreAspectRatio = true
                    };
                    result.Image.Resize(geo);
                }
                else
                {
                    MagickColor color = source.Image.HasAlpha ? new MagickColor(0, 0, 0, 1) : new MagickColor(0, 0, 0);
                    result.Image.Resize(width, height);
                    result.Image.Extent(width, height, Gravity.Center, fillColor ?? color);
                }
            }

            return result;
        }

        public TMImage GetImage(Channel channel)
        {
            return ExtractChannel(GetStoredImage(channel), GetSourceChannel(channel));
        }

        public void PutEditedImage(TMImage newImage, Channel channel)
        {
            if (newImage == null)
                throw new ArgumentException("Source bitmap is null");

            AlterImage(channel, (image, sourceChannel) =>
            {
                var pixels = image.GetPixelArray();
                var newPixels = newImage.GetPixelArray();

                if (newImage.Image.Width != image.Image.Width || newImage.Image.Height != image.Image.Height)
                    throw new ArgumentException("Source image resolution is not equal to the new image");

                if (newImage.Image.HasAlpha != image.Image.HasAlpha)
                    throw new ArgumentException("Source image and new image have different alpha value");

                var NUM_PIXEL_BYTES = newImage.Image.HasAlpha ? 4 : 3;
                for (int i = (int)sourceChannel; i < pixels.Length; i += NUM_PIXEL_BYTES)
                {
                    pixels[i] = newPixels[i];
                }
                image.SetPixels(pixels);
                return image;
            });
        }

        
        private static TMImage ExtractChannel(TMImage sourceBitmap, Channel channel)
        {
            if (sourceBitmap == null)
                throw new ArgumentException("Source bitmap is null");

            if (channel == Channel.Alpha && !sourceBitmap.Image.HasAlpha)
                throw new ArgumentException("Source bitmap has no alpha channel");

            var NUM_PIXEL_BYTES = 0;
            MagickImage mgkImage = null;
            if (sourceBitmap.Image.HasAlpha)
            {
                NUM_PIXEL_BYTES = 4;
                mgkImage = new MagickImage(new MagickColor(0, 0, 0, 0), sourceBitmap.Image.Width, sourceBitmap.Image.Height);
                mgkImage.Alpha(AlphaOption.On);
            }
            else
            {
                NUM_PIXEL_BYTES = 3;
                mgkImage = new MagickImage(new MagickColor(0, 0, 0), sourceBitmap.Image.Width, sourceBitmap.Image.Height);
            }

            var result = new TMImage(mgkImage, sourceBitmap.FileName);
            var pixels = result.GetPixelArray();
            var sourcePixels = sourceBitmap.GetPixelArray();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = sourcePixels[i - (i % NUM_PIXEL_BYTES) + (int)channel];
            }
            result.SetPixels(pixels);
            return result;
        }

        private static TMImage MakeChannelThumbnail(TMImage sourceBitmap, Channel channel)
        {
            if (sourceBitmap == null)
                throw new ArgumentException("Source bitmap is null");

            var thumb = sourceBitmap.Clone();
            thumb.Image.Thumbnail(512, 512);
            var result = ExtractChannel(thumb, channel);
            result.Image.Alpha(AlphaOption.Off);
            return result;
        }

        public Task LoadChannelAsync(string path, Channel channelSlot, Channel channelSource)
        {
            switch (channelSlot)
            {
                case Channel.Red:
                    return Task.Run(() => { lock (redLock) LoadChannel(path, channelSlot, channelSource); });
                case Channel.Green:
                    return Task.Run(() => { lock (greenLock) LoadChannel(path, channelSlot, channelSource); });
                case Channel.Blue:
                    return Task.Run(() => { lock (blueLock) LoadChannel(path, channelSlot, channelSource); });
                case Channel.Alpha:
                    return Task.Run(() => { lock (alphaLock) LoadChannel(path, channelSlot, channelSource); });
                default:
                    throw new ArgumentException("Invalid channel");
            }
        }

        public void LoadChannel(string path, Channel channelSlot, Channel channelSource)
        {
            if (path == string.Empty)
                throw new ArgumentException("Invalid path");


            var source = new TMImage(new MagickImage(path), Path.GetFileName(path)) ?? throw new ArgumentException("Failed to load image");

            source.Image.Alpha(AlphaOption.On);
            source.Image.ColorType = ColorType.TrueColorAlpha;
            source.Image.AutoOrient();
            source.Image.ColorSpace = ColorSpace.sRGB;

            switch (channelSlot)
            {
                case Channel.Red:
                    red = source;
                    redChSource = channelSource;
                    break;
                case Channel.Green:
                    green = source;
                    greenChSource = channelSource;
                    break;
                case Channel.Blue:
                    blue = source;
                    blueChSource = channelSource;
                    break;
                case Channel.Alpha:
                    alpha = source;
                    alphaChSource = channelSource;
                    break;
                default:
                    throw new ArgumentException("Invalid channel");
            }
        }

        public void SetChannelSource(Channel channel, Channel channelSource)
        {
            switch (channel)
            {
                case Channel.Red:
                    redChSource = channelSource;
                    break;
                case Channel.Green:
                    greenChSource = channelSource;
                    break;
                case Channel.Blue:
                    blueChSource = channelSource;
                    break;
                case Channel.Alpha:
                    alphaChSource = channelSource;
                    break;
                default:
                    throw new ArgumentException("Invalid channel");
            }
        }

        public TMImage GetChannelThumbnail(Channel channel)
        {
            TMImage thumbnail = GetStoredImage(channel);
            return thumbnail == null ? null : MakeChannelThumbnail(thumbnail, GetSourceChannel(channel));
        }

        public Task<TMImage> GetChannelThumbnailAsync(Channel channel)
        {
            switch (channel)
            {
                case Channel.Red:
                    return Task.Run(() => { lock (redLock) return GetChannelThumbnail(channel); });
                case Channel.Green:
                    return Task.Run(() => { lock (greenLock) return GetChannelThumbnail(channel); });
                case Channel.Blue:
                    return Task.Run(() => { lock (blueLock) return GetChannelThumbnail(channel); });
                case Channel.Alpha:
                    return Task.Run(() => { lock (alphaLock) return GetChannelThumbnail(channel); });
                default:
                    throw new ArgumentException("Invalid channel");
            }
        }

        public void Swap(Channel ch1, Channel ch2)
        {
            TMImage[] imgs = { red, green, blue, alpha, null };
            Channel[] channels = { redChSource, greenChSource, blueChSource, alphaChSource, 0 };

            imgs[4] = imgs[(int)ch1];
            imgs[(int)ch1] = imgs[(int)ch2];
            imgs[(int)ch2] = imgs[4];

            channels[4] = channels[(int)ch1];
            channels[(int)ch1] = channels[(int)ch2];
            channels[(int)ch2] = channels[4];

            red = imgs[0];
            green = imgs[1];
            blue = imgs[2];
            alpha = imgs[3];

            redChSource = channels[0];
            greenChSource = channels[1];
            blueChSource = channels[2];
            alphaChSource = channels[3];
        }

        public void Clear(Channel which)
        {
            AlterImage(which, (image, imageSource) => null);
        }

        public bool IsEmpty(Channel channel)
        {
            return GetStoredImage(channel) is null;
        }

        public string GetOriginFileName(Channel channel)
        {
            return GetStoredImage(channel).FileName;
        }

        public Channel GetSourceChannel(Channel channel)
        {
            switch (channel)
            {
                case Channel.Red:
                    return redChSource;
                case Channel.Green:
                    return greenChSource;
                case Channel.Blue:
                    return blueChSource;
                case Channel.Alpha:
                    return alphaChSource;
                default:
                    throw new ArgumentException("Invalid channel");
            }
        }

        private void AlterImage(Channel channel, Func<TMImage, Channel, TMImage> func)
        {
            switch (channel)
            {
                case Channel.Red:
                    red = func(red, redChSource);
                    break;
                case Channel.Green:
                    green = func(green, greenChSource);
                    break;
                case Channel.Blue:
                    blue = func(blue, blueChSource);
                    break;
                case Channel.Alpha:
                    alpha = func(alpha, alphaChSource);
                    break;
                default:
                    throw new ArgumentException("Invalid channel");
            }
        }

        private TMImage GetStoredImage(Channel channel)
        {
            switch (channel)
            {
                case Channel.Red:
                    return red;
                case Channel.Green:
                    return green;
                case Channel.Blue:
                    return blue;
                case Channel.Alpha:
                    return alpha;
                default:
                    throw new ArgumentException("Invalid channel");
            }
        }
    }
}
