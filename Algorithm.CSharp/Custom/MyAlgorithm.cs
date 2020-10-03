using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Algorithm;
using QuantConnect.Algorithm.Framework.Alphas;
using QuantConnect.Algorithm.Framework.Execution;
using QuantConnect.Algorithm.Framework.Portfolio;
using QuantConnect.Algorithm.Framework.Selection;
using QuantConnect.Data.Fundamental;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Orders;


namespace QuantConnect
{
	public class MyAlgorithm : QCAlgorithm
	{
		public override void Initialize()
		{
			SetStartDate(2019, 1, 1);
			SetEndDate(2020, 12, 30);
			SetCash(100000);

			var sectors = new[]{
					   MorningstarSectorCode.Technology,
					   MorningstarSectorCode.FinancialServices,
					   MorningstarSectorCode.Healthcare
				   };

			UniverseSettings.Resolution = Resolution.Daily;
			SetUniverseSelection(new GrowthQualitySelectionModel(sectors));
			SetAlpha(new ConstantAlphaModel(InsightType.Price, InsightDirection.Up, TimeSpan.FromDays(1)));
			SetPortfolioConstruction(new SectorWeightingPortfolioConstructionModel(Resolution.Daily));
			SetExecution(new ImmediateExecutionModel());
		}


		public override void OnOrderEvent(OrderEvent orderEvent)
		{
			Debug(Time + " " + orderEvent);
		}

		public override void OnEndOfAlgorithm()
		{
			Log($"{Time} - TotalPortfolioValue: {Portfolio.TotalPortfolioValue}");
			Log($"{Time} - CashBook: {Portfolio.CashBook}");
		}
	}

	public class GrowthQualitySelectionModel : FundamentalUniverseSelectionModel
	{
		private readonly int[] _sectors;
		public GrowthQualitySelectionModel(int[] sectors)
			: base(true)
		{
			_sectors = sectors;
		}

		public override IEnumerable<Symbol> SelectCoarse(QCAlgorithm algorithm, IEnumerable<CoarseFundamental> coarse)
		{
			//if (algorithm.Time.DayOfWeek != DayOfWeek.Tuesday)
			//	return Universe.Unchanged;

			return coarse.Where(c => c.HasFundamentalData).Select(c => c.Symbol);
		}
		public override IEnumerable<Symbol> SelectFine(QCAlgorithm algorithm, IEnumerable<FineFundamental> fine)
		{
			return fine/*.Where(f => f.OperationRatios.NetMargin.OneYear > 30 &&
								   f.OperationRatios.GrossMargin5YrAvg > 50
								   //f.OperationRatios.DebttoAssets.OneYear < 0.3m &&
								   //f.OperationRatios.RevenueGrowth.FiveYears > 5 &&
								  // f.OperationRatios.TotalAssetsGrowth.FiveYears > 5 &&
								   //f.OperationRatios.NetIncomeGrowth.FiveYears > 5
				//   f.MarketCap < 10000000000 
				)*/
				.OrderByDescending(f => f.OperationRatios.ROIC)
				.Select(i => i.Symbol);
		}
	}

	public class SectorWeightingPortfolioConstructionModel : EqualWeightingPortfolioConstructionModel
	{
		private readonly IDictionary<int, List<Symbol>> _sectors = new Dictionary<int, List<Symbol>>();
		public SectorWeightingPortfolioConstructionModel(Resolution resolution)
			: base(resolution)
		{
		}

		public override void OnSecuritiesChanged(QCAlgorithm algorithm, SecurityChanges changes)
		{
			foreach (var security in changes.AddedSecurities)
			{
				var sector = security.Fundamentals.AssetClassification.MorningstarSectorCode;
				List<Symbol> sectorSymbols;
				if (!_sectors.TryGetValue(sector, out sectorSymbols))
				{
					sectorSymbols = new List<Symbol>();
				}
				_sectors.Add(sector, sectorSymbols);
			}

			foreach (var security in changes.RemovedSecurities)
			{
				var sector = security.Fundamentals.AssetClassification.MorningstarSectorCode;
				List<Symbol> sectorSymbols;
				if (!_sectors.TryGetValue(sector, out sectorSymbols))
					continue;
				sectorSymbols.Remove(security.Symbol);
				if (!sectorSymbols.Any())
					_sectors.Remove(sector);
			}
			base.OnSecuritiesChanged(algorithm, changes);
		}

		protected override Dictionary<Insight, double> DetermineTargetPercent(List<Insight> activeInsights)
		{
			var sectorBuyingPower = 1m / _sectors.Count;
			var insightsInSectors = new List<Insight>();
			foreach (var kvp in _sectors)
			{
				insightsInSectors = activeInsights.Where(i => kvp.Value.Contains(i.Symbol)).Select(i => i).ToList();
			}

			var percent = sectorBuyingPower / insightsInSectors.Count;
			var determineTargetPercent = insightsInSectors.ToDictionary(i => i, i => (double)((int)i.Direction * percent));
			//Debug("haha");
			return determineTargetPercent;
		}
	}

}