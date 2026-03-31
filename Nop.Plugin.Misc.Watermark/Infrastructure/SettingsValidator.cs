using FluentValidation;
using Nop.Plugin.Misc.Watermark.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.Misc.Watermark.Infrastructure
{
    public class ConfigurationModelValidator : BaseNopValidator<ConfigurationModel>
    {
        public ConfigurationModelValidator(ILocalizationService localizationService)
        {
            RuleFor(x => x.TextSettings).SetValidator(new CommonWatermarkSettingsValidator(localizationService));
            RuleFor(x => x.PictureSettings).SetValidator(new CommonWatermarkSettingsValidator(localizationService));

            RuleFor(x => x.BrandStripHeight)
                .InclusiveBetween(1, 30)
                .When(x => x.BrandStripEnabled)
                .WithMessage("Brand strip height must be between 1% and 30%");

            RuleFor(x => x.BrandStripOpacity)
                .InclusiveBetween(0, 1)
                .When(x => x.BrandStripEnabled)
                .WithMessage("Brand strip opacity must be between 0 and 1");

            RuleFor(x => x.MinimumWatermarkSizePx)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Minimum watermark size must be 0 or greater");

            RuleFor(x => x.MaximumWatermarkSizePx)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Maximum watermark size must be 0 or greater");
        }
    }

    public class CommonWatermarkSettingsValidator : AbstractValidator<CommonWatermarkSettings>
    {
        public CommonWatermarkSettingsValidator(ILocalizationService localizationService)
        {
            RuleFor(x => x.Opacity)
                .InclusiveBetween(0, 1)
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Misc.Watermark.WatermarkOpacityErrorMessage"), 0, 1);

            RuleFor(x => x.Size)
                .InclusiveBetween(0, 100)
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Misc.Watermark.SizeErrorMessage"), 0, 100);

            RuleFor(x => x.PaddingX)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Horizontal padding must be 0 or greater");

            RuleFor(x => x.PaddingY)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Vertical padding must be 0 or greater");

            RuleFor(x => x.CustomX)
                .InclusiveBetween(0, 100)
                .When(x => x.UseCustomPosition)
                .WithMessage("Custom X must be between 0 and 100");

            RuleFor(x => x.CustomY)
                .InclusiveBetween(0, 100)
                .When(x => x.UseCustomPosition)
                .WithMessage("Custom Y must be between 0 and 100");
        }
    }
}
