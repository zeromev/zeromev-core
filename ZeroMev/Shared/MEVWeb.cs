using System;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace ZeroMev.Shared
{
    public enum MEVType
    {
        None,
        Swap,
        Swaps,
        Frontrun,
        Sandwich,
        Backrun,
        Arb,
        Liquid,
        NFT,
        PunkBid,
        PunkAccept,
        PunkSnipe,
        UserSwapVolume,
        UserSandwichedSwapVolume,
        ExtractorSwapVolume
    }

    public enum MEVClass
    {
        All,
        Unclassified,
        Positive,
        Neutral,
        Toxic,
        Info
    }

    public enum MEVFilter
    {
        All,
        Info,
        Toxic,
        Other
    }

    public enum MEVError
    {
        None,
        Unknown,
        Reverted,
        OutOfGas
    }

    public enum ProtocolSwap
    {
        Unknown,
        Uniswap2,
        Uniswap3,
        Curve,
        ZeroX,
        Balancer1,
        Bancor,
        Multiple
    }

    public enum ProtocolLiquidation
    {
        Unknown,
        Aave,
        CompoundV2
    }

    public enum ProtocolNFT
    {
        Unknown,
        Opensea
    }

    public interface IMEV
    {
        int? TxIndex { get; } // preferred
        string? TxHash { get; } // suppied if TxIndex not available
        MEVType MEVType { get; }
        MEVClass MEVClass { get; }
        decimal? MEVAmountUsd { get; set; }
        string? MEVDetail { get; set; }
        string? ActionSummary { get; }
        string? ActionDetail { get; }
        void Cache(MEVBlock mevBlock, int mevIndex); // mevIndex is allows mev instances to find other related instances, eg: backrun can cheaply find related sandwiched and frontun txs without duplicating them
        int? AddSwap(MEVSwap swap);
    }

    public class Symbol
    {
        public const int UnknownSymbolIndex = -1;
        public const int EthSymbolIndex = -2;
        public const string UnknownImage = @"/un.png";
        public static Symbol EthSymbol = new Symbol("Eth", "eth.png", "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2");

        public Symbol()
        {
        }

        public Symbol(string name, string image, string tokenAddress)
        {
            Name = name;
            Image = image;
            TokenAddress = tokenAddress;
        }

        [JsonPropertyName("n")]
        public string Name { get; set; }

        [JsonPropertyName("i")]
        public string Image { get; set; }

        [JsonIgnore] // to save storage
        public string TokenAddress { get; set; }

        public string GetImage()
        {
            if (Image == null) return Symbol.UnknownImage;
            return Image;
        }

        public string GetImageUrl()
        {
            if (Image == null) return Symbol.UnknownImage;
            return Config.Settings.ImagesUrl + Image;
        }
    }

    public class MEVLiteCache
    {
        public const int CachedMevBlockCount = 300;

        [JsonPropertyName("l")]
        public long? LastBlockNumber = null;

        [JsonPropertyName("r")]
        public List<MEVLiteBlock> Blocks = new List<MEVLiteBlock>();

        [JsonIgnore]
        public MEVSummary[] Totals = new MEVSummary[Enum.GetValues(typeof(MEVFilter)).Length];

        [JsonConstructor]
        public MEVLiteCache()
        {
        }

        public void CalculateSummaries()
        {
            Array.Clear(Totals);

            for (int i = 0; i < Blocks.Count; i++)
            {
                MEVLiteBlock mb = Blocks[i];
                mb.IsFirst = (i == 0);
                mb.CalculateSummaries(Totals);
            }
        }

        public string Duration()
        {
            if (Blocks == null || Blocks.Count == 0) return "0 secs";
            if (Blocks[0].BlockTime == null || Blocks[1].BlockTime == null) return "?";
            TimeSpan ts = Blocks[0].BlockTime.Value - Blocks[Blocks.Count - 1].BlockTime.Value;
            return ZMTx.DurationStr(ts);
        }

        public string BlockCount()
        {
            if (Blocks == null) return "0";
            return Blocks.Count.ToString();
        }
    }

    public class MEVLiteBlock
    {
        [JsonConstructor]
        public MEVLiteBlock()
        {
        }

        public MEVLiteBlock(long blockNumber, DateTime? blockTime)
        {
            BlockNumber = blockNumber;
            BlockTime = blockTime;
        }

        [JsonPropertyName("b")]
        public long BlockNumber { get; set; }

        [JsonPropertyName("t")]
        public DateTime? BlockTime { get; set; }

        [JsonPropertyName("m")]
        public List<MEVLite> MEVLite { get; set; }

        public bool IsFirst { get; set; }

        [JsonIgnore]
        public MEVSummary[] MEVSummaries;

        public void CalculateSummaries(MEVSummary[] totals)
        {
            MEVSummaries = new MEVSummary[Enum.GetValues(typeof(MEVFilter)).Length];
            foreach (var m in MEVLite)
            {
                MEVWeb.UpdateSummaries((IMEV)m, MEVSummaries);
                MEVWeb.UpdateSummaries((IMEV)m, totals);
            }
        }
    }

    public class MEVLite : IMEV
    {
        public MEVLite(IMEV mev)
        {
            MEVType = mev.MEVType;
            MEVClass = mev.MEVClass;
            MEVAmountUsd = mev.MEVAmountUsd;
        }

        [JsonConstructor]
        public MEVLite()
        {
        }

        [JsonIgnore]
        public int? TxIndex => null;

        [JsonIgnore]
        public string TxHash => null;

        [JsonPropertyName("m")]
        public MEVType MEVType { get; set; } = MEVType.None;

        [JsonPropertyName("c")]
        public MEVClass MEVClass { get; set; } = MEVClass.All;

        [JsonPropertyName("u")]
        public decimal? MEVAmountUsd { get; set; } = null;

        [JsonIgnore]
        public string? MEVDetail { get; set; } = null;

        [JsonIgnore]
        public string? ActionSummary { get; set; } = null;

        [JsonIgnore]
        public string? ActionDetail { get; set; } = null;

        public void Cache(MEVBlock mevBlock, int mevIndex)
        {
        }

        public int? AddSwap(MEVSwap swap)
        {
            return null;
        }
    }

    public class MEVBlock
    {
        public MEVBlock()
        {
        }

        public MEVBlock(long blockNumber)
        {
            BlockNumber = blockNumber;
        }

        // persisted
        [JsonPropertyName("bn")]
        public long BlockNumber { get; set; }

        [JsonPropertyName("ts")]
        public DateTime? BlockTime { get; set; }

        [JsonPropertyName("sb")]
        public List<Symbol> Symbols { get; set; } = new List<Symbol>();

        [JsonPropertyName("eu")]
        public decimal? EthUsd { get; set; }

        [JsonPropertyName("s")]
        public List<MEVSwapsTx> SwapsTx { get; set; } = new List<MEVSwapsTx>();

        [JsonPropertyName("f")]
        public List<MEVFrontrun> Frontruns { get; set; } = new List<MEVFrontrun>();

        [JsonPropertyName("w")]
        public List<MEVSandwiched[]> Sandwiched { get; set; } = new List<MEVSandwiched[]>();

        [JsonPropertyName("b")]
        public List<MEVBackrun> Backruns { get; set; } = new List<MEVBackrun>();

        [JsonPropertyName("a")]
        public List<MEVArb> Arbs { get; set; } = new List<MEVArb>();

        [JsonPropertyName("l")]
        public List<MEVLiquidation> Liquidations { get; set; } = new List<MEVLiquidation>();

        [JsonPropertyName("n")]
        public List<MEVNFT> NFTrades { get; set; } = new List<MEVNFT>();

        // calculated
        [JsonIgnore]
        public MEVSummary[] MEVSummaries { get; private set; }
        [JsonIgnore]
        public int[] MEVClassCount { get; private set; }
        [JsonIgnore]
        public int[] MEVClassAmount { get; private set; }
        [JsonIgnore]
        public IMEV[] ExistingMEV { get; set; }

        public Symbol? GetSymbol(int symbolIndex)
        {
            if (symbolIndex < 0)
            {
                if (symbolIndex == Symbol.EthSymbolIndex)
                    return Symbol.EthSymbol;
                return null;
            }
            return Symbols[symbolIndex];
        }

        public string? GetSymbolName(int symbolIndex)
        {
            if (symbolIndex < 0)
            {
                if (symbolIndex == Symbol.EthSymbolIndex)
                    return "Eth";
                return "";
            }
            return Symbols[symbolIndex].Name;
        }

        public string? GetImage(int symbolIndex)
        {
            if (symbolIndex < 0)
            {
                if (symbolIndex == Symbol.EthSymbolIndex)
                    return Symbol.EthSymbol.GetImageUrl();
                return Symbol.UnknownImage;
            }
            return Symbols[symbolIndex].GetImageUrl();
        }

        public bool IsEth(int symbolIndex)
        {
            if (symbolIndex == Symbol.UnknownSymbolIndex) return false;
            if (symbolIndex == Symbol.EthSymbolIndex) return true;
            if (Symbols[symbolIndex].Image == null || Symbols[symbolIndex].Image.Length == 0) return false; // probably a fake
            return Symbols[symbolIndex].Name == "WETH";
        }

        public bool IsUsd(int symbolIndex)
        {
            if (symbolIndex == Symbol.UnknownSymbolIndex) return false;
            if (Symbols[symbolIndex].Image == null || Symbols[symbolIndex].Image.Length == 0) return false; // probably a fake
            if (Symbols[symbolIndex].Name == "USD Coin") return true;
            if (Symbols[symbolIndex].Name == "Dai") return true;
            if (Symbols[symbolIndex].Name == "Tether USD") return true;
            return false;
        }
    }

    public class MEVSwapsTx : IMEV
    {
        public MEVSwapsTx()
        {
        }

        public MEVSwapsTx(int? txIndex)
        {
            TxIndex = txIndex;
        }

        [JsonPropertyName("s")]
        public MEVSwaps Swaps { get; set; } = new MEVSwaps();

        [JsonPropertyName("i")]
        public int? TxIndex { get; set; }

        [JsonIgnore]
        public string TxHash => null;

        [JsonIgnore]
        public MEVType MEVType => MEVType.Swap;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Info;

        [JsonIgnore]
        public decimal? MEVAmountUsd { get; set; }

        [JsonIgnore]
        public string? MEVDetail { get; set; } = "see action.";

        [JsonIgnore]
        public string? ActionSummary => Swaps.ActionSummary;

        [JsonIgnore]
        public string? ActionDetail => Swaps.ActionDetail;

        public void Cache(MEVBlock mevBlock, int mevIndex)
        {
            Swaps.Cache(mevBlock, mevIndex);
        }

        public int? AddSwap(MEVSwap swap)
        {
            return Swaps.AddSwap(swap);
        }
    }

    public class MEVSwap : IMEV, IComparable<MEVSwap>
    {
        public MEVSwap()
        {
        }

        public MEVSwap(TraceAddress traceAddress, ProtocolSwap protocol, int symbolInIndex, int symbolOutIndex, ZMDecimal? amountIn, ZMDecimal? amountOut, ZMDecimal? inUsdRate, ZMDecimal? outUsdRate, string addressFrom, string addressTo)
        {
            TraceAddress = traceAddress;
            Protocol = protocol;
            SymbolInIndex = symbolInIndex;
            SymbolOutIndex = symbolOutIndex;
            AmountIn = amountIn;
            AmountOut = amountOut;
            AddressFrom = addressFrom;
            AddressTo = addressTo;

            // store the output usd because it's smaller than the BigDecimal rate and generally more useful
            if (inUsdRate.HasValue && amountIn.HasValue) AmountInUsd = (amountIn.Value * inUsdRate.Value).ToUsd();
            if (outUsdRate.HasValue && amountOut.HasValue) AmountOutUsd = (amountOut.Value * outUsdRate.Value).ToUsd();

            if (AmountInUsd > Num.OversizedAmount) AmountInUsd = null;
            if (AmountOutUsd > Num.OversizedAmount) AmountOutUsd = null;
        }

        [JsonIgnore]
        public bool IsKnown
        {
            get
            {
                return SymbolInIndex != Symbol.UnknownSymbolIndex && SymbolOutIndex != Symbol.UnknownSymbolIndex;
            }
        }

        // maintained by the owner parent IMEV instance
        [JsonIgnore]
        public int? TxIndex => null;

        [JsonIgnore]
        public string TxHash => null;

        [JsonPropertyName("t")]
        public TraceAddress TraceAddress { get; set; }

        [JsonPropertyName("a")]
        public int SymbolInIndex { get; set; }

        [JsonPropertyName("b")]
        public int SymbolOutIndex { get; set; }

        [JsonPropertyName("c")]
        public ZMDecimal? AmountIn { get; set; }

        [JsonPropertyName("d")]
        public ZMDecimal? AmountOut { get; set; }

        [JsonPropertyName("e")]
        public decimal? AmountInUsd { get; set; }

        [JsonPropertyName("f")]
        public decimal? AmountOutUsd { get; set; }

        [JsonPropertyName("p")]
        public ProtocolSwap Protocol { get; set; }

        [JsonIgnore]
        public MEVType MEVType => MEVType.Swap;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Info;

        [JsonIgnore]
        public decimal? MEVAmountUsd { get; set; } = null;

        [JsonIgnore]
        public string? MEVDetail { get; set; } = null;

        [JsonIgnore]
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

        [JsonIgnore]
        public bool IsBaseUsdRateIn { get; set; }

        [JsonIgnore]
        public bool IsBaseUsdRateOut { get; set; }
        
        [JsonIgnore]
        public string AddressFrom { get; set; }

        [JsonIgnore]
        public string AddressTo { get; set; }

        public void Cache(MEVBlock mevBlock, int mevIndex)
        {
            RecalculateUsd(mevBlock);

            StringBuilder sb = new StringBuilder();
            BuildActionSummary(mevBlock, sb);
            ActionSummary = sb.ToString();

            sb.Clear();
            BuildActionDetail(mevBlock, sb);
            ActionDetail = sb.ToString();
        }

        public void RecalculateUsd(MEVBlock mevBlock)
        {
            // recalculate usd amounts using base currencies where possible for greater accuracy (especially with low liquidity tokens)

            // usd out
            if (this.AmountOut != null)
            {
                if (mevBlock.IsUsd(this.SymbolOutIndex))
                {
                    this.AmountOutUsd = this.AmountOut.Value.ToUsd();
                    this.IsBaseUsdRateOut = true;
                }
                else if (mevBlock.EthUsd.HasValue && mevBlock.IsEth(this.SymbolOutIndex))
                {
                    this.AmountOutUsd = Math.Round(((decimal)this.AmountOut.Value) * mevBlock.EthUsd.Value, 2);
                    this.IsBaseUsdRateOut = true;
                }
            }

            // usd in
            if (this.AmountIn != null)
            {
                if (mevBlock.IsUsd(this.SymbolInIndex))
                {
                    this.AmountInUsd = this.AmountIn.Value.ToUsd();
                    this.IsBaseUsdRateIn = true;
                }
                else if (mevBlock.EthUsd.HasValue && mevBlock.IsEth(this.SymbolInIndex))
                {
                    this.AmountInUsd = Math.Round(((decimal)this.AmountIn.Value) * mevBlock.EthUsd.Value, 2);
                    this.IsBaseUsdRateIn = true;
                }
            }
        }

        public int? AddSwap(MEVSwap swap)
        {
            return null;
        }

        public ZMDecimal? InUsdRate()
        {
            if (AmountInUsd == null) return null;
            if (AmountIn == null || AmountIn < Num.EpsilonAmount) return null;
            if (AmountInUsd > Num.OversizedAmount) return null;
            return (ZMDecimal)AmountInUsd / AmountIn;
        }

        public ZMDecimal? OutUsdRate()
        {
            if (AmountOutUsd == null) return null;
            if (AmountOut == null || AmountOut < Num.EpsilonAmount) return null;
            if (AmountOutUsd > Num.OversizedAmount) return null;
            return (ZMDecimal)AmountOutUsd / AmountOut;
        }

        public void BuildActionSummary(MEVBlock mevBlock, StringBuilder sb)
        {
            sb.Append("<img src=\"");
            sb.Append(mevBlock.GetImage(SymbolInIndex));
            sb.Append("\" width=\"20\" height=\"20\"><img src=\"swap.svg\" width=\"20\" height=\"20\"><img src=\"");
            sb.Append(mevBlock.GetImage(SymbolOutIndex));
            sb.Append("\" width=\"20\" height=\"20\">");
        }

        public void BuildActionDetail(MEVBlock mevBlock, StringBuilder sb)
        {
            sb.Append(Protocol);
            sb.Append(" ");
            sb.Append(mevBlock.GetSymbolName(SymbolInIndex));
            sb.Append(" ");
            sb.Append((AmountIn ?? 0).Shorten());
            sb.Append(" > ");
            sb.Append(mevBlock.GetSymbolName(SymbolOutIndex));
            sb.Append(" ");
            sb.Append((AmountOut ?? 0).Shorten());
            sb.Append(" ($");
            sb.Append(AmountOutUsd != null ? AmountOutUsd.ToString() : "?");
            sb.Append(")");
        }

        public int CompareTo(MEVSwap other)
        {
            return this.TraceAddress.CompareTo(other.TraceAddress);
        }
    }

    public class MEVSwaps : IMEV
    {
        public MEVSwaps()
        {
        }

        public MEVSwaps(MEVSwap swap)
        {
            Swaps.Add(swap);
        }

        [JsonPropertyName("s")]
        public List<MEVSwap> Swaps { get; set; } = new List<MEVSwap>();

        [JsonIgnore]
        public int? TxIndex => null;

        [JsonIgnore]
        public string TxHash => null;

        [JsonIgnore]
        public MEVType MEVType => MEVType.Swaps;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Info;

        [JsonIgnore]
        public decimal? MEVAmountUsd { get; set; } = null;

        [JsonIgnore]
        public string? MEVDetail { get; set; } = null;

        [JsonIgnore]
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

        public int? AddSwap(MEVSwap swap)
        {
            // add and return if we are the first
            if (Swaps.Count == 0)
            {
                Swaps.Add(swap);
                return 0;
            }

            // swaps are sorted by trace address, so look for where to insert it
            int insertIndex = 0;
            for (int i = 0; i < Swaps.Count; i++)
            {
                var other = Swaps[i];
                int comp = swap.CompareTo(other);

                // return if we already have the swap
                if (comp == 0)
                    return null;

                if (comp > 0)
                    insertIndex = i + 1;
            }

            Swaps.Insert(insertIndex, swap);
            return insertIndex;
        }

        public static int? Shuffle(int? insertedIndex, int? trackIndex)
        {
            if (insertedIndex == null || trackIndex == null)
                return trackIndex;

            if (trackIndex.Value >= insertedIndex)
                return trackIndex.Value + 1;
            return trackIndex.Value;
        }

        public void Cache(MEVBlock mevBlock, int mevIndex)
        {
            foreach (var swap in Swaps)
                swap.RecalculateUsd(mevBlock);

            StringBuilder sb = new StringBuilder();
            BuildActionSummary(mevBlock, sb);
            ActionSummary = sb.ToString();

            sb.Clear();
            BuildActionDetail(mevBlock, sb);
            ActionDetail = sb.ToString();
        }

        public string BuildActionSummary(MEVBlock mevBlock, StringBuilder sb)
        {
            if (Swaps == null || Swaps.Count == 0) return "no swaps";
            Swaps[0].BuildActionSummary(mevBlock, sb);
            if (Swaps.Count > 1)
            {
                sb.Append(" +");
                sb.Append(Swaps.Count - 1);
            }
            sb.AppendLine("");
            return sb.ToString();
        }

        public string BuildActionDetail(MEVBlock mevBlock, StringBuilder sb)
        {
            sb.Append(Swaps.Count);
            sb.AppendLine(" swaps.");
            foreach (var swap in Swaps)
            {
                swap.BuildActionDetail(mevBlock, sb);
                sb.AppendLine("");
            }
            return sb.ToString();
        }
    }

    public class MEVFrontrun : IMEV
    {
        private const string ErrorParameters = "can't get parameters to calculate.";
        private const string ErrorPool = "can't extract AMM pool to calculate.";
        private const string ErrorUndetectedRevert = "possible undetected revert- failed to calculate.";

        public MEVFrontrun()
        {
        }

        public MEVFrontrun(int? txIndex, MEVSwap swap)
        {
            TxIndex = txIndex;
            Swaps = new MEVSwaps(swap);
            FrontrunSwapIndex = 0;
        }

        [JsonPropertyName("s")]
        public MEVSwaps Swaps { get; set; }

        [JsonPropertyName("i")]
        public int? TxIndex { get; set; }

        [JsonPropertyName("f")]
        public int? FrontrunSwapIndex { get; set; }

        [JsonIgnore]
        public MEVSwap Swap => FrontrunSwapIndex != null && Swaps != null ? Swaps.Swaps[FrontrunSwapIndex.Value] : null;

        [JsonIgnore]
        public string TxHash => null;

        [JsonIgnore]
        public MEVType MEVType => MEVType.Frontrun;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Toxic;

        [JsonIgnore]
        public decimal? MEVAmountUsd { get; set; } = null;

        [JsonIgnore]
        public string? MEVDetail { get; set; } = null;

        [JsonIgnore]
        public string? ActionSummary => Swaps.ActionSummary;

        [JsonIgnore]
        public string? ActionDetail => Swaps.ActionDetail;

        [JsonIgnore]
        public decimal? SandwichProfitUsd { get; set; }

        [JsonIgnore]
        public float? FrontrunImbalance { get; set; }

        public void Cache(MEVBlock mevBlock, int mevIndex)
        {
            Swaps.Cache(mevBlock, mevIndex);
            CalculateMev(mevBlock, mevIndex);
        }

        public int? AddSwap(MEVSwap swap)
        {
            var insertedIndex = Swaps.AddSwap(swap);
            FrontrunSwapIndex = MEVSwaps.Shuffle(insertedIndex, FrontrunSwapIndex);
            return null;
        }

        private void CalculateMev(MEVBlock mevBlock, int mevIndex)
        {
            // mev (calculate for itself and all related sandwiched and backrun instances)
            if (!MEVCalc.GetSandwichParameters(mevBlock, mevIndex, out var a, out var b, out var front, out var back, out var sandwiched))
            {
                MEVDetail = ErrorParameters;
                back.MEVDetail = ErrorParameters;
                return;
            }

            // low liquidity and malicious tokens can return bad profit / user loss results
            // these cases are often wrapped in more liquid swaps which allow the attacker to withdraw profits safely
            // (eg: WETH -> malicious token -> sandwich -> malicious token -> WETH) and these wrapping swaps give more accurate results
            // as such, when we detect a wrapped sandwich, use the [wrapped token output] - [wrapped token input] as profit and the neg of this as user loss
            if (MEVCalc.DetectWrappedSandwich(front, back, out var frontWrap, out var backWrap))
            {
                // these sandwiches tend to be well balanced as the attacker is trying to get their money out not take a position
                decimal? wrapProfitUsd = null;
                if (backWrap.AmountOutUsd != null && frontWrap.AmountInUsd != null && backWrap.AmountOut != null && frontWrap.AmountIn != null)
                {
                    wrapProfitUsd = backWrap.AmountOutUsd - frontWrap.AmountInUsd;
                    wrapProfitUsd = Math.Round(wrapProfitUsd.Value, 2);
                    sandwiched[0].MEVAmountUsd = -wrapProfitUsd;
                }
                SetMevDetail(false, false, true, mevBlock, front, back, sandwiched, (wrapProfitUsd != null) ? -wrapProfitUsd : null, wrapProfitUsd);
                return;
            }

            // extract AMM x y pool values
            ZMDecimal c = 0.997; // our baseline protocol model of Uniswap 2 with 0.3% commission
            ZMDecimal x, y;
            if (!MEVCalc.PoolFromSwapsABAB(a, b, c, out x, out y))
            {
                // when input/output values match, the txs were probably retried reverts without this being detected
                MEVDetail = ErrorUndetectedRevert;
                back.MEVDetail = ErrorUndetectedRevert;
                return;
            }
            bool isLowLiquidity = MEVCalc.IsPoolLiquidityLow(a, b, x, y);

            // pool values must be positive
            if (x < 0 || y < 0)
            {
                MEVDetail = ErrorPool;
                back.MEVDetail = ErrorPool;
                return;
            }

            // the backrun trades against all other swaps in a sandwich attack
            bool[] isBA = new bool[b.Length];
            isBA[isBA.Length - 1] = true;

            // get the recalculated original set used for error reduction
            MEVCalc.CalculateSwaps(x, y, c, a, b, isBA, out var x_, out var y_, out var a_, out var b_, out var imbalanceSwitch);

            // sandwich profit / backrun user loss
            ZMDecimal sandwichProfit;
            ZMDecimal? backrunUserLoss = null;
            ZMDecimal? frontrunUserLoss = null;
            ZMDecimal? backrunImbalance = null;
            ZMDecimal? frontrunImbalance = null;
            int backIndex = a.Length - 1;
            ZMDecimal[] af, bf;
            if (b[backIndex] >= b[0])
                sandwichProfit = MEVCalc.SandwichProfitBackHeavy(x, y, c, a, b, isBA, 1, a.Length - 1, a_, b_, out backrunUserLoss, out af, out bf, out backrunImbalance);
            else
                sandwichProfit = MEVCalc.SandwichProfitFrontHeavy(x, y, c, a, b, isBA, 1, a.Length - 1, a_, b_, out frontrunUserLoss, out af, out bf, out frontrunImbalance);

            var sandwichProfitUsd = MEVCalc.SwapUsd(back.Swap.OutUsdRate(), sandwichProfit);
            this.SandwichProfitUsd = sandwichProfitUsd;
            this.FrontrunImbalance = (float?)frontrunImbalance;
            back.BackrunImbalance = (float?)backrunImbalance;

            decimal? backrunUserLossUsd = null;
            if (backrunUserLoss != null)
            {
                backrunUserLossUsd = MEVCalc.SwapUsd(back.Swap.OutUsdRate(), backrunUserLoss.Value);
                back.BackrunAmountUsd = backrunUserLossUsd;
            }

            decimal sumFrontrunUserLossUsd = 0;
            // sandwich profits are calculated in token a
            // frontrun user loss is calculated in token b
            // we want their usd rates to be accurate and comparable
            // sandwich profits are easily converted to usd using the final (a) back out rate, as this is the latest most efficient market information after the sandwich
            // the latest usd rate for frontrun losses (b) is in the middle of the sandwich, which would inflate the results and be inconsistent with sandwich profits
            // to avoid this, we use a calculated usd rate for (b) based on final pool values after the sandwich instead
            var br = MEVCalc.GetAFromB(y_, x_, c, a[backIndex]);
            ZMDecimal? bFinalUsdRate = null;

            if (back.Swap.IsBaseUsdRateOut)
            {
                // keep it simple if we are using base rates
                bFinalUsdRate = (ZMDecimal?)back.Swap.AmountOutUsd / back.Swap.AmountIn;
            }
            else if (back.Swap.AmountOutUsd < Num.OversizedAmount && !imbalanceSwitch && !isLowLiquidity && br != 0)
            {
                bFinalUsdRate = back.Swap.AmountOutUsd / br;
            }

            if (!bFinalUsdRate.HasValue)
            {
                // if we can't trust exchange rates (eg: on pool imbalance attacks) then use sandwich profits instead
                sumFrontrunUserLossUsd = -sandwichProfitUsd ?? 0;
                sandwiched[0].MEVAmountUsd = sumFrontrunUserLossUsd;
            }
            else
            {
                // frontrun user loss
                var bNoFrontrun = MEVCalc.FrontrunUserLoss(x, y, c, a, b, isBA, 1, a.Length - 1, a_, b_);
                int prevTxIndex = -1;
                decimal txUsd = 0;
                for (int i = 0; i < sandwiched.Length; i++)
                {
                    int index = i + 1;
                    var vi = b[index] - bNoFrontrun[index];
                    if (sandwiched[i].TxIndex != prevTxIndex) // handle multiple sandwiched swaps in one tx
                    {
                        txUsd = 0;
                        prevTxIndex = sandwiched[i].TxIndex.Value;
                    }
                    var usd = MEVCalc.SwapUsd(bFinalUsdRate, vi); // use backswap for consistency
                    if (usd != null)
                    {
                        txUsd += usd.Value;
                        sumFrontrunUserLossUsd += usd.Value;
                    }
                    sandwiched[i].MEVAmountUsd = txUsd;
                }
            }

            SetMevDetail(imbalanceSwitch, isLowLiquidity, false, mevBlock, front, back, sandwiched, sumFrontrunUserLossUsd, sandwichProfitUsd);
        }

        private void SetMevDetail(bool imbalanceSwitch, bool isLowLiquidity, bool isWrapped, MEVBlock mevBlock, MEVFrontrun front, MEVBackrun back, MEVSandwiched[] sandwiched, decimal? userLossUsd, decimal? profitUsd)
        {
            StringBuilder sb = new StringBuilder();
            if (isWrapped)
                sb.AppendLine("wrapped sandwich.");
            else if (imbalanceSwitch)
                sb.AppendLine("pool imbalance attack.");
            else if (isLowLiquidity)
                sb.AppendLine("low liquidity pair.");
            sb.AppendLine($"{sandwiched.Length} sandwiched swaps.\nuser loss (frontrun) = ${(userLossUsd != null ? userLossUsd.ToString() : "?")}.");
            sb.AppendLine($"sandwich profit = ${(profitUsd != null ? profitUsd.ToString() : "?")}.");
            if (back.BackrunAmountUsd != null)
            {
                sb.AppendLine("");
                sb.AppendLine($"the attacker used the sandwich to take a position at an inflated price,");
                sb.AppendLine($"backrun position = ${(back.BackrunAmountUsd != null ? (-back.BackrunAmountUsd).ToString() : "?")} of {mevBlock.Symbols[back.Swap.SymbolOutIndex].Name}.");
            }

            sb.AppendLine("\nfrontrun");
            front.Swap.BuildActionDetail(mevBlock, sb);
            sb.AppendLine("\n\nsandwiches");
            foreach (MEVSandwiched sd in sandwiched)
                foreach (var s in sd.SandwichedSwaps())
                {
                    s.BuildActionDetail(mevBlock, sb);
                    sb.AppendLine("");
                }
            sb.AppendLine("\nbackrun");
            back.Swap.BuildActionDetail(mevBlock, sb);

            var mevDetail = sb.ToString();
            front.MEVDetail = mevDetail;
            back.MEVDetail = mevDetail;
            foreach (MEVSandwiched sandwich in sandwiched)
                sandwich.MEVDetail = mevDetail;
        }
    }

    public class MEVSandwiched : IMEV
    {
        public MEVSandwiched()
        {
        }

        public MEVSandwiched(int? txIndex, MEVSwap swap)
        {
            TxIndex = txIndex;
            Swaps = new MEVSwaps(swap);
            SandwichedSwapIndex = new List<int?>() { 0 };
        }

        [JsonPropertyName("s")]
        public MEVSwaps Swaps { get; set; }

        [JsonPropertyName("i")]
        public int? TxIndex { get; set; }

        [JsonPropertyName("w")]
        public List<int?> SandwichedSwapIndex { get; set; }

        [JsonIgnore]
        public string TxHash => null;

        [JsonIgnore]
        public MEVType MEVType => MEVType.Sandwich;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Toxic;

        [JsonIgnore]
        public decimal? MEVAmountUsd { get; set; } = null;

        [JsonIgnore]
        public string? MEVDetail { get; set; } = null;

        [JsonIgnore]
        public string? ActionSummary { get; set; } = null;

        [JsonIgnore]
        public string? ActionDetail => Swaps.ActionDetail;

        public void Cache(MEVBlock mevBlock, int mevIndex)
        {
            Swaps.Cache(mevBlock, mevIndex);

            StringBuilder sb = new StringBuilder();
            Swaps.Swaps[SandwichedSwapIndex[0].Value].BuildActionSummary(mevBlock, sb);
            if (Swaps.Swaps.Count > 1)
            {
                sb.Append(" +");
                sb.Append(Swaps.Swaps.Count - 1);
            }
            ActionSummary = sb.ToString();
        }

        public int? AddSwap(MEVSwap swap)
        {
            var insertedIndex = Swaps.AddSwap(swap);
            for (int i = 0; i < SandwichedSwapIndex.Count; i++)
                SandwichedSwapIndex[i] = MEVSwaps.Shuffle(insertedIndex, SandwichedSwapIndex[i]);
            return null;
        }

        public void AddSandwichedSwap(MEVSwap sandwiched)
        {
            var index = Swaps.AddSwap(sandwiched);
            if (index != null)
                SandwichedSwapIndex.Add(index);
        }

        public List<MEVSwap> SandwichedSwaps()
        {
            List<MEVSwap> sandwichedSwaps = new List<MEVSwap>(Swaps.Swaps.Count);
            for (int i = 0; i < SandwichedSwapIndex.Count; i++)
                sandwichedSwaps.Add(Swaps.Swaps[SandwichedSwapIndex[i].Value]);
            return sandwichedSwaps;
        }
    }

    public class MEVBackrun : IMEV
    {
        public MEVBackrun()
        {
        }

        public MEVBackrun(int? txIndex, MEVSwap swap)
        {
            TxIndex = txIndex;
            Swaps = new MEVSwaps(swap);
            BackrunSwapIndex = 0;
        }

        [JsonPropertyName("s")]
        public MEVSwaps Swaps { get; set; }

        [JsonPropertyName("i")]
        public int? TxIndex { get; set; }

        [JsonPropertyName("b")]
        public int? BackrunSwapIndex { get; set; }

        [JsonPropertyName("u")]
        public decimal? BackrunAmountUsd { get; set; }

        [JsonIgnore]
        public MEVSwap Swap => BackrunSwapIndex != null && Swaps != null ? Swaps.Swaps[BackrunSwapIndex.Value] : null;

        [JsonIgnore]
        public string TxHash => null;

        [JsonIgnore]
        public MEVType MEVType => MEVType.Backrun;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Toxic;

        [JsonIgnore]
        public decimal? MEVAmountUsd { get; set; }

        [JsonIgnore]
        public string? MEVDetail { get; set; }

        [JsonIgnore]
        public string? ActionSummary => Swaps.ActionSummary;

        [JsonIgnore]
        public string? ActionDetail => Swaps.ActionDetail;

        [JsonIgnore]
        public string BackrunAmountStr
        {
            get
            {
                return Num.ToUsdStr(BackrunAmountUsd);
            }
        }

        [JsonIgnore]
        public float? BackrunImbalance { get; set; }

        public void Cache(MEVBlock mevBlock, int mevIndex)
        {
            Swaps.Cache(mevBlock, mevIndex);
        }

        public int? AddSwap(MEVSwap swap)
        {
            var insertedIndex = Swaps.AddSwap(swap);
            BackrunSwapIndex = MEVSwaps.Shuffle(insertedIndex, BackrunSwapIndex);
            return null;
        }
    }

    public class MEVArb : IMEV
    {
        public static decimal MaxUsdRate = 100000;

        public MEVArb()
        {
        }

        public MEVArb(int? txIndex, MEVClass mevClass, decimal? mevAmountUsd, int arbCase, MEVSwap swap)
        {
            TxIndex = txIndex;
            MEVClass = mevClass;
            MEVAmountUsd = mevAmountUsd;
            ArbCase = arbCase;
            Swaps = new MEVSwaps(swap);
            ArbSwapIndex = new List<int?>() { 0 };
        }

        [JsonPropertyName("a")]
        public int ArbCase { get; set; }

        [JsonPropertyName("w")]
        public List<int?> ArbSwapIndex { get; set; }

        [JsonPropertyName("s")]
        public MEVSwaps Swaps { get; set; }

        [JsonPropertyName("i")]
        public int? TxIndex { get; set; }

        [JsonIgnore]
        public string TxHash => null;

        [JsonIgnore]
        public MEVType MEVType => MEVType.Arb;

        [JsonPropertyName("c")]
        public MEVClass MEVClass { get; set; } = MEVClass.Unclassified;

        [JsonPropertyName("u")]
        public decimal? MEVAmountUsd { get; set; }

        [JsonIgnore]
        public string? MEVDetail { get; set; }

        [JsonIgnore]
        public string? ActionSummary => Swaps.ActionSummary;

        [JsonIgnore]
        public string? ActionDetail => Swaps.ActionDetail;

        public void Cache(MEVBlock mevBlock, int mevIndex)
        {
            StringBuilder sb = new StringBuilder();

            if (MEVAmountUsd == null)
            {
                sb.AppendLine("arb missing exchange rates, can't calculate.");
            }
            else if (Swaps?.Swaps == null || Swaps.Swaps.Count == 0)
            {
                sb.AppendLine("no arb swaps.");
            }
            else
            {
                sb.Append(Swaps.Swaps.Count);
                sb.AppendLine(" swaps in arb.");
                foreach (var a in ArbSwaps())
                {
                    a.BuildActionDetail(mevBlock, sb);
                    sb.AppendLine("");
                }
            }

            sb.Append("arb user loss $");
            sb.Append(MEVAmountUsd);
            sb.AppendLine(".");

            MEVDetail = sb.ToString();

            Swaps.Cache(mevBlock, mevIndex);
        }

        public int? AddSwap(MEVSwap swap)
        {
            var insertedIndex = Swaps.AddSwap(swap);
            for (int i = 0; i < ArbSwapIndex.Count; i++)
                ArbSwapIndex[i] = MEVSwaps.Shuffle(insertedIndex, ArbSwapIndex[i]);
            return null;
        }
        public void AddArbSwap(MEVSwap swap)
        {
            var index = Swaps.AddSwap(swap);
            if (index != null)
                ArbSwapIndex.Add(index);
        }

        public List<MEVSwap> ArbSwaps()
        {
            List<MEVSwap> arbSwaps = new List<MEVSwap>(Swaps.Swaps.Count);
            for (int i = 0; i < ArbSwapIndex.Count; i++)
            {
                if (ArbSwapIndex[i] != null)
                    arbSwaps.Add(Swaps.Swaps[ArbSwapIndex[i].Value]);
            }
            return arbSwaps;
        }
    }

    public class MEVLiquidation : IMEV
    {
        public MEVLiquidation()
        {
        }

        public MEVLiquidation(string txHash, ProtocolLiquidation protocol, ZMDecimal? debtPurchaseAmount, decimal? debtPurchaseAmountUsd, int debtSymbolIndex, ZMDecimal? receivedAmount, decimal? receivedAmountUsd, int receivedSymbolIndex, bool? isReverted)
        {
            TxHash = txHash;
            Protocol = protocol;
            DebtPurchaseAmount = debtPurchaseAmount;
            DebtPurchaseAmountUsd = debtPurchaseAmountUsd;
            ReceivedAmount = receivedAmount;
            ReceivedAmountUsd = receivedAmountUsd;
            DebtSymbolIndex = debtSymbolIndex;
            ReceivedSymbolIndex = receivedSymbolIndex;
            IsReverted = isReverted;
        }

        decimal? _receivedAmountUsd = null;
        decimal? _debtPurchaseAmountUsd = null;

        [JsonPropertyName("p")]
        public ProtocolLiquidation Protocol { get; set; }

        [JsonPropertyName("d")]
        public ZMDecimal? DebtPurchaseAmount { get; set; }

        [JsonPropertyName("du")]
        public decimal? DebtPurchaseAmountUsd
        {
            get
            {
                if (Protocol == ProtocolLiquidation.CompoundV2) return null;
                return _debtPurchaseAmountUsd;
            }
            set
            {
                _debtPurchaseAmountUsd = value;
            }
        }

        [JsonPropertyName("r")]
        public ZMDecimal? ReceivedAmount { get; set; }

        [JsonPropertyName("ru")]
        public decimal? ReceivedAmountUsd
        {
            get
            {
                if (Protocol == ProtocolLiquidation.CompoundV2) return null;
                return _receivedAmountUsd;
            }
            set
            {
                _receivedAmountUsd = value;
            }
        }

        [JsonPropertyName("a")]
        public int DebtSymbolIndex { get; set; }

        [JsonPropertyName("b")]
        public int ReceivedSymbolIndex { get; set; }

        [JsonPropertyName("v")]
        public bool? IsReverted { get; set; }

        [JsonPropertyName("h")]
        public string TxHash { get; set; }

        [JsonIgnore]
        public int? TxIndex => null;

        [JsonIgnore]
        public MEVType MEVType => MEVType.Liquid;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Unclassified;

        [JsonIgnore]
        public string? MEVDetail { get; set; } = "see action.";

        [JsonIgnore]
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

        [JsonIgnore]
        public decimal? MEVAmountUsd
        {
            get
            {
                if (Protocol == ProtocolLiquidation.CompoundV2 || ReceivedAmountUsd == null || DebtPurchaseAmountUsd == null)
                    return null;

                return DebtPurchaseAmountUsd - ReceivedAmountUsd;
            }
            set
            {
            }
        }

        public void Cache(MEVBlock mevBlock, int mevIndex)
        {
            StringBuilder sb = new StringBuilder();
            BuildActionSummary(mevBlock, sb);
            ActionSummary = sb.ToString();

            sb.Clear();
            BuildActionDetail(mevBlock, sb);
            ActionDetail = sb.ToString();
        }

        public string BuildActionSummary(MEVBlock mevBlock, StringBuilder sb)
        {
            sb.Append("<img src=\"");
            sb.Append(mevBlock.GetImage(DebtSymbolIndex));
            sb.Append("\" width=\"20\" height=\"20\"><img src=\"liq.svg\" width=\"20\" height=\"20\"><img src=\"");
            sb.Append(mevBlock.GetImage(ReceivedSymbolIndex));
            sb.Append("\" width=\"20\" height=\"20\">");
            return sb.ToString();
        }

        public string BuildActionDetail(MEVBlock mevBlock, StringBuilder sb)
        {
            // aave protocol liquidation.
            // debt purchase amount symbolA $143.11 (2784324.33).
            // received amount usd symbolB $54.17 (243432.11).
            sb.Append(Protocol.ToString());
            sb.AppendLine(" protocol liquidation.");
            sb.Append("debt purchase amount ");
            sb.Append(mevBlock.GetSymbolName(DebtSymbolIndex));
            sb.Append(" $");
            if (DebtPurchaseAmountUsd != null)
                sb.Append(DebtPurchaseAmountUsd);
            else
                sb.Append("?");
            sb.Append(" (");
            sb.Append(DebtPurchaseAmount);
            sb.AppendLine(").");

            sb.Append("received amount ");
            sb.Append(mevBlock.GetSymbolName(ReceivedSymbolIndex));
            sb.Append(" $");
            if (ReceivedAmountUsd != null)
                sb.Append(ReceivedAmountUsd);
            else
                sb.Append("?");
            sb.Append(" (");
            sb.Append(ReceivedAmount);
            sb.AppendLine(").");

            sb.Append("liquidation amount $");
            if (ReceivedAmountUsd != null)
                sb.Append(MEVAmountUsd);
            else
                sb.Append("?");

            return sb.ToString();
        }

        public int? AddSwap(MEVSwap swap)
        {
            return null;
        }
    }

    public class MEVNFT : IMEV
    {
        public MEVNFT(int? txIndex, ProtocolNFT protocol, int paymentSymbolIndex, string collectionAddress, string tokenId, ZMDecimal? paymentAmount, ZMDecimal? paymentAmountUsd, MEVError? error)
        {
            TxIndex = txIndex;
            Protocol = protocol;
            PaymentSymbolIndex = paymentSymbolIndex;
            PaymentAmount = paymentAmount;
            PaymentAmountUsd = paymentAmountUsd;
            CollectionAddress = collectionAddress;
            TokenId = tokenId;
            Error = error;
        }

        [JsonPropertyName("p")]
        public ProtocolNFT Protocol { get; set; }

        [JsonPropertyName("s")]
        public int PaymentSymbolIndex { get; set; }

        [JsonPropertyName("c")]
        public string CollectionAddress { get; set; }

        [JsonPropertyName("t")]
        public string TokenId { get; set; }

        [JsonPropertyName("a")]
        public ZMDecimal? PaymentAmount { get; set; }

        [JsonPropertyName("u")]
        public ZMDecimal? PaymentAmountUsd { get; set; }

        [JsonPropertyName("e")]
        public MEVError? Error { get; set; }

        [JsonPropertyName("i")]
        public int? TxIndex { get; set; }

        [JsonIgnore]
        public string TxHash => null;

        [JsonIgnore]
        public MEVType MEVType => MEVType.NFT;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Info;

        [JsonIgnore]
        public decimal? MEVAmountUsd { get; set; } = null;

        [JsonIgnore]
        public string? MEVDetail { get; set; } = "see action.";

        [JsonIgnore]
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

        public void Cache(MEVBlock mevBlock, int mevIndex)
        {
            // Opensea nft protocol
            // link https://opensea.io/assets/0xac3d871d3431847bdff9eebb42eb51ae06e131c3/6016
            // $1313.10 (0.33 WETH)

            StringBuilder sb = new StringBuilder();

            string nftLink = null;
            if (Protocol == ProtocolNFT.Opensea)
            {
                sb.Append("https://opensea.io/assets/");
                sb.Append(CollectionAddress);
                sb.Append(@"/");
                sb.Append(TokenId);
                nftLink = sb.ToString();
            }

            sb.Clear();
            sb.Append("<a href=\"");
            sb.Append(nftLink);
            sb.Append("\" target=\"_blank\"><img src=\"\\nft.png\" alt=\"nft\" width=\"20\" height=\"20\"><img src=\"\\link.svg\" alt=\"nft link\" width=\"20\" height=\"20\"></a>");
            ActionSummary = sb.ToString();

            sb.Clear();
            sb.Append(Protocol);
            sb.AppendLine(" nft protocol");
            if (nftLink != null)
            {
                sb.Append("link ");
                sb.AppendLine(nftLink);
            }
            else
            {
                sb.Append("collection ");
                sb.AppendLine(CollectionAddress);
                sb.Append(@"token ");
                sb.AppendLine(TokenId);
            }
            sb.Append("payment $");
            if (PaymentAmountUsd != null)
                sb.Append(PaymentAmountUsd);
            else
                sb.Append("?");
            sb.Append(" (");
            sb.Append(PaymentAmount);
            sb.Append(" ");
            sb.Append(mevBlock.GetSymbolName(PaymentSymbolIndex));
            sb.Append(")");
            if (Error != MEVError.None)
            {
                sb.AppendLine();
                sb.AppendLine(Error.ToString());
            }
            ActionDetail = sb.ToString();
        }

        public int? AddSwap(MEVSwap swap)
        {
            return null;
        }
    }

    public struct MEVSummary
    {
        public MEVFilter MEVFilter;
        public decimal Count;
        public decimal AmountUsd;
    }

    public class MEVRow
    {
        public MEVRow(int index, MEVType mevType, decimal? mevAmount, string mevComment)
        {
            Index = index;
            MEVType = mevType;
            MEVAmount = mevAmount;
            MEVComment = mevComment;
        }

        public int Index { get; set; }
        public MEVType MEVType { get; set; }
        public decimal? MEVAmount { get; set; }
        public string MEVComment { get; set; }
    }

    public struct MEVDisplay
    {
        public readonly int Index;
        public readonly MEVClass Class;
        public readonly string Name;
        public readonly bool IsVisible;
        public readonly string CssClass;
        public readonly string HelpLink;

        public MEVDisplay(MEVClass mevClass, string name, bool isVisible, string cssClass, string helpLink = "")
        {
            Index = (int)mevClass;
            Class = mevClass;
            Name = name;
            IsVisible = isVisible;
            CssClass = cssClass;
            HelpLink = helpLink;
        }

        public bool DoDisplay
        {
            get
            {
                return IsVisible;
            }
        }
    }

    public class MEVWeb
    {
        public static MEVDisplay[] Rows = {
            new MEVDisplay(MEVClass.All, "", false, "mev-any", "http://info.zeromev.org/explorer.html#mev"),
            new MEVDisplay(MEVClass.Unclassified, "Unclassified", true, "mev-un", "http://info.zeromev.org/terms.html#unclassified-mev"),
            new MEVDisplay(MEVClass.Positive, "Positive", true, "mev-pos", "http://info.zeromev.org/explorer.html#mev"),
            new MEVDisplay(MEVClass.Neutral, "Neutral", true, "mev-neu", "http://info.zeromev.org/terms.html#unclassified-mev"),
            new MEVDisplay(MEVClass.Toxic, "Toxic", true, "mev-tox", "http://info.zeromev.org/terms.html#toxic-mev"),
            new MEVDisplay(MEVClass.Info, "Info", true, "mev-inf","http://info.zeromev.org/explorer.html#mev") };

        public static MEVDisplay Get(MEVClass mevClass)
        {
            return Rows[(int)mevClass];
        }

        public static string CssClass(MEVClass mevClass)
        {
            return Rows[(int)mevClass].CssClass;
        }

        public static MEVFilter GetMEVFilter(MEVClass mevClass)
        {
            switch (mevClass)
            {
                case MEVClass.Toxic:
                    return MEVFilter.Toxic;

                case MEVClass.Unclassified:
                case MEVClass.Positive:
                case MEVClass.Neutral:
                    return MEVFilter.Other;

                default:
                    return MEVFilter.Info;
            }
        }

        public static void UpdateSummaries(IMEV mev, MEVSummary[] mevSummaries)
        {
            var mevFilter = MEVWeb.GetMEVFilter(mev.MEVClass);
            mevSummaries[(int)MEVFilter.All].Count++;
            mevSummaries[(int)MEVFilter.All].AmountUsd += mev.MEVAmountUsd ?? 0;
            if (mevFilter == MEVFilter.Toxic || mevFilter == MEVFilter.Other)
            {
                mevSummaries[(int)mevFilter].Count++;
                mevSummaries[(int)mevFilter].AmountUsd += mev.MEVAmountUsd ?? 0;
            }
        }
    }

    public static class MEVCalc
    {
        public const string EthSymbolName = "WETH";
        public static HashSet<string> UsdSymbolNames = new HashSet<string>() { "Tether USD", "USD Coin", "Dai", "Binance USD", "Gemini Dollar" };

        public static bool GetSandwichParameters(MEVBlock mb, int index, out ZMDecimal[]? aOut, out ZMDecimal[]? bOut, out MEVFrontrun front, out MEVBackrun back, out MEVSandwiched[] sandwiched)
        {
            aOut = null;
            bOut = null;

            if (index >= mb.Frontruns.Count ||
                index >= mb.Backruns.Count ||
                index >= mb.Sandwiched.Count)
            {
                front = null;
                back = null;
                sandwiched = null;
                return false;
            }

            front = mb.Frontruns[index];
            back = mb.Backruns[index];
            sandwiched = mb.Sandwiched[index];

#if (DEBUG)
            if (!front.Swap.IsKnown || !back.Swap.IsKnown) 
                Console.WriteLine($"swaps unknown skipping sandwich block {mb.BlockNumber}");
#endif
            if (!front.Swap.IsKnown) return false;
            if (!back.Swap.IsKnown) return false;

            var a = new List<ZMDecimal>();
            var b = new List<ZMDecimal>();

            // determine sandwich protocol
            ProtocolSwap? protocol = null;
            for (int i = 0; i < sandwiched.Length; i++)
            {
                MEVSandwiched ms = sandwiched[i];
                for (int j = 0; j < ms.SandwichedSwapIndex.Count; j++)
                {
                    var swap = ms.Swaps.Swaps[ms.SandwichedSwapIndex[j].Value];
                    if (!swap.IsKnown) return false;
                    if (protocol != null && swap.Protocol != protocol) return false;
                    protocol = swap.Protocol;
                }
            }

            // set frontrun amount with coalescing
            if (!AmountCoalescing(protocol, front.Swap, front.Swaps.Swaps, out var frontIn, out var frontOut))
                return false;

            a.Add(frontIn);
            b.Add(frontOut);

            for (int i = 0; i < sandwiched.Length; i++)
            {
                MEVSandwiched ms = sandwiched[i];
                for (int j = 0; j < ms.SandwichedSwapIndex.Count; j++)
                {
                    var swap = ms.Swaps.Swaps[ms.SandwichedSwapIndex[j].Value];
                    a.Add(swap.AmountIn ?? 0);
                    b.Add(swap.AmountOut ?? 0);
                }
            }

            // set backrun amount with coalescing
            if (!AmountCoalescing(protocol, back.Swap, back.Swaps.Swaps, out var backIn, out var backOut))
                return false;

            a.Add(backOut); // amounts reversed as backruns trade against frontrun and sandwiched
            b.Add(backIn);

            aOut = a.ToArray();
            bOut = b.ToArray();
            return true;
        }

        private static bool AmountCoalescing(ProtocolSwap? protocol, MEVSwap swap, List<MEVSwap> swaps, out ZMDecimal sumIn, out ZMDecimal sumOut)
        {
            sumIn = 0;
            sumOut = 0;

            // set frontrun amount with coalescing
            bool success = false;
            foreach (MEVSwap s in swaps)
            {
                if (s.SymbolInIndex == swap.SymbolInIndex &&
                    s.SymbolOutIndex == swap.SymbolOutIndex &&
                    s.Protocol == protocol)
                {
                    sumIn += s.AmountIn ?? 0;
                    sumOut += s.AmountOut ?? 0;
                    success = true;
                }
            }
            return success;
        }

        public static bool DetectWrappedSandwich(MEVFrontrun front, MEVBackrun back, out MEVSwap frontWrap, out MEVSwap backWrap)
        {
            // wrapped swaps must be within the same transaction (only look within the passed front/back runs)
            // find the outermost of any number of layers of wrapping
            int fi = front.FrontrunSwapIndex.Value - 1;
            int bi = back.BackrunSwapIndex.Value + 1;

            bool isWrapDetected = false;
            frontWrap = front.Swap;
            backWrap = back.Swap;

            // wrapped sandwiches must be well balanced or we should use the pool extraction method instead
            if (front.Swap.AmountOut == null || back.Swap.AmountIn == null) return false;
            var p = (front.Swap.AmountOut / (front.Swap.AmountOut + back.Swap.AmountIn));
            if (p > 0.51 || p < 0.49)
                return false;

            while (fi >= 0 && bi < back.Swaps.Swaps.Count)
            {
                var frontWrapNext = front.Swaps.Swaps[fi];
                var backWrapNext = back.Swaps.Swaps[bi];

                if (frontWrapNext.SymbolOutIndex != frontWrap.SymbolInIndex) break;
                if (backWrapNext.SymbolInIndex != backWrap.SymbolOutIndex) break;
                if (frontWrapNext.SymbolInIndex != backWrapNext.SymbolOutIndex) break;

                isWrapDetected = true;
                frontWrap = frontWrapNext;
                backWrap = backWrapNext;
                fi--;
                bi++;
            }

            return isWrapDetected;
        }

        public static void CalculateSwaps(ZMDecimal x, ZMDecimal y, ZMDecimal c, ZMDecimal[] a, ZMDecimal[] b, bool[] isBA, out ZMDecimal x_, out ZMDecimal y_, out ZMDecimal[] a_, out ZMDecimal[] b_, out bool imbalanceSwitch)
        {
            imbalanceSwitch = false;
            x_ = x;
            y_ = y;

            a_ = new ZMDecimal[a.Length];
            b_ = new ZMDecimal[b.Length];

            for (int i = 0; i < a.Length; i++)
            {
                var prevx = x_;
                var prevy = y_;

                if (!isBA[i])
                {
                    // ab_ swap
                    a_[i] = a[i];
                    b_[i] = MEVCalc.SwapOutputAmount(ref x_, ref y_, c, a[i]);
                }
                else
                {
                    // ba_ swap
                    a_[i] = MEVCalc.SwapOutputAmount(ref y_, ref x_, c, b[i]);
                    b_[i] = b[i];
                }

                bool sign = x_ > y_;
                bool prevSign = prevx > prevy;
                if (sign != prevSign)
                    imbalanceSwitch = true;
            }
        }

        // given starting x y values that produced an output b in an ab swap, determine value a
        public static ZMDecimal GetAFromB(ZMDecimal x, ZMDecimal y, ZMDecimal c, ZMDecimal b)
        {
            return (-b * x) / ((b * c) - (c * y));
        }

        public static void CopySwaps(ZMDecimal[] a, ZMDecimal[] b, out ZMDecimal[] an, out ZMDecimal[] bn)
        {
            an = new ZMDecimal[a.Length];
            bn = new ZMDecimal[b.Length];
            Array.Copy(a, an, a.Length);
            Array.Copy(b, bn, b.Length);
        }

        public static void FinalizeSwapCalculations(ZMDecimal[] a, ZMDecimal[] b, ZMDecimal[] a_, ZMDecimal[] b_, ZMDecimal[] an, ZMDecimal[] bn, out ZMDecimal[] af, out ZMDecimal[] bf)
        {
            af = new ZMDecimal[a.Length];
            bf = new ZMDecimal[b.Length];

            // final calculated values = calculated new * (original value / calculated original)
            for (int i = 0; i < a.Length; i++)
            {
                if (an[i] == 0 || a_[i] == 0)
                    af[i] = 0;
                else
                    af[i] = an[i] * (a[i] / a_[i]);

                if (bn[i] == 0 || b_[i] == 0)
                    bf[i] = 0;
                else
                    bf[i] = bn[i] * (b[i] / b_[i]);
            }
        }

        // returns the output (b) values of the sandwiched swaps had they not been frontrun
        // user loss = returned b - original b
        public static ZMDecimal[] FrontrunUserLoss(ZMDecimal x, ZMDecimal y, ZMDecimal c, ZMDecimal[] a, ZMDecimal[] b, bool[] isBA, int sandwichedFrom, int sandwichedTo, ZMDecimal[] a_, ZMDecimal[] b_)
        {
            MEVCalc.CopySwaps(a, b, out var an, out var bn);

            // zero the frontrun in amount and recalculate to find what each victim transaction would have recieved had it not been frontrun
            an[0] = 0;
            MEVCalc.CalculateSwaps(x, y, c, an, bn, isBA, out var xv, out var yv, out var av, out var bv, out var imbalanceSwitch);
            MEVCalc.FinalizeSwapCalculations(a, b, a_, b_, av, bv, out var af, out var bf);
            return bf;
        }

        public static ZMDecimal SandwichProfitBackHeavy(ZMDecimal x, ZMDecimal y, ZMDecimal c, ZMDecimal[] a, ZMDecimal[] b, bool[] isBA, int sandwichedFrom, int sandwichedTo, ZMDecimal[] a_, ZMDecimal[] b_, out ZMDecimal? backrunUserLoss, out ZMDecimal[] af, out ZMDecimal[] bf, out ZMDecimal? backImbalance)
        {
            MEVCalc.CopySwaps(a, b, out var an, out var bn);

            // set back in to front out and recalculate
            int backIndex = b.Length - 1;
            if (b[0] < bn[backIndex])
                bn[backIndex] = b[0];
            MEVCalc.CalculateSwaps(x, y, c, an, bn, isBA, out var xv, out var yv, out var av, out var bv, out var imbalanceSwitch);
            MEVCalc.FinalizeSwapCalculations(a, b, a_, b_, av, bv, out af, out bf);

            // backrun user loss = calculated optimal back out - original back out
            backrunUserLoss = af[backIndex] - a[backIndex];
            backImbalance = -backrunUserLoss / af[backIndex]; // expressed as the amount we are overweight on the backrun normalized by the optimal amount needed to balance

            // sandwich profit = calculated back out - calculated front in
            return af[backIndex] - af[0];
        }

        public static ZMDecimal SandwichProfitFrontHeavy(ZMDecimal x, ZMDecimal y, ZMDecimal c, ZMDecimal[] a, ZMDecimal[] b, bool[] isBA, int sandwichedFrom, int sandwichedTo, ZMDecimal[] a_, ZMDecimal[] b_, out ZMDecimal? frontrunUserLoss, out ZMDecimal[] af, out ZMDecimal[] bf, out ZMDecimal? frontImbalance)
        {
            MEVCalc.CopySwaps(a, b, out var an, out var bn);

            // use back in as front out and reverse swap the front in from this new front out
            int backIndex = b.Length - 1;
            var newFrontIn = MEVCalc.GetAFromB(x, y, c, b[backIndex]);
            if (newFrontIn < an[0])
                an[0] = newFrontIn;
            MEVCalc.CalculateSwaps(x, y, c, an, bn, isBA, out var xv, out var yv, out var av, out var bv, out var imbalanceSwitch);
            MEVCalc.FinalizeSwapCalculations(a, b, a_, b_, av, bv, out af, out bf);

            // frontrun user loss = calculated optimal front in - original front in
            frontrunUserLoss = an[0] - a[0];
            frontImbalance = frontrunUserLoss / an[0]; // expressed as the amount we are overweight on the frontrun normalized by the amount needed to balance

            // sandwich profit = calculated back out - calculated front in
            return af[backIndex] - af[0];
        }

        public static bool PoolFromSwapsABAB(ZMDecimal[] a, ZMDecimal[] b, ZMDecimal c, out ZMDecimal x, out ZMDecimal y)
        {
            // this happens when we don't detect reverts and the user retries the same tx
            if (a[0] == a[1] && b[0] == b[1])
            {
                x = -1;
                y = -1;
                return false;
            }

            x = ((a[0] * a[1] * b[1] * c) + (a[0].Pow(2) * b[1])) / ((a[1] * b[0]) - (a[0] * b[1]));
            y = ((((b[0] * ((a[1] * b[1]) - (a[0] * b[1]))) + (a[1] * b[0].Pow(2))) * c) + (a[0] * b[0] * b[1])) / ((a[1] * b[0] * c) - (a[0] * b[1] * c));
            return true;
        }

        public static bool PoolFromSwapsABBA(ZMDecimal[] a, ZMDecimal[] b, ZMDecimal c, out ZMDecimal x, out ZMDecimal y)
        {
            // this happens when we don't detect reverts and the user retries the same tx
            if (a[0] == a[1] && b[0] == b[1])
            {
                x = -1;
                y = -1;
                return false;
            }

            var cPow2 = c.Pow(2);
            x = -(((a[0].Pow(2) * b[1]) - (a[0] * a[1] * b[1])) * cPow2) / ((a[0] * b[1] * cPow2) - (a[1] * b[0]));
            y = ((a[0] * b[0] * b[1] * cPow2) + (b[0] * a[1] * b[1] * c) - (b[0] * a[0] * b[1] * c) - (a[1] * b[0].Pow(2))) / ((a[0] * b[1] * cPow2) - (a[1] * b[0]));
            return true;
        }

        public static void PoolFromSwapsABBA(ZMDecimal a0, ZMDecimal a1, ZMDecimal b0, ZMDecimal b1, ZMDecimal c, out ZMDecimal x, out ZMDecimal y)
        {
            var cPow2 = c.Pow(2);
            x = -(((a0.Pow(2) * b1) - (a0 * a1 * b1)) * cPow2) / ((a0 * b1 * cPow2) - (a1 * b0));
            y = ((a0 * b0 * b1 * cPow2) + (b0 * a1 * b1 * c) - (b0 * a0 * b1 * c) - (a1 * b0.Pow(2))) / ((a0 * b1 * cPow2) - (a1 * b0));
        }

        public static bool IsPoolLiquidityLow(ZMDecimal[] a, ZMDecimal[] b, ZMDecimal x, ZMDecimal y)
        {
            for (int i = 0; i < a.Length; i++)
                if (a[i] >= x || b[i] >= y) return true;
            return false;
        }

        public static ZMDecimal SwapOutputAmount(ref ZMDecimal reserveIn, ref ZMDecimal reserveOut, ZMDecimal c, ZMDecimal amountIn)
        {
            // see UniswapV2Library.sol getAmountOut()
            if (amountIn == 0)
                return 0;

            // get amount out
            var aLessFee = (amountIn * c);
            var numerator = aLessFee * reserveOut;
            var denominator = reserveIn + aLessFee;
            var amountOut = numerator / denominator;

            // update reserves
            reserveIn += amountIn; // include fees
            reserveOut -= amountOut;

            return amountOut;
        }

        public static ZMDecimal SwapOutputAmountReversed(ref ZMDecimal reserveIn, ref ZMDecimal reserveOut, ZMDecimal c, ZMDecimal amountIn)
        {
            // as above but with commission applied in reverse
            // the caller is expected to have switched amounts and reserves

            // get amount out
            var aLessFee = (amountIn * (1 + (1 - c)));
            var numerator = aLessFee * reserveOut;
            var denominator = reserveIn + aLessFee;
            var amountOut = numerator / denominator;

            // update reserves
            reserveIn += amountIn; // include fees
            reserveOut -= amountOut;

            return amountOut;
        }

        public static decimal? SwapUsd(ZMDecimal? usdRate, ZMDecimal? amount, bool doNullTiny = true)
        {
            if (amount == null)
                return null;

            if (usdRate != null)
            {
                var userLossUsd = amount.Value * usdRate.Value;
                if (!doNullTiny || userLossUsd > 0.01 || userLossUsd < -0.01) // ignore tiny amounts
                    return userLossUsd.ToUsd();
            }
            return null;
        }
    }
}