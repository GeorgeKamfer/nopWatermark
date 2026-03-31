using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using Nop.Core.Configuration;

namespace Nop.Plugin.Misc.Watermark
{
    public class WatermarkSettings : ISettings
    {
        public bool WatermarkTextEnable { get; set; }
        public string WatermarkText { get; set; }
        public string WatermarkFont { get; set; }
        public string TextColor { get; set; }
        public int TextRotatedDegree { get; set; }
        public CommonSettings TextSettings { get; set; }
        public bool TextOutlineEnabled { get; set; }
        public string TextOutlineColor { get; set; }

        public bool WatermarkPictureEnable { get; set; }
        public int PictureId { get; set; }
        public CommonSettings PictureSettings { get; set; }

        public bool ApplyOnProductPictures { get; set; }
        public bool ApplyOnCategoryPictures { get; set; }
        public bool ApplyOnManufacturerPictures { get; set; }
        public int MinimumImageWidthForWatermark { get; set; }
        public int MinimumImageHeightForWatermark { get; set; }

        public int MinimumWatermarkSizePx { get; set; }
        public int MaximumWatermarkSizePx { get; set; }

        public bool BrandStripEnabled { get; set; }
        public BrandStripPosition BrandStripPlacement { get; set; }
        public int BrandStripHeight { get; set; }
        public string BrandStripColor { get; set; }
        public double BrandStripOpacity { get; set; }
        public string BrandStripText { get; set; }
        public string BrandStripTextColor { get; set; }
    }

    public enum WatermarkPosition
    {
        TopLeftCorner,
        TopCenter,
        TopRightCorner,
        CenterLeft,
        Center,
        CenterRight,
        BottomLeftCorner,
        BottomCenter,
        BottomRightCorner,
    }

    public enum BrandStripPosition
    {
        Top,
        Bottom
    }

    [TypeConverter("Nop.Plugin.Misc.Watermark.CommonSettingsConvertor")]
    [JsonConverter(typeof(NoTypeConverterJsonConverter<CommonSettings>))]
    public class CommonSettings
    {
        public int Size { get; set; }
        public List<WatermarkPosition> PositionList { get; set; }
        public double Opacity { get; set; }
        public int PaddingX { get; set; }
        public int PaddingY { get; set; }
        public bool UseCustomPosition { get; set; }
        public double CustomX { get; set; }
        public double CustomY { get; set; }
    }

}
