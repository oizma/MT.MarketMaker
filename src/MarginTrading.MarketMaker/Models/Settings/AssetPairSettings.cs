﻿using MarginTrading.MarketMaker.Contracts.Enums;
using MarginTrading.MarketMaker.Enums;
using MarginTrading.MarketMaker.Services.CrossRates.Models;

namespace MarginTrading.MarketMaker.Models.Settings
{
    public class AssetPairSettings
    {
        public AssetPairQuotesSourceTypeDomainEnum QuotesSourceType { get; }
        public AssetPairExtPriceSettings ExtPriceSettings { get; }
        public CrossRateCalcInfo CrossRateCalcInfo { get; }

        public AssetPairSettings(AssetPairQuotesSourceTypeDomainEnum quotesSourceType, AssetPairExtPriceSettings extPriceSettings, CrossRateCalcInfo crossRateCalcInfo)
        {
            QuotesSourceType = quotesSourceType;
            ExtPriceSettings = extPriceSettings;
            CrossRateCalcInfo = crossRateCalcInfo;
        }
    }
}