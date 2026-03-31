using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.Watermark.Infrastructure;
using Nop.Plugin.Misc.Watermark.Models;
using Nop.Plugin.Misc.Watermark.Services;
using Nop.Services.Caching;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using SkiaSharp;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.Watermark.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [AutoValidateAntiforgeryToken]
    public class MiscWatermarkController : BasePluginController
    {
        private readonly IStoreContext _storeContext;
        private readonly FontProvider _fontProvider;
        private readonly ILocalizationService _localizationService;
        private readonly ISettingService _settingService;
        private readonly IPermissionService _permissionService;
        private readonly INotificationService _notificationService;
        private readonly IPictureService _pictureService;

        public MiscWatermarkController(
            IPermissionService permissionService,
            ILocalizationService localizationService,
            INotificationService notificationService,
            ISettingService settingService,
            IStoreContext storeContext,
            FontProvider fontProvider,
            IPictureService pictureService)
        {
            _localizationService = localizationService;
            _notificationService = notificationService;
            _settingService = settingService;
            _permissionService = permissionService;
            _storeContext = storeContext;
            _fontProvider = fontProvider;
            _pictureService = pictureService;
        }

        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            var activeStoreScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<WatermarkSettings>(activeStoreScope);
            var model = new ConfigurationModel
            {
                WatermarkTextEnable = settings.WatermarkTextEnable,
                WatermarkText = settings.WatermarkText,
                AvailableFontsList = GetAvailableFontNames(),
                WatermarkFont = settings.WatermarkFont,
                TextColor = $"{settings.TextColor}",
                TextSettings = MapCommonSettingsToModel(settings.TextSettings),
                TextRotatedDegree = settings.TextRotatedDegree,
                TextOutlineEnabled = settings.TextOutlineEnabled,
                TextOutlineColor = settings.TextOutlineColor ?? "",
                WatermarkPictureEnable = settings.WatermarkPictureEnable,
                PictureId = settings.PictureId,
                PictureSettings = MapCommonSettingsToModel(settings.PictureSettings),
                ActiveStoreScopeConfiguration = activeStoreScope,
                ApplyOnProductPictures = settings.ApplyOnProductPictures,
                ApplyOnCategoryPictures = settings.ApplyOnCategoryPictures,
                ApplyOnManufacturerPictures = settings.ApplyOnManufacturerPictures,
                MinimumImageWidthForWatermark = settings.MinimumImageWidthForWatermark,
                MinimumImageHeightForWatermark = settings.MinimumImageHeightForWatermark,
                MinimumWatermarkSizePx = settings.MinimumWatermarkSizePx,
                MaximumWatermarkSizePx = settings.MaximumWatermarkSizePx,
                BrandStripEnabled = settings.BrandStripEnabled,
                BrandStripPlacement = (int)settings.BrandStripPlacement,
                AvailableBrandStripPositions = new List<SelectListItem>
                {
                    new SelectListItem("Top", "0"),
                    new SelectListItem("Bottom", "1"),
                },
                BrandStripHeight = settings.BrandStripHeight,
                BrandStripColor = settings.BrandStripColor ?? "000000",
                BrandStripOpacity = settings.BrandStripOpacity,
                BrandStripText = settings.BrandStripText ?? "",
                BrandStripTextColor = settings.BrandStripTextColor ?? "FFFFFF",
            };

            if (model.AvailableFontsList.All(i => i.Value != model.WatermarkFont))
                model.WatermarkFont = string.Empty;

            if (activeStoreScope > 0)
            {
                model.WatermarkTextEnable_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.WatermarkTextEnable, activeStoreScope);
                model.Text_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.WatermarkText, activeStoreScope);
                model.WatermarkFont_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.WatermarkFont, activeStoreScope);
                model.TextColor_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.TextColor, activeStoreScope);
                model.TextSettings_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.TextSettings, activeStoreScope);
                model.WatermarkTextRotatedDegree_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.TextRotatedDegree, activeStoreScope);
                model.TextOutlineEnabled_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.TextOutlineEnabled, activeStoreScope);
                model.TextOutlineColor_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.TextOutlineColor, activeStoreScope);
                model.WatermarkPictureEnable_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.WatermarkPictureEnable, activeStoreScope);
                model.WatermarkPictureId_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.PictureId, activeStoreScope);
                model.PictureSettings_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.PictureSettings, activeStoreScope);
                model.ApplyOnProductPictures_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.ApplyOnProductPictures, activeStoreScope);
                model.ApplyOnCategoryPictures_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.ApplyOnCategoryPictures, activeStoreScope);
                model.ApplyOnManufacturerPictures_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.ApplyOnManufacturerPictures, activeStoreScope);
                model.WatermarkMinimumImageHeightForWatermark_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.MinimumImageHeightForWatermark, activeStoreScope);
                model.WatermarkMinimumImageWidthForWatermark_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.MinimumImageWidthForWatermark, activeStoreScope);
                model.MinimumWatermarkSizePx_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.MinimumWatermarkSizePx, activeStoreScope);
                model.MaximumWatermarkSizePx_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.MaximumWatermarkSizePx, activeStoreScope);
                model.BrandStripEnabled_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.BrandStripEnabled, activeStoreScope);
                model.BrandStripPlacement_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.BrandStripPlacement, activeStoreScope);
                model.BrandStripHeight_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.BrandStripHeight, activeStoreScope);
                model.BrandStripColor_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.BrandStripColor, activeStoreScope);
                model.BrandStripOpacity_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.BrandStripOpacity, activeStoreScope);
                model.BrandStripText_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.BrandStripText, activeStoreScope);
                model.BrandStripTextColor_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.BrandStripTextColor, activeStoreScope);
            }

            return View("~/Plugins/Misc.Watermark/Views/MiscWatermark/Configure.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            FixDoubleModelBinding(model);

            var activeStoreScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<WatermarkSettings>(activeStoreScope);

            settings.WatermarkTextEnable = model.WatermarkTextEnable;
            settings.WatermarkText = model.WatermarkText;
            settings.WatermarkFont = model.WatermarkFont;
            if (model.TextColor != null)
            {
                SKColor.TryParse(model.TextColor, out var color);
                settings.TextColor = color.ToRgb24Hex();
            }
            settings.TextRotatedDegree = model.TextRotatedDegree;
            settings.TextOutlineEnabled = model.TextOutlineEnabled;
            if (model.TextOutlineColor != null)
            {
                SKColor.TryParse(model.TextOutlineColor, out var outlineColor);
                settings.TextOutlineColor = outlineColor.ToRgb24Hex();
            }
            if (model.TextSettings != null)
            {
                settings.TextSettings.Size = model.TextSettings.Size;
                settings.TextSettings.Opacity = model.TextSettings.Opacity;
                settings.TextSettings.PositionList = PreparePositionList(model.TextSettings);
                settings.TextSettings.PaddingX = model.TextSettings.PaddingX;
                settings.TextSettings.PaddingY = model.TextSettings.PaddingY;
                settings.TextSettings.UseCustomPosition = model.TextSettings.UseCustomPosition;
                settings.TextSettings.CustomX = model.TextSettings.CustomX;
                settings.TextSettings.CustomY = model.TextSettings.CustomY;
            }

            settings.WatermarkPictureEnable = model.WatermarkPictureEnable;
            settings.PictureId = model.PictureId == 0 ? settings.PictureId : model.PictureId;
            if (model.PictureSettings != null)
            {
                settings.PictureSettings.Size = model.PictureSettings.Size;
                settings.PictureSettings.Opacity = model.PictureSettings.Opacity;
                settings.PictureSettings.PositionList = PreparePositionList(model.PictureSettings);
                settings.PictureSettings.PaddingX = model.PictureSettings.PaddingX;
                settings.PictureSettings.PaddingY = model.PictureSettings.PaddingY;
                settings.PictureSettings.UseCustomPosition = model.PictureSettings.UseCustomPosition;
                settings.PictureSettings.CustomX = model.PictureSettings.CustomX;
                settings.PictureSettings.CustomY = model.PictureSettings.CustomY;
            }

            settings.ApplyOnProductPictures = model.ApplyOnProductPictures;
            settings.ApplyOnCategoryPictures = model.ApplyOnCategoryPictures;
            settings.ApplyOnManufacturerPictures = model.ApplyOnManufacturerPictures;
            settings.MinimumImageHeightForWatermark = model.MinimumImageHeightForWatermark;
            settings.MinimumImageWidthForWatermark = model.MinimumImageWidthForWatermark;
            settings.MinimumWatermarkSizePx = model.MinimumWatermarkSizePx;
            settings.MaximumWatermarkSizePx = model.MaximumWatermarkSizePx;

            settings.BrandStripEnabled = model.BrandStripEnabled;
            settings.BrandStripPlacement = (BrandStripPosition)model.BrandStripPlacement;
            settings.BrandStripHeight = model.BrandStripHeight;
            if (model.BrandStripColor != null)
            {
                SKColor.TryParse(model.BrandStripColor, out var stripColor);
                settings.BrandStripColor = stripColor.ToRgb24Hex();
            }
            settings.BrandStripOpacity = model.BrandStripOpacity;
            settings.BrandStripText = model.BrandStripText;
            if (model.BrandStripTextColor != null)
            {
                SKColor.TryParse(model.BrandStripTextColor, out var stripTextColor);
                settings.BrandStripTextColor = stripTextColor.ToRgb24Hex();
            }

            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.WatermarkTextEnable, model.WatermarkTextEnable_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.WatermarkText, model.Text_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.WatermarkFont, model.WatermarkFont_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.TextColor, model.TextColor_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.TextRotatedDegree, model.WatermarkTextRotatedDegree_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.TextSettings, model.TextSettings_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.TextOutlineEnabled, model.TextOutlineEnabled_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.TextOutlineColor, model.TextOutlineColor_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.WatermarkPictureEnable, model.WatermarkPictureEnable_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.PictureId, model.WatermarkPictureId_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.PictureSettings, model.PictureSettings_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.ApplyOnProductPictures, model.ApplyOnProductPictures_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.ApplyOnCategoryPictures, model.ApplyOnCategoryPictures_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.ApplyOnManufacturerPictures, model.ApplyOnManufacturerPictures_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.MinimumImageHeightForWatermark, model.WatermarkMinimumImageHeightForWatermark_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.MinimumImageWidthForWatermark, model.WatermarkMinimumImageWidthForWatermark_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.MinimumWatermarkSizePx, model.MinimumWatermarkSizePx_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.MaximumWatermarkSizePx, model.MaximumWatermarkSizePx_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.BrandStripEnabled, model.BrandStripEnabled_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.BrandStripPlacement, model.BrandStripPlacement_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.BrandStripHeight, model.BrandStripHeight_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.BrandStripColor, model.BrandStripColor_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.BrandStripOpacity, model.BrandStripOpacity_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.BrandStripText, model.BrandStripText_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.BrandStripTextColor, model.BrandStripTextColor_OverrideForStore, activeStoreScope, false);

            await new ClearCacheTask(EngineContext.Current.Resolve<IStaticCacheManager>()).ExecuteAsync();
            if (EngineContext.Current.Resolve<IPictureService>() is MiscWatermarkPictureService pictureService)
                await pictureService.DeleteThumbs();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> PreviewWatermark(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return Json(new { success = false, message = "Access denied" });

            FixDoubleModelBinding(model);

            try
            {
                var settings = MapModelToSettings(model);
                if (_pictureService is MiscWatermarkPictureService watermarkService)
                {
                    var previewBytes = await watermarkService.GeneratePreviewAsync(settings);
                    if (previewBytes == null)
                        return Json(new { success = false, message = "No product pictures found to use as preview sample." });

                    var base64 = Convert.ToBase64String(previewBytes);
                    return Json(new { success = true, image = $"data:image/png;base64,{base64}" });
                }

                return Json(new { success = false, message = "Watermark service not available." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Preview failed: {ex.Message}" });
            }
        }

        private WatermarkSettings MapModelToSettings(ConfigurationModel model)
        {
            var settings = new WatermarkSettings
            {
                WatermarkTextEnable = model.WatermarkTextEnable,
                WatermarkText = model.WatermarkText,
                WatermarkFont = model.WatermarkFont,
                TextRotatedDegree = model.TextRotatedDegree,
                TextOutlineEnabled = model.TextOutlineEnabled,
                WatermarkPictureEnable = model.WatermarkPictureEnable,
                PictureId = model.PictureId,
                ApplyOnProductPictures = model.ApplyOnProductPictures,
                ApplyOnCategoryPictures = model.ApplyOnCategoryPictures,
                ApplyOnManufacturerPictures = model.ApplyOnManufacturerPictures,
                MinimumImageWidthForWatermark = model.MinimumImageWidthForWatermark,
                MinimumImageHeightForWatermark = model.MinimumImageHeightForWatermark,
                MinimumWatermarkSizePx = model.MinimumWatermarkSizePx,
                MaximumWatermarkSizePx = model.MaximumWatermarkSizePx,
                BrandStripEnabled = model.BrandStripEnabled,
                BrandStripPlacement = (BrandStripPosition)model.BrandStripPlacement,
                BrandStripHeight = model.BrandStripHeight,
                BrandStripOpacity = model.BrandStripOpacity,
                BrandStripText = model.BrandStripText,
            };

            if (model.TextColor != null)
            {
                SKColor.TryParse(model.TextColor, out var tc);
                settings.TextColor = tc.ToRgb24Hex();
            }
            if (model.TextOutlineColor != null)
            {
                SKColor.TryParse(model.TextOutlineColor, out var toc);
                settings.TextOutlineColor = toc.ToRgb24Hex();
            }
            if (model.BrandStripColor != null)
            {
                SKColor.TryParse(model.BrandStripColor, out var bsc);
                settings.BrandStripColor = bsc.ToRgb24Hex();
            }
            if (model.BrandStripTextColor != null)
            {
                SKColor.TryParse(model.BrandStripTextColor, out var bstc);
                settings.BrandStripTextColor = bstc.ToRgb24Hex();
            }

            settings.TextSettings = model.TextSettings != null
                ? new CommonSettings
                {
                    Size = model.TextSettings.Size,
                    Opacity = model.TextSettings.Opacity,
                    PositionList = PreparePositionList(model.TextSettings),
                    PaddingX = model.TextSettings.PaddingX,
                    PaddingY = model.TextSettings.PaddingY,
                    UseCustomPosition = model.TextSettings.UseCustomPosition,
                    CustomX = model.TextSettings.CustomX,
                    CustomY = model.TextSettings.CustomY,
                }
                : new CommonSettings { PositionList = new List<WatermarkPosition>() };

            settings.PictureSettings = model.PictureSettings != null
                ? new CommonSettings
                {
                    Size = model.PictureSettings.Size,
                    Opacity = model.PictureSettings.Opacity,
                    PositionList = PreparePositionList(model.PictureSettings),
                    PaddingX = model.PictureSettings.PaddingX,
                    PaddingY = model.PictureSettings.PaddingY,
                    UseCustomPosition = model.PictureSettings.UseCustomPosition,
                    CustomX = model.PictureSettings.CustomX,
                    CustomY = model.PictureSettings.CustomY,
                }
                : new CommonSettings { PositionList = new List<WatermarkPosition>() };

            return settings;
        }

        private static CommonWatermarkSettings MapCommonSettingsToModel(CommonSettings s)
        {
            s ??= new CommonSettings { PositionList = new List<WatermarkPosition>() };
            return new CommonWatermarkSettings
            {
                Size = s.Size,
                TopLeftCorner = s.PositionList.Contains(WatermarkPosition.TopLeftCorner),
                TopCenter = s.PositionList.Contains(WatermarkPosition.TopCenter),
                TopRightCorner = s.PositionList.Contains(WatermarkPosition.TopRightCorner),
                CenterLeft = s.PositionList.Contains(WatermarkPosition.CenterLeft),
                Center = s.PositionList.Contains(WatermarkPosition.Center),
                CenterRight = s.PositionList.Contains(WatermarkPosition.CenterRight),
                BottomLeftCorner = s.PositionList.Contains(WatermarkPosition.BottomLeftCorner),
                BottomCenter = s.PositionList.Contains(WatermarkPosition.BottomCenter),
                BottomRightCorner = s.PositionList.Contains(WatermarkPosition.BottomRightCorner),
                Opacity = s.Opacity,
                PaddingX = s.PaddingX,
                PaddingY = s.PaddingY,
                UseCustomPosition = s.UseCustomPosition,
                CustomX = s.CustomX,
                CustomY = s.CustomY,
            };
        }

        private static List<WatermarkPosition> PreparePositionList(CommonWatermarkSettings model)
        {
            var positionList = new List<WatermarkPosition>();
            if (model.TopLeftCorner)
                positionList.Add(WatermarkPosition.TopLeftCorner);
            if (model.TopCenter)
                positionList.Add(WatermarkPosition.TopCenter);
            if (model.TopRightCorner)
                positionList.Add(WatermarkPosition.TopRightCorner);
            if (model.CenterLeft)
                positionList.Add(WatermarkPosition.CenterLeft);
            if (model.Center)
                positionList.Add(WatermarkPosition.Center);
            if (model.CenterRight)
                positionList.Add(WatermarkPosition.CenterRight);
            if (model.BottomLeftCorner)
                positionList.Add(WatermarkPosition.BottomLeftCorner);
            if (model.BottomCenter)
                positionList.Add(WatermarkPosition.BottomCenter);
            if (model.BottomRightCorner)
                positionList.Add(WatermarkPosition.BottomRightCorner);
            return positionList;
        }

        private List<SelectListItem> GetAvailableFontNames()
        {
            var customGroup = new SelectListGroup { Name = "Custom" };
            var customFonts = _fontProvider.CustomFonts.Select(s =>
                new SelectListItem { Text = s, Value = s, Group = customGroup });

            var systemGroup = new SelectListGroup { Name = "System" };
            var systemFonts = _fontProvider.SystemFonts.Select(s =>
                new SelectListItem { Text = s, Value = s, Group = systemGroup });

            return customFonts.Concat(systemFonts).ToList();
        }

        /// <summary>
        /// Re-parses all double form values using InvariantCulture so that "0.50" (period decimal)
        /// is correctly interpreted regardless of the server's locale settings.
        /// </summary>
        private void FixDoubleModelBinding(ConfigurationModel model)
        {
            model.TextSettings ??= new CommonWatermarkSettings();
            model.PictureSettings ??= new CommonWatermarkSettings();

            model.TextSettings.Opacity = ReadFormDouble("TextSettings.Opacity", model.TextSettings.Opacity);
            model.TextSettings.CustomX = ReadFormDouble("TextSettings.CustomX", model.TextSettings.CustomX);
            model.TextSettings.CustomY = ReadFormDouble("TextSettings.CustomY", model.TextSettings.CustomY);

            model.PictureSettings.Opacity = ReadFormDouble("PictureSettings.Opacity", model.PictureSettings.Opacity);
            model.PictureSettings.CustomX = ReadFormDouble("PictureSettings.CustomX", model.PictureSettings.CustomX);
            model.PictureSettings.CustomY = ReadFormDouble("PictureSettings.CustomY", model.PictureSettings.CustomY);

            model.BrandStripOpacity = ReadFormDouble("BrandStripOpacity", model.BrandStripOpacity);
        }

        private double ReadFormDouble(string formKey, double fallback)
        {
            if (Request.Form.TryGetValue(formKey, out var raw) && !string.IsNullOrWhiteSpace(raw))
            {
                if (double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var result))
                    return result;
            }
            return fallback;
        }
    }
}
