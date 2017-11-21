﻿using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using MarginTrading.MarketMaker.Infrastructure.Implementation;
using MarginTrading.MarketMaker.Models.Api;
using MarginTrading.MarketMaker.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MarginTrading.MarketMaker.Controllers
{
    [Route("api/[controller]")]
    public class ExtPriceStatusController : Controller
    {
        private readonly IExtPricesStatusService _extPricesStatusService;

        public ExtPriceStatusController(IExtPricesStatusService extPricesStatusService)
        {
            _extPricesStatusService = extPricesStatusService;
        }

        /// <summary>
        /// Gets all status
        /// </summary>
        [HttpGet]
        [Route("")]
        [SwaggerOperation("GetAllExtPriceStatuses")]
        public IReadOnlyDictionary<string, IReadOnlyList<ExtPriceStatusModel>> GetAllStatuses()
        {
            return _extPricesStatusService.GetAll();
        }

        /// <summary>
        /// Gets status for a single asset pair
        /// </summary>
        [HttpGet]
        [Route("{assetPairId}")]
        [SwaggerOperation("GetExtPriceStatus")]
        [CanBeNull]
        public IReadOnlyList<ExtPriceStatusModel> GetStatus(string assetPairId)
        {
            return _extPricesStatusService.Get(assetPairId);
        }

        /// <summary>
        /// Gets logs
        /// </summary>
        [HttpGet]
        [Route("logs")]
        [SwaggerOperation("GetLogs")]
        [CanBeNull]
        public List<LogModel> GetLogs()
        {
            return Trace.GetLast();
        }

        /// <summary>
        /// Gets logs for asset pair
        /// </summary>
        [HttpGet]
        [Route("logs/{contains}")]
        [SwaggerOperation("GetLogsFiltered")]
        [CanBeNull]
        public IEnumerable<LogModel> GetLogsFiltered(string contains)
        {
            return Trace.GetLast().Where(l => l.Group.Contains(contains) || l.Message.Contains(contains));
        }
    }
}