using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Nito.AsyncEx;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Media;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Plugin.Misc.Watermark.Infrastructure;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Plugins;
using Nop.Services.Seo;
using SkiaSharp;

namespace Nop.Plugin.Misc.Watermark.Services
{
    public class MiscWatermarkPictureService : PictureService, IDisposable
    {
        private const int MAX_FONT_SIZE = 2000;

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _thumbLocks = new();

        private readonly IRepository<ProductPicture> _productPictureRepository;
        private readonly IRepository<Category> _categoryRepository;
        private readonly IRepository<Manufacturer> _manufacturerRepository;
        private readonly IPluginService _pluginService;
        private readonly INopFileProvider _fileProvider;
        private readonly FontProvider _fontProvider;
        private readonly ISettingService _settingService;
        private readonly MediaSettings _mediaSettings;
        private readonly IStoreContext _storeContext;
        private readonly AsyncLazy<SKImage> _watermarkImage;

        private bool? _pluginInstalled;

        private async Task<bool> IsPluginInstalledAsync()
        {
            _pluginInstalled ??= (await _pluginService.GetPluginDescriptorBySystemNameAsync<WatermarkPlugin>("Misc.Watermark")) != null;
            return _pluginInstalled.Value;
        }

        public MiscWatermarkPictureService(
           IRepository<Picture> pictureRepository,
           IRepository<Category> categoryRepository,
           IRepository<Manufacturer> manufacturerRepository,
           IRepository<ProductPicture> productPictureRepository,
           ISettingService settingService,
           IWebHelper webHelper,
           MediaSettings mediaSettings,
           IStoreContext storeContext,
           INopFileProvider fileProvider,
           IProductAttributeParser productAttributeParser,
           IRepository<PictureBinary> pictureBinaryRepository,
           IUrlRecordService urlRecordService,
           IDownloadService downloadService,
           IHttpContextAccessor httpContextAccessor,
           ILogger logger,
           IPluginService pluginService,
           FontProvider fontProvider,
           IProductAttributeService productAttributeService)
           : base(
               downloadService,
               httpContextAccessor,
               logger,
               fileProvider,
               productAttributeParser,
               productAttributeService,
               pictureRepository,
               pictureBinaryRepository,
               productPictureRepository,
               settingService,
               urlRecordService,
               webHelper,
               mediaSettings)
        {
            _categoryRepository = categoryRepository;
            _manufacturerRepository = manufacturerRepository;
            _productPictureRepository = productPictureRepository;
            _settingService = settingService;
            _mediaSettings = mediaSettings;
            _fileProvider = fileProvider;

            _storeContext = storeContext;
            _pluginService = pluginService;
            _fontProvider = fontProvider;

            _watermarkImage = new AsyncLazy<SKImage>(async () =>
            {
                var watermarkPictureId = (await GetSettingsAsync()).PictureId;
                if (watermarkPictureId == 0)
                    return null;

                var picture = await base.GetPictureByIdAsync(watermarkPictureId);
                if (picture == null)
                    return null;
                var pictureBinary = await LoadPictureBinaryAsync(picture);
                return SKImage.FromEncodedData(pictureBinary);
            });
        }

        public virtual Task DeleteThumbs()
        {
            var defaultThumbsPath =
                _fileProvider.GetAbsolutePath(NopMediaDefaults.ImageThumbsPath);

            var imageDirectoryInfo = new DirectoryInfo(defaultThumbsPath);
            foreach (var fileInfo in imageDirectoryInfo.GetFiles())
                fileInfo.Delete();

            return Task.CompletedTask;
        }

        public override async Task<(string Url, Picture Picture)> GetPictureUrlAsync(Picture picture,
            int targetSize = 0,
            bool showDefaultPicture = true,
            string storeLocation = null,
            PictureType defaultPictureType = PictureType.Entity)
        {
            if (!await IsPluginInstalledAsync())
                return await base.GetPictureUrlAsync(picture, targetSize, showDefaultPicture, storeLocation, defaultPictureType);

            if (picture == null)
                return showDefaultPicture ? (await GetDefaultPictureUrlAsync(targetSize, defaultPictureType, storeLocation), null) : (string.Empty, (Picture)null);

            byte[] pictureBinary = null;
            if (picture.IsNew)
            {
                await DeletePictureThumbsAsync(picture);
                pictureBinary = await LoadPictureBinaryAsync(picture);

                if ((pictureBinary?.Length ?? 0) == 0)
                    return showDefaultPicture ? (await GetDefaultPictureUrlAsync(targetSize, defaultPictureType, storeLocation), picture) : (string.Empty, picture);

                picture = await UpdatePictureAsync(picture.Id,
                    pictureBinary,
                    picture.MimeType,
                    picture.SeoFilename,
                    picture.AltAttribute,
                    picture.TitleAttribute,
                    false,
                    false);
            }

            var seoFileName = picture.SeoFilename;

            var storeId = (await _storeContext.GetCurrentStoreAsync()).Id;
            var lastPart = await GetFileExtensionFromMimeTypeAsync(picture.MimeType);
            string thumbFileName;
            if (storeId == 1)
            {
                if (targetSize == 0 || picture.MimeType == MimeTypes.ImageSvg)
                    thumbFileName = !string.IsNullOrEmpty(seoFileName)
                        ? $"{picture.Id:0000000}_{seoFileName}.{lastPart}"
                        : $"{picture.Id:0000000}.{lastPart}";
                else
                    thumbFileName = !string.IsNullOrEmpty(seoFileName)
                        ? $"{picture.Id:0000000}_{seoFileName}_{targetSize}.{lastPart}"
                        : $"{picture.Id:0000000}_{targetSize}.{lastPart}";
            }
            else
            {
                if (targetSize == 0 || picture.MimeType == MimeTypes.ImageSvg)
                    thumbFileName = !string.IsNullOrEmpty(seoFileName)
                        ? $"{picture.Id:0000000}_{seoFileName}_{storeId}.{lastPart}"
                        : $"{picture.Id:0000000}_{storeId}.{lastPart}";
                else
                    thumbFileName = !string.IsNullOrEmpty(seoFileName)
                        ? $"{picture.Id:0000000}_{seoFileName}_{targetSize}_{storeId}.{lastPart}"
                        : $"{picture.Id:0000000}_{targetSize}_{storeId}.{lastPart}";
            }

            var thumbFilePath = await GetThumbLocalPathAsync(thumbFileName);

            if (await GeneratedThumbExistsAsync(thumbFilePath, thumbFileName))
                return (await GetThumbUrlAsync(thumbFileName, storeLocation), picture);

            pictureBinary ??= await LoadPictureBinaryAsync(picture);

            if (pictureBinary == null || pictureBinary.Length == 0)
            {
                await SaveThumbAsync(thumbFilePath, thumbFileName, picture?.MimeType ?? string.Empty, pictureBinary ?? Array.Empty<byte>());
                return (await GetThumbUrlAsync(thumbFileName, storeLocation), picture);
            }

            var thumbLock = _thumbLocks.GetOrAdd(thumbFileName, _ => new SemaphoreSlim(1, 1));
            await thumbLock.WaitAsync();
            try
            {
                try
                {
                    if (picture.MimeType != MimeTypes.ImageSvg)
                    {
                        using var inputImage = SKBitmap.Decode(pictureBinary);

                        if (inputImage == null)
                        {
                            await SaveThumbAsync(thumbFilePath, thumbFileName, picture.MimeType, pictureBinary);
                        }
                        else
                        {
                            SKBitmap outputImage = inputImage;

                            if (targetSize != 0)
                                try
                                {
                                    var newSize =
                                        ScaleRectangleToFitBounds(new SKSizeI(targetSize, targetSize), inputImage.Info.Size);
                                    outputImage = inputImage.Resize(newSize, SKFilterQuality.Medium);
                                }
                                catch (Exception ex)
                                {
                                    await _logger.WarningAsync($"Error resizing image for watermark (picture Id {picture.Id}): {ex.Message}", ex);
                                }

                            await MakeImageWatermarkAsync(outputImage, picture.Id);

                            var format = GetImageFormatByMimeType(picture.MimeType);
                            pictureBinary = outputImage.Encode(format,
                                _mediaSettings.DefaultImageQuality > 0 ? _mediaSettings.DefaultImageQuality : 80).ToArray();

                            outputImage.Dispose();

                            await SaveThumbAsync(thumbFilePath, thumbFileName, picture.MimeType, pictureBinary);
                        }
                    }
                    else
                    {
                        await SaveThumbAsync(thumbFilePath, thumbFileName, picture.MimeType, pictureBinary);
                    }
                }
                catch (Exception ex)
                {
                    await _logger.ErrorAsync($"Error during watermark operation (picture Id {picture?.Id}): {ex.Message}", ex);
                    await SaveThumbAsync(thumbFilePath, thumbFileName, picture.MimeType, pictureBinary);
                }
            }
            finally
            {
                thumbLock.Release();
            }

            return (await GetThumbUrlAsync(thumbFileName, storeLocation), picture);
        }

        private async Task MakeImageWatermarkAsync(SKBitmap sourceImage, int pictureId)
        {
            var currentSettings = await GetSettingsAsync();
            var applyWatermark = IsWatermarkRequired(pictureId, currentSettings);

            if (!applyWatermark || sourceImage.Height <= currentSettings.MinimumImageHeightForWatermark
                                || sourceImage.Width <= currentSettings.MinimumImageWidthForWatermark)
                return;

            await ApplyWatermarksAsync(sourceImage, currentSettings);
        }

        internal async Task ApplyWatermarksAsync(SKBitmap sourceImage, WatermarkSettings settings)
        {
            if (settings.BrandStripEnabled)
            {
                var watermarkImg = await _watermarkImage.Task;
                PlaceBrandStrip(sourceImage, watermarkImg, settings);
            }

            if (settings.WatermarkTextEnable && !string.IsNullOrEmpty(settings.WatermarkText))
                PlaceTextWatermark(sourceImage, settings);

            var watermarkImage = await _watermarkImage.Task;
            if (settings.WatermarkPictureEnable && watermarkImage != null)
                PlaceImageWatermark(sourceImage, watermarkImage, settings);
        }

        private static void PlaceImageWatermark(SKBitmap destImage, SKImage watermarkImage,
            WatermarkSettings currentSettings)
        {
            var watermarkSizeInPercent = (double)currentSettings.PictureSettings.Size / 100;
            var boundingBoxSize = new SKSizeI((int)(destImage.Width * watermarkSizeInPercent),
                (int)(destImage.Height * watermarkSizeInPercent));
            var calculatedWatermarkSize =
                ScaleRectangleToFitBounds(boundingBoxSize, new SKSizeI(watermarkImage.Width, watermarkImage.Height));
            if (calculatedWatermarkSize.Width == 0 || calculatedWatermarkSize.Height == 0)
                return;

            calculatedWatermarkSize = ClampWatermarkSize(calculatedWatermarkSize, currentSettings);
            if (calculatedWatermarkSize.Width == 0 || calculatedWatermarkSize.Height == 0)
                return;

            var alpha = (byte)(currentSettings.PictureSettings.Opacity * 255);
            using var paint = new SKPaint
            {
                BlendMode = SKBlendMode.SrcOver,
                Color = SKColors.White.WithAlpha(alpha),
                FilterQuality = SKFilterQuality.High
            };

            using var canvas = new SKCanvas(destImage);

            if (currentSettings.PictureSettings.UseCustomPosition)
            {
                var pos = CalculateCustomPosition(currentSettings.PictureSettings, destImage.Info.Size, calculatedWatermarkSize);
                canvas.DrawImage(watermarkImage, SKRectI.Create(pos, calculatedWatermarkSize), paint);
            }
            else
            {
                foreach (var watermarkPosition in currentSettings.PictureSettings.PositionList.Select(position =>
                         CalculateWatermarkPosition(position, destImage.Info.Size, calculatedWatermarkSize, currentSettings.PictureSettings)))
                    canvas.DrawImage(watermarkImage, SKRectI.Create(watermarkPosition, calculatedWatermarkSize), paint);
            }
        }

        private void PlaceTextWatermark(SKBitmap sourceBitmap, WatermarkSettings currentSettings)
        {
            var text = currentSettings.WatermarkText;
            var textAngle = currentSettings.TextRotatedDegree;
            var sizeFactor = (double)currentSettings.TextSettings.Size / 100;
            var maxTextSize = new SKSizeI(
                (int)(sourceBitmap.Width * sizeFactor),
                (int)(sourceBitmap.Height * sizeFactor));

            var color = SKColor.Parse(currentSettings.TextColor);
            color = color.WithAlpha((byte)(currentSettings.TextSettings.Opacity * 255));

            var typeface = GetFontTypeface(currentSettings);
            var fontSize = ComputeMaxFontSize(typeface, text, textAngle, maxTextSize, out var rotatedTextSize);

            if (fontSize < 2)
                return;

            using var fillPaint = new SKPaint
            {
                Color = color,
                Typeface = typeface,
                TextSize = fontSize,
                TextAlign = SKTextAlign.Center,
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
            };

            var horizontalTextRect = new SKRect();
            fillPaint.MeasureText(text, ref horizontalTextRect);

            SKPaint outlinePaint = null;
            if (currentSettings.TextOutlineEnabled && !string.IsNullOrEmpty(currentSettings.TextOutlineColor))
            {
                var outlineColor = SKColor.Parse(currentSettings.TextOutlineColor);
                outlineColor = outlineColor.WithAlpha((byte)(currentSettings.TextSettings.Opacity * 255));
                var strokeWidth = Math.Max(1f, fontSize / 25f);
                outlinePaint = new SKPaint
                {
                    Color = outlineColor,
                    Typeface = typeface,
                    TextSize = fontSize,
                    TextAlign = SKTextAlign.Center,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = strokeWidth,
                    StrokeJoin = SKStrokeJoin.Round,
                };
            }

            using var canvas = new SKCanvas(sourceBitmap);

            void DrawTextAtPosition(SKPointI textPosition)
            {
                textPosition.Offset(rotatedTextSize.Width / 2, rotatedTextSize.Height / 2);

                canvas.Save();
                canvas.Translate(textPosition);
                canvas.RotateDegrees(textAngle);

                if (outlinePaint != null)
                    canvas.DrawText(text, 0, -horizontalTextRect.MidY, outlinePaint);

                canvas.DrawText(text, 0, -horizontalTextRect.MidY, fillPaint);
                canvas.Restore();
            }

            if (currentSettings.TextSettings.UseCustomPosition)
            {
                var pos = CalculateCustomPosition(currentSettings.TextSettings, sourceBitmap.Info.Size, rotatedTextSize);
                DrawTextAtPosition(pos);
            }
            else
            {
                foreach (var textPosition in currentSettings.TextSettings.PositionList.Select(position =>
                         CalculateWatermarkPosition(position, sourceBitmap.Info.Size, rotatedTextSize, currentSettings.TextSettings)))
                {
                    DrawTextAtPosition(textPosition);
                }
            }

            outlinePaint?.Dispose();
        }

        private void PlaceBrandStrip(SKBitmap sourceBitmap, SKImage logoImage, WatermarkSettings settings)
        {
            var stripHeightPercent = Math.Clamp(settings.BrandStripHeight, 1, 30);
            var stripHeight = (int)(sourceBitmap.Height * stripHeightPercent / 100.0);
            if (stripHeight < 10) return;

            var stripY = settings.BrandStripPlacement == BrandStripPosition.Top ? 0 : sourceBitmap.Height - stripHeight;
            var stripColor = SKColor.TryParse(settings.BrandStripColor ?? "#000000", out var parsed) ? parsed : SKColors.Black;
            var stripAlpha = (byte)(Math.Clamp(settings.BrandStripOpacity, 0, 1) * 255);
            stripColor = stripColor.WithAlpha(stripAlpha);

            using var canvas = new SKCanvas(sourceBitmap);

            using (var stripPaint = new SKPaint { Color = stripColor, Style = SKPaintStyle.Fill })
            {
                canvas.DrawRect(0, stripY, sourceBitmap.Width, stripHeight, stripPaint);
            }

            var innerPadding = (int)(stripHeight * 0.15);
            var contentHeight = stripHeight - (innerPadding * 2);
            if (contentHeight < 4) return;

            var contentX = innerPadding * 2;
            var contentY = stripY + innerPadding;

            if (settings.WatermarkPictureEnable && logoImage != null)
            {
                var logoAspect = (double)logoImage.Width / logoImage.Height;
                var logoHeight = contentHeight;
                var logoWidth = (int)(logoHeight * logoAspect);

                using var logoPaint = new SKPaint
                {
                    FilterQuality = SKFilterQuality.High,
                    Color = SKColors.White.WithAlpha(255),
                };

                var logoRect = new SKRect(contentX, contentY, contentX + logoWidth, contentY + logoHeight);
                canvas.DrawImage(logoImage, logoRect, logoPaint);
                contentX += logoWidth + innerPadding;
            }

            if (!string.IsNullOrEmpty(settings.BrandStripText))
            {
                var textColor = SKColor.TryParse(settings.BrandStripTextColor ?? "#FFFFFF", out var tc) ? tc : SKColors.White;
                var typeface = GetFontTypeface(settings);

                var textFontSize = contentHeight * 0.7f;
                using var textPaint = new SKPaint
                {
                    Color = textColor,
                    Typeface = typeface,
                    TextSize = textFontSize,
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill,
                };

                var textBounds = new SKRect();
                textPaint.MeasureText(settings.BrandStripText, ref textBounds);

                if (textBounds.Width > sourceBitmap.Width - contentX - innerPadding)
                {
                    var scale = (sourceBitmap.Width - contentX - innerPadding * 2) / textBounds.Width;
                    textPaint.TextSize *= scale;
                    textPaint.MeasureText(settings.BrandStripText, ref textBounds);
                }

                var textY = contentY + (contentHeight / 2f) - textBounds.MidY;
                canvas.DrawText(settings.BrandStripText, contentX, textY, textPaint);
            }
        }

        /// <summary>
        /// Generates a preview image with watermark applied using the given settings.
        /// </summary>
        public async Task<byte[]> GeneratePreviewAsync(WatermarkSettings settings, int sourcePictureId = 0)
        {
            if (sourcePictureId == 0)
            {
                var firstPp = _productPictureRepository.Table.FirstOrDefault();
                if (firstPp != null)
                    sourcePictureId = firstPp.PictureId;
            }

            if (sourcePictureId == 0)
                return null;

            var picture = await base.GetPictureByIdAsync(sourcePictureId);
            if (picture == null)
                return null;

            var binary = await LoadPictureBinaryAsync(picture);
            if (binary == null || binary.Length == 0)
                return null;

            using var inputImage = SKBitmap.Decode(binary);
            if (inputImage == null)
                return null;

            var previewMaxSize = 600;
            var newSize = ScaleRectangleToFitBounds(new SKSizeI(previewMaxSize, previewMaxSize), inputImage.Info.Size);
            using var resized = inputImage.Resize(newSize, SKFilterQuality.Medium) ?? inputImage;

            await ApplyWatermarksAsync(resized, settings);

            return resized.Encode(SKEncodedImageFormat.Png, 90).ToArray();
        }

        private static int ComputeMaxFontSize(SKTypeface typeface, string text, int angle, SKSizeI bounds,
            out SKSizeI actualRotatedTextSize)
        {
            actualRotatedTextSize = new SKSizeI();
            if (string.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0)
                return 0;

            using var paint = new SKPaint { Typeface = typeface };
            for (var fontSize = 2; fontSize <= MAX_FONT_SIZE; fontSize++)
            {
                paint.TextSize = fontSize;
                var textRect = new SKRect();
                paint.MeasureText(text, ref textRect);
                var rotatedTextSize = CalculateRotatedRectSize(textRect.Size, angle);
                if ((rotatedTextSize.Width > bounds.Width) ||
                    (rotatedTextSize.Height > bounds.Height))
                    return fontSize - 1;

                actualRotatedTextSize = rotatedTextSize.ToSizeI();
            }
            return MAX_FONT_SIZE;
        }

        private bool IsWatermarkRequired(int pictureId, WatermarkSettings settings)
        {
            if (settings.ApplyOnProductPictures && _productPictureRepository.Table.Any(product => product.PictureId == pictureId))
                return true;

            if (settings.ApplyOnCategoryPictures && _categoryRepository.Table.Any(category => category.PictureId == pictureId))
                return true;

            return settings.ApplyOnManufacturerPictures &&
                   _manufacturerRepository.Table.Any(manufacturer => manufacturer.PictureId == pictureId);
        }

        private async Task<WatermarkSettings> GetSettingsAsync()
        {
            var currentStore = await _storeContext.GetCurrentStoreAsync();
            return await _settingService.LoadSettingAsync<WatermarkSettings>(currentStore.Id);
        }

        private static SKSizeI ScaleRectangleToFitBounds(SKSizeI bounds, SKSizeI rect)
        {
            if (rect.Width < bounds.Width && rect.Height < bounds.Height)
                return rect;

            if (bounds.Width == 0 || bounds.Height == 0)
                return new SKSizeI(0, 0);

            var scaleFactorWidth = (double)rect.Width / bounds.Width;
            var scaleFactorHeight = (double)rect.Height / bounds.Height;

            var scaleFactor = Math.Max(scaleFactorWidth, scaleFactorHeight);
            return new SKSizeI
            {
                Width = (int)(rect.Width / scaleFactor),
                Height = (int)(rect.Height / scaleFactor)
            };
        }

        private static SKSize CalculateRotatedRectSize(SKSize rectSize, double angleDeg)
        {
            var angleRad = angleDeg * Math.PI / 180;
            var width = rectSize.Height * Math.Abs(Math.Sin(angleRad)) +
                        rectSize.Width * Math.Abs(Math.Cos(angleRad));
            var height = rectSize.Height * Math.Abs(Math.Cos(angleRad)) +
                         rectSize.Width * Math.Abs(Math.Sin(angleRad));
            return new SKSize((float)width, (float)height);
        }

        private static SKSizeI ClampWatermarkSize(SKSizeI size, WatermarkSettings settings)
        {
            if (settings.MinimumWatermarkSizePx > 0
                && (size.Width < settings.MinimumWatermarkSizePx || size.Height < settings.MinimumWatermarkSizePx))
                return new SKSizeI(0, 0);

            if (settings.MaximumWatermarkSizePx > 0
                && (size.Width > settings.MaximumWatermarkSizePx || size.Height > settings.MaximumWatermarkSizePx))
            {
                return ScaleRectangleToFitBounds(
                    new SKSizeI(settings.MaximumWatermarkSizePx, settings.MaximumWatermarkSizePx), size);
            }

            return size;
        }

        private static SKPointI CalculateCustomPosition(CommonSettings posSettings, SKSizeI imageSize, SKSizeI watermarkSize)
        {
            var x = (int)((imageSize.Width - watermarkSize.Width) * Math.Clamp(posSettings.CustomX, 0, 100) / 100.0);
            var y = (int)((imageSize.Height - watermarkSize.Height) * Math.Clamp(posSettings.CustomY, 0, 100) / 100.0);
            return new SKPointI(x, y);
        }

        private static SKPointI CalculateWatermarkPosition(WatermarkPosition watermarkPosition, SKSizeI imageSize,
            SKSizeI watermarkSize, CommonSettings posSettings)
        {
            var padX = posSettings?.PaddingX ?? 0;
            var padY = posSettings?.PaddingY ?? 0;
            var position = new SKPointI();

            switch (watermarkPosition)
            {
                case WatermarkPosition.TopLeftCorner:
                    position.X = padX;
                    position.Y = padY;
                    break;
                case WatermarkPosition.TopCenter:
                    position.X = (imageSize.Width / 2) - (watermarkSize.Width / 2);
                    position.Y = padY;
                    break;
                case WatermarkPosition.TopRightCorner:
                    position.X = imageSize.Width - watermarkSize.Width - padX;
                    position.Y = padY;
                    break;
                case WatermarkPosition.CenterLeft:
                    position.X = padX;
                    position.Y = (imageSize.Height / 2) - (watermarkSize.Height / 2);
                    break;
                case WatermarkPosition.Center:
                    position.X = (imageSize.Width / 2) - (watermarkSize.Width / 2);
                    position.Y = (imageSize.Height / 2) - (watermarkSize.Height / 2);
                    break;
                case WatermarkPosition.CenterRight:
                    position.X = imageSize.Width - watermarkSize.Width - padX;
                    position.Y = (imageSize.Height / 2) - (watermarkSize.Height / 2);
                    break;
                case WatermarkPosition.BottomLeftCorner:
                    position.X = padX;
                    position.Y = imageSize.Height - watermarkSize.Height - padY;
                    break;
                case WatermarkPosition.BottomCenter:
                    position.X = (imageSize.Width / 2) - (watermarkSize.Width / 2);
                    position.Y = imageSize.Height - watermarkSize.Height - padY;
                    break;
                case WatermarkPosition.BottomRightCorner:
                    position.X = imageSize.Width - watermarkSize.Width - padX;
                    position.Y = imageSize.Height - watermarkSize.Height - padY;
                    break;
            }
            return position;
        }

        private SKTypeface GetFontTypeface(WatermarkSettings settings)
        {
            var typeface = _fontProvider.GetTypeface(settings.WatermarkFont);
            if (typeface != null)
                return typeface;

            return _fontProvider.AvailableFonts.Any()
                ? _fontProvider.GetTypeface(_fontProvider.AvailableFonts.First())
                : throw new InvalidOperationException("Fonts are missing");
        }

        /// <inheritdoc />
        public override async Task<Picture> GetProductPictureAsync(
            Product product, string attributesXml)
        {
            if (product == null)
                return null;

            try
            {
                return await base.GetProductPictureAsync(product, attributesXml);
            }
            catch (NullReferenceException ex)
            {
                await _logger.WarningAsync(
                    $"Product {product.Id}: attribute value referenced in AttributesXml no longer exists, falling back to main picture: {ex.Message}", ex);
                return (await GetPicturesByProductIdAsync(product.Id, 1)).FirstOrDefault();
            }
        }

        #region IDisposable

        private void ReleaseUnmanagedResources()
        {
            if (_watermarkImage.IsStarted)
            {
                var image = _watermarkImage.Task.GetAwaiter().GetResult();
                image?.Dispose();
            }
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~MiscWatermarkPictureService()
        {
            ReleaseUnmanagedResources();
        }

        #endregion
    }
}
