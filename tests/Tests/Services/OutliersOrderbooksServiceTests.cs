﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Common;
using FluentAssertions;
using MarginTrading.MarketMaker.Models;
using MarginTrading.MarketMaker.Services;
using MarginTrading.MarketMaker.Services.Implementation;
using Moq;
using MoreLinq;
using NUnit.Framework;

namespace Tests.Services
{
    public class OutliersOrderbooksServiceTests
    {
        private static readonly TestSuit<OutliersOrderbooksService> _testSuit = TestSuit.Create<OutliersOrderbooksService>();

        [TearDown]
        public void TearDown()
        {
            _testSuit.Reset();
        }

        private static IEnumerable<TestCaseData> GetCasesForPrices(decimal[] prices, params (decimal Threshold, int[] Result)[] others)
        {
            foreach (var (threshold, result) in others)
                yield return new TestCaseData(prices, threshold).Returns(result);
        }

        public static IEnumerable<TestCaseData> GetCases() {

            return
                // encompassing, odd exchanges count
                GetCasesForPrices(new decimal[] {100, 101, 102, 103, 100, 103, 99, 101, 70, 110},
                    (0.02m, new[] {2, 5}), (0.03m, new[] {5}), (0.3m, new[] {5}), (0.301m, new int[0]))
                ;
        }

        [TestCaseSource(nameof(GetCases))]
        public IEnumerable<int> Always_ShouldReturnValidResults(decimal[] prices, decimal threshold)
        {
            //arrange
            var validOrderbooks = prices.ToChunks(2).Select((p, i) =>
            {
                var exchangeName = (i + 1).ToString();
                var orderbook = CreateOrderbook(exchangeName);
                _testSuit.Setup<IBestPricesService>(m =>
                    m.Setup(s => s.Calc(orderbook)).Returns(new BestPrices(p.First(), p.Last())));
                return KeyValuePair.Create(exchangeName, orderbook);
            }).ToImmutableDictionary();

            _testSuit.Setup<IPriceCalcSettingsService>(s => s.GetOutlierThreshold("pair") == threshold);
            _testSuit.Setup<IPrimaryExchangeService>(s => s.GetLastPrimaryExchange("pair") == "ex1");

            //act
            var result = _testSuit.Sut.FindOutliers("pair", validOrderbooks);

            //assert
            return result.Select(r => int.Parse(r.ExchangeName)).ToArray();
        }

        private static ExternalOrderbook CreateOrderbook(string exchangeName)
        {
            return new ExternalOrderbook("pair", exchangeName, DateTime.Now,
                ImmutableArray.Create<OrderbookPosition>(), ImmutableArray.Create<OrderbookPosition>());
        }
    }
}
