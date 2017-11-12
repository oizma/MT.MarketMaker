﻿namespace MarginTrading.MarketMaker.Services.CrossRates.Models
{
    internal class CrossRateSourceAssetPair
    {
        /// <summary>
        /// Asset pair Id, ex. BTCUSD
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// True if the base asset is quoting in the pair.
        /// </summary>
        /// <remarks>
        /// Base asset is the one that is common in two source pairs used for cross-rate calculating.<br/>
        /// Ex: ETHUSD is calculated based on BTC from ETHBTC (will have this prop = true) and BTCUSD (will have false).
        /// </remarks>
        public bool IsCrossRateBaseAssetQuoting { get; }

        public CrossRateSourceAssetPair(string id, bool isCrossRateBaseAssetQuoting)
        {
            Id = id ?? throw new System.ArgumentNullException(nameof(id));
            IsCrossRateBaseAssetQuoting = isCrossRateBaseAssetQuoting;
        }
    }
}