﻿using MarginTrading.MarketMaker.Enums;
using MarginTrading.MarketMaker.Services.CrossRates.Models;

namespace MarginTrading.MarketMaker.Models.Settings
{
    internal class AssetPairSettings
    {
        public AssetPairQuotesSourceTypeEnum QuotesSourceType { get; }
        public AssetPairExtPriceSettings ExtPriceSettings { get; }
        public CrossRateCalcInfo CrossRateCalcInfo { get; }

        public AssetPairSettings(AssetPairQuotesSourceTypeEnum quotesSourceType, AssetPairExtPriceSettings extPriceSettings, CrossRateCalcInfo crossRateCalcInfo)
        {
            QuotesSourceType = quotesSourceType;
            ExtPriceSettings = extPriceSettings;
            CrossRateCalcInfo = crossRateCalcInfo;
        }
    }
}