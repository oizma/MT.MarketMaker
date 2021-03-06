﻿using System.Collections.Immutable;

namespace MarginTrading.MarketMaker.Services.ExtPrices
{
    public interface IDisabledOrderbooksService
    {
        ImmutableHashSet<string> GetDisabledExchanges(string assetPairId);
        void Disable(string assetPairId, ImmutableHashSet<string> exchanges, string reason);
    }
}