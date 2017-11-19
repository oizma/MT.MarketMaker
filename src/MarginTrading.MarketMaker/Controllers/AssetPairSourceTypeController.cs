﻿using System.Collections.Immutable;
using System.Threading.Tasks;
using JetBrains.Annotations;
using MarginTrading.MarketMaker.Enums;
using MarginTrading.MarketMaker.Services.Common;
using Microsoft.AspNetCore.Mvc;

namespace MarginTrading.MarketMaker.Controllers
{
    [Route("api/[controller]")]
    public class AssetPairSourceTypeController : Controller
    {
        private readonly IAssetPairSourceTypeService _assetPairSourceTypeService;

        public AssetPairSourceTypeController(IAssetPairSourceTypeService assetPairSourceTypeService)
        {
            _assetPairSourceTypeService = assetPairSourceTypeService;
        }

        /// <summary>
        /// Gets all existing settings
        /// </summary>
        [HttpGet]
        public ImmutableDictionary<string, AssetPairQuotesSourceTypeEnum> List()
        {
            return _assetPairSourceTypeService.Get();
        }

        /// <summary>
        /// Gets settings for a single asset pair
        /// </summary>
        [CanBeNull]
        [HttpGet]
        [Route("{assetPairId}")]
        public AssetPairQuotesSourceTypeEnum? Get(string assetPairId)
        {
            return _assetPairSourceTypeService.Get(assetPairId);
        }

        /// <summary>
        /// Inserts settings for an asset pair
        /// </summary>
        [HttpPut]
        [Route("{assetPairId}")]
        public async Task<IActionResult> Add(string assetPairId, AssetPairQuotesSourceTypeEnum sourceType)
        {
            await _assetPairSourceTypeService.AddAssetPairQuotesSourceAsync(assetPairId, sourceType);
            return Ok(new {success = true});
        }

        /// <summary>
        /// Updates settings for an asset pair
        /// </summary>
        [HttpPost]
        [Route("{assetPairId}")]
        public async Task<IActionResult> Update(string assetPairId, AssetPairQuotesSourceTypeEnum sourceType)
        {
            await _assetPairSourceTypeService.UpdateAssetPairQuotesSourceAsync(assetPairId, sourceType);
            return Ok(new {success = true});
        }
    }
}