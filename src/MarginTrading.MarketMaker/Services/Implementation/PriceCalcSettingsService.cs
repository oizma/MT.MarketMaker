﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using MarginTrading.MarketMaker.AzureRepositories;
using MarginTrading.MarketMaker.AzureRepositories.Entities;
using MarginTrading.MarketMaker.Enums;
using MarginTrading.MarketMaker.Infrastructure.Implementation;
using MarginTrading.MarketMaker.Models;
using MarginTrading.MarketMaker.Models.Api;
using MoreLinq;
using Rocks.Caching;

namespace MarginTrading.MarketMaker.Services.Implementation
{
    internal class PriceCalcSettingsService : IPriceCalcSettingsService
    {
        // do not update existing entities instances!
        private static readonly TimeSpan DefaultMinOrderbooksSendingPeriod = TimeSpan.FromSeconds(1) / 2;

        private readonly ReadWriteLockedDictionary<string, ImmutableDictionary<string, ExchangeExtPriceSettingsEntity>>
            _exchangesCache = new ReadWriteLockedDictionary<string, ImmutableDictionary<string, ExchangeExtPriceSettingsEntity>>();

        private readonly CachedEntityAccessorService<AssetPairExtPriceSettingsEntity> _assetPairsCachedAccessor;
        private readonly IExchangeExtPriceSettingsRepository _exchangesRepository;
        private readonly IAssetsPairsExtPriceSettingsRepository _assetPairsRepository;
        private readonly IAlertService _alertService;

        public PriceCalcSettingsService(ICacheProvider cache,
            IAssetsPairsExtPriceSettingsRepository assetPairsRepository,
            IExchangeExtPriceSettingsRepository exchangesRepository,
            IAlertService alertService)
        {
            _exchangesRepository = exchangesRepository;
            _alertService = alertService;
            _assetPairsRepository = assetPairsRepository;
            _assetPairsCachedAccessor =
                new CachedEntityAccessorService<AssetPairExtPriceSettingsEntity>(cache, assetPairsRepository);
        }

        public bool IsStepEnabled(OrderbookGeneratorStepEnum step, string assetPairId)
        {
            return GetAsset(assetPairId).Steps.GetValueOrDefault(step, true);
        }

        public string GetPresetPrimaryExchange(string assetPairId)
        {
            return GetAsset(assetPairId).PresetDefaultExchange;
        }

        public decimal GetVolumeMultiplier(string assetPairId, string exchangeName)
        {
            return (decimal) GetExchange(assetPairId, exchangeName).OrderGeneration.VolumeMultiplier;
        }

        public TimeSpan GetOrderbookOutdatingThreshold(string assetPairId, string exchangeName, DateTime now)
        {
            return GetExchange(assetPairId, exchangeName).OrderbookOutdatingThreshold;
        }

        public RepeatedOutliersParams GetRepeatedOutliersParams(string assetPairId)
        {
            var p = GetAsset(assetPairId).RepeatedOutliers;
            return new RepeatedOutliersParams(p.MaxSequenceLength, p.MaxSequenceAge, (decimal) p.MaxAvg, p.MaxAvgAge);
        }

        public decimal GetOutlierThreshold(string assetPairId)
        {
            return (decimal) GetAsset(assetPairId).OutlierThreshold;
        }

        public ImmutableDictionary<string, decimal> GetHedgingPreferences(string assetPairId)
        {
            return GetAllExchanges(assetPairId).ToImmutableDictionary(e => e.Key,
                e => e.Value.Hedging.IsTemporarilyUnavailable ? 0m : (decimal) e.Value.Hedging.DefaultPreference);
        }

        public (decimal Bid, decimal Ask) GetPriceMarkups(string assetPairId)
        {
            var markups = GetAsset(assetPairId).Markups;
            return ((decimal) markups.Bid, (decimal) markups.Ask);
        }

        public IReadOnlyList<HedgingPreferenceModel> GetAllHedgingPreferences()
        {
            return _exchangesCache.Values.SelectMany(ap =>
                ap.Values.Select(e => new HedgingPreferenceModel
                {
                    AssetPairId = e.AssetPairId,
                    Exchange = e.Exchange,
                    Preference = (decimal) e.Hedging.DefaultPreference,
                    HedgingTemporarilyDisabled = e.Hedging.IsTemporarilyUnavailable,
                })).ToList();
        }

        public ImmutableHashSet<string> GetDisabledExchanges(string assetPairId)
        {
            return GetAllExchanges(assetPairId).Where(p => p.Value.Disabled.IsTemporarilyDisabled).Select(p => p.Key)
                .ToImmutableHashSet();
        }

        public void ChangeExchangesTemporarilyDisabled(string assetPairId, ImmutableHashSet<string> exchanges,
            bool disable, string reason)
        {
            if (exchanges.Count == 0)
                return;

            _exchangesCache.UpdateIfExists(assetPairId,
                (k, old) =>
                {
                    //todo: replace with copying
                    var toUpdate = old.Values.Where(o => exchanges.Contains(o.Exchange))
                        .Pipe(p =>
                        {
                            // and hope races won't break anything
                            p.Disabled = new ExchangeExtPriceSettingsEntity.DisabledSettings
                            {
                                IsTemporarilyDisabled = true,
                                Reason = reason,
                            };
                        });
                    _exchangesRepository.InsertOrReplaceAsync(toUpdate).GetAwaiter().GetResult();
                    return old;
                });
            ExchangesTemporarilyDisabledChanged(assetPairId, exchanges, disable, reason);
        }

        private void ExchangesTemporarilyDisabledChanged(string assetPairId, IEnumerable<string> exchanges, bool disable, string reason)
        {
            ExchangesStateChanged(assetPairId, exchanges, disable ? "disabled" : "enabled", reason);
        }

        private void ExchangesStateChanged(string assetPairId, IEnumerable<string> exchanges, string stateChangeDescription, string reason)
        {
            var exchangesStr = string.Join(", ", exchanges);
            if (exchangesStr == string.Empty)
            {
                return;
            }

            var ending = exchangesStr.Contains(',') ? "s" : "";

            _alertService.AlertRiskOfficer(assetPairId, $"Exchange{ending} {exchangesStr} for {assetPairId} became {stateChangeDescription} because {reason}");
        }

        public bool IsExchangeConfigured(string assetPairId, string exchange)
        {
            return GetAllExchanges(assetPairId).GetValueOrDefault(exchange) != null;
        }

        public TimeSpan GetMinOrderbooksSendingPeriod(string assetPairId)
        {
            return GetAsset(assetPairId).MinOrderbooksSendingPeriod ?? DefaultMinOrderbooksSendingPeriod;
        }

        public async Task<IReadOnlyList<AssetPairExtPriceSettingsModel>> GetAllAsync(string assetPairId = null)
        {
            IEnumerable<AssetPairExtPriceSettingsEntity> assetPairsEntities;
            if (assetPairId == null)
                assetPairsEntities = await _assetPairsRepository.GetAllAsync();
            else
                assetPairsEntities =
                    new[] {_assetPairsCachedAccessor.GetByKey(GetAssetPairKeys(assetPairId))}.Where(e => e != null);

            return assetPairsEntities
                .GroupJoin(await _exchangesRepository.GetAllAsync(), ap => ap.AssetPairId, e => e.AssetPairId, Convert)
                .ToList();
        }

        public Task Set(AssetPairExtPriceSettingsModel model)
        {
            var entity = Convert(model, TryGetAsset(model.AssetPairId));
            entity.Timestamp = DateTimeOffset.UtcNow;

            var upsertAssetPairTask = _assetPairsCachedAccessor.Upsert(entity);

            var exchangesEntities = model.Exchanges.Select(e => Convert(e, entity)).ToImmutableDictionary(e => e.Exchange);
            ImmutableDictionary<string, ExchangeExtPriceSettingsEntity> oldExchangesEntities = ImmutableDictionary<string, ExchangeExtPriceSettingsEntity>.Empty;

            // do not update existing entities instances!
            _exchangesCache.AddOrUpdate(entity.AssetPairId,
                k =>
                {
                    _exchangesRepository.InsertOrReplaceAsync(exchangesEntities.Values).GetAwaiter().GetResult();
                    return exchangesEntities;
                },
                (k, old) =>
                {
                    oldExchangesEntities = old;
                    Task.WaitAll(
                        _exchangesRepository.InsertOrReplaceAsync(exchangesEntities.Values),
                        _exchangesRepository.DeleteAsync(old.Values.Where(o =>
                            !exchangesEntities.ContainsKey(o.Exchange))));
                    return exchangesEntities;
                });

            const string reason = "settings was manually changed";
            ExchangesTemporarilyDisabledChanged(entity.AssetPairId, oldExchangesEntities.Values, exchangesEntities.Values, reason);
            CanPerformHedgingChanged(entity.AssetPairId, oldExchangesEntities.Values, exchangesEntities.Values, reason);
            return upsertAssetPairTask;
        }

        private void CanPerformHedgingChanged(string assetPairId,
            IEnumerable<ExchangeExtPriceSettingsEntity> oldEntities,
            IEnumerable<ExchangeExtPriceSettingsEntity> newEntities, string reason)
        {
            var (hedgingOn, hedgingOff) = oldEntities.FindChanges(newEntities, e => e.Exchange,
                    CanPerformHedging,
                    (o, n) => o == n)
                .Where(t => t.New != null)
                .Partition(t => CanPerformHedging(t.New));

            ExchangesStateChanged(assetPairId, hedgingOn.Select(t => t.New.Exchange), "available for hedging", reason);
            ExchangesStateChanged(assetPairId, hedgingOff.Select(t => t.New.Exchange), "unavailable for hedging", reason);
        }

        private void ExchangesTemporarilyDisabledChanged(string assetPairId,
            IEnumerable<ExchangeExtPriceSettingsEntity> oldEntities,
            IEnumerable<ExchangeExtPriceSettingsEntity> newEntities, string reason)
        {
            var (disable, enable) = oldEntities.FindChanges(newEntities, e => e.Exchange,
                    e => e.Disabled.IsTemporarilyDisabled,
                    (o, n) => o == n)
                .Where(t => t.New != null)
                .Partition(ex => ex.New.Disabled.IsTemporarilyDisabled);
            ExchangesTemporarilyDisabledChanged(assetPairId, disable.Select(t => t.New.Exchange), true, reason);
            ExchangesTemporarilyDisabledChanged(assetPairId, enable.Select(t => t.New.Exchange), false, reason);
        }

        private static bool CanPerformHedging(ExchangeExtPriceSettingsEntity e)
        {
            return e.Hedging.DefaultPreference * (e.Hedging.IsTemporarilyUnavailable ? 0 : 1) > 0;
        }

        private ImmutableDictionary<string, ExchangeExtPriceSettingsEntity> GetAllExchanges(string assetPairId)
        {
            return _exchangesCache.GetOrAdd(assetPairId,
                k => _exchangesRepository.GetAsync(k).GetAwaiter().GetResult().ToImmutableDictionary(e => e.Exchange));
        }

        private ExchangeExtPriceSettingsEntity GetExchange(string assetPairId, string exchange)
        {
            return GetAllExchanges(assetPairId).GetValueOrDefault(exchange)
                   ?? throw new InvalidOperationException(
                       $"Settings for exchange {exchange} for asset pair {assetPairId} not found");
        }

        private AssetPairExtPriceSettingsEntity GetAsset(string assetPairId)
        {
            return TryGetAsset(assetPairId)
                   ?? throw new InvalidOperationException($"Settings for asset pair {assetPairId} not found");
        }

        [CanBeNull]
        private AssetPairExtPriceSettingsEntity TryGetAsset(string assetPairId)
        {
            return _assetPairsCachedAccessor.GetByKey(GetAssetPairKeys(assetPairId));
        }

        private static CachedEntityAccessorService.EntityKeys GetAssetPairKeys(string assetPairId)
        {
            return new CachedEntityAccessorService.EntityKeys(AssetPairExtPriceSettingsEntity.GeneratePartitionKey(),
                AssetPairExtPriceSettingsEntity
                    .GenerateRowKey(assetPairId));
        }

        private static AssetPairExtPriceSettingsModel Convert(AssetPairExtPriceSettingsEntity assetPair, IEnumerable<ExchangeExtPriceSettingsEntity> exchanges)
        {
            return new AssetPairExtPriceSettingsModel
            {
                AssetPairId = assetPair.AssetPairId,
                Timestamp = assetPair.Timestamp,
                PresetDefaultExchange = assetPair.PresetDefaultExchange,
                MinOrderbooksSendingPeriod = assetPair.MinOrderbooksSendingPeriod ?? DefaultMinOrderbooksSendingPeriod,
                RepeatedOutliers = new RepeatedOutliersParamsModel
                {
                    MaxSequenceLength = assetPair.RepeatedOutliers.MaxSequenceLength,
                    MaxSequenceAge = assetPair.RepeatedOutliers.MaxSequenceAge,
                    MaxAvg = (decimal)assetPair.RepeatedOutliers.MaxAvg,
                    MaxAvgAge = assetPair.RepeatedOutliers.MaxAvgAge,
                },
                Markups = new MarkupsModel
                {
                    Bid = (decimal)assetPair.Markups.Bid,
                    Ask = (decimal)assetPair.Markups.Ask,
                },
                OutlierThreshold = assetPair.OutlierThreshold,
                Steps = ConvertSteps(assetPair.Steps),
                Exchanges = exchanges.Select(e => new ExchangeExtPriceSettingsModel
                {
                    Exchange = e.Exchange,
                    Hedging = new HedgingSettingsModel
                    {
                        DefaultPreference = e.Hedging.DefaultPreference,
                        IsTemporarilyUnavailable = e.Hedging.IsTemporarilyUnavailable,
                    },
                    OrderGeneration = new OrderGenerationSettingsModel
                    {
                        VolumeMultiplier = (decimal)e.OrderGeneration.VolumeMultiplier,
                        OrderRenewalDelay = e.OrderGeneration.OrderRenewalDelay,
                    },
                    OrderbookOutdatingThreshold = e.OrderbookOutdatingThreshold,
                    Disabled = new DisabledSettingsModel
                    {
                        IsTemporarilyDisabled = e.Disabled.IsTemporarilyDisabled,
                        Reason = e.Disabled.Reason,
                    }
                }).ToList()
            };
        }

        private static ImmutableDictionary<OrderbookGeneratorStepEnum, bool> ConvertSteps(ImmutableDictionary<OrderbookGeneratorStepEnum, bool> steps)
        {
            return Enum.GetValues(typeof(OrderbookGeneratorStepEnum)).Cast<OrderbookGeneratorStepEnum>()
                .ToImmutableDictionary(e => e, e => steps.GetValueOrDefault(e, true));
        }

        private static AssetPairExtPriceSettingsEntity Convert(AssetPairExtPriceSettingsModel model, [CanBeNull] AssetPairExtPriceSettingsEntity oldEntity)
        {
            return new AssetPairExtPriceSettingsEntity
            {
                AssetPairId = model.AssetPairId,
                Timestamp = model.Timestamp,
                PresetDefaultExchange = model.PresetDefaultExchange,
                MinOrderbooksSendingPeriod = model.MinOrderbooksSendingPeriod ?? oldEntity?.MinOrderbooksSendingPeriod,
                RepeatedOutliers = new AssetPairExtPriceSettingsEntity.RepeatedOutliersParams
                {
                    MaxSequenceLength = model.RepeatedOutliers.MaxSequenceLength,
                    MaxSequenceAge = model.RepeatedOutliers.MaxSequenceAge,
                    MaxAvg = (double) model.RepeatedOutliers.MaxAvg,
                    MaxAvgAge = model.RepeatedOutliers.MaxAvgAge,
                },
                Markups = new AssetPairExtPriceSettingsEntity.MarkupsParams
                {
                    Bid = (double) model.Markups.Bid,
                    Ask = (double) model.Markups.Ask,
                },
                OutlierThreshold = model.OutlierThreshold,
                Steps = model.Steps,
            };
        }

        private static ExchangeExtPriceSettingsEntity Convert(ExchangeExtPriceSettingsModel e, AssetPairExtPriceSettingsEntity entity)
        {
            return new ExchangeExtPriceSettingsEntity
            {
                Exchange = e.Exchange,
                AssetPairId = entity.AssetPairId,
                Hedging = new ExchangeExtPriceSettingsEntity.HedgingSettings
                {
                    DefaultPreference = e.Hedging.DefaultPreference,
                    IsTemporarilyUnavailable = e.Hedging.IsTemporarilyUnavailable,
                },
                OrderGeneration = new ExchangeExtPriceSettingsEntity.OrderGenerationSettings
                {
                    VolumeMultiplier = (double)e.OrderGeneration.VolumeMultiplier,
                    OrderRenewalDelay = e.OrderGeneration.OrderRenewalDelay,
                },
                OrderbookOutdatingThreshold = e.OrderbookOutdatingThreshold,
                Disabled = new ExchangeExtPriceSettingsEntity.DisabledSettings
                {
                    IsTemporarilyDisabled = e.Disabled.IsTemporarilyDisabled,
                    Reason = e.Disabled.Reason,
                }
            };
        }
    }
}