﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using JetBrains.Annotations;
using MarginTrading.MarketMaker.Enums;
using MarginTrading.MarketMaker.Models;

namespace MarginTrading.MarketMaker.Services.ExtPrices
{
    public interface IPrimaryExchangeService
    {
        [CanBeNull]
        ExchangeQuality GetPrimaryExchange(string assetPairId, ImmutableDictionary<string, ExchangeErrorStateDomainEnum> errors, DateTime now, string currentProcessingExchange);

        [CanBeNull, Pure]
        string GetLastPrimaryExchange(string assetPairId);

        [Pure]
        IReadOnlyDictionary<string, ImmutableDictionary<string, ExchangeQuality>> GetQualities();

        [Pure]
        IReadOnlyDictionary<string, string> GetLastPrimaryExchanges();

        [Pure]
        ImmutableDictionary<string, ExchangeQuality> GetQualities(string assetPairId);
    }
}