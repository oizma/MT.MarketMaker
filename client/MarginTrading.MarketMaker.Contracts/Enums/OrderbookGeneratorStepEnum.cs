﻿namespace MarginTrading.MarketMaker.Contracts.Enums
{
    public enum OrderbookGeneratorStepEnum
    {
        FindOutdated = 30,
        FindOutliers = 40,
        FindRepeatedProblems = 50,
        ChoosePrimary = 60,
        GetArbitrageFreeSpread = 70,
        Transform = 80
    }
}