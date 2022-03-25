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
        PunkSnipe
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
        Bancor
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
        string? ActionSummary { get; set; }
        string? ActionDetail { get; set; }
        void Cache(MEVBlock mevBlock, int mevIndex); // mevIndex is allows mev instances to find other related instances, eg: backrun can cheaply find related sandwiched and frontun txs without duplicating them
    }

    public class Symbol
    {
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
            foreach (var b in Blocks)
                b.CalculateSummaries(Totals);
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
        public List<MEVSwap> Swaps { get; set; } = new List<MEVSwap>();

        [JsonPropertyName("c")]
        public List<MEVContractSwaps> ContractSwaps { get; set; } = new List<MEVContractSwaps>();

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
        public bool[] ExistingMEV { get; set; }

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
    }

    public class MEVSwap : IMEV
    {
        public MEVSwap()
        {
        }

        public MEVSwap(int txIndex, ProtocolSwap protocol, int symbolInIndex, int symbolOutIndex, ZMDecimal amountIn, ZMDecimal amountOut, ZMDecimal? inUsdRate, ZMDecimal? outUsdRate)
        {
            TxIndex = txIndex;
            Protocol = protocol;
            SymbolInIndex = symbolInIndex;
            SymbolOutIndex = symbolOutIndex;
            AmountIn = amountIn;
            AmountOut = amountOut;

            // store the output usd because it's smaller than the BigDecimal rate and generally more useful
            if (inUsdRate.HasValue) AmountInUsd = (amountIn * inUsdRate.Value).ToUsd();
            if (outUsdRate.HasValue) AmountOutUsd = (amountOut * outUsdRate.Value).ToUsd();
        }

        [JsonPropertyName("i")]
        public int? TxIndex { get; set; }

        [JsonIgnore]
        public string TxHash => null;

        [JsonPropertyName("a")]
        public int SymbolInIndex { get; set; }

        [JsonPropertyName("b")]
        public int SymbolOutIndex { get; set; }

        [JsonPropertyName("c")]
        public ZMDecimal AmountIn { get; set; }

        [JsonPropertyName("d")]
        public ZMDecimal AmountOut { get; set; }

        [JsonPropertyName("e")]
        public decimal? AmountInUsd { get; set; }

        [JsonPropertyName("f")]
        public decimal? AmountOutUsd { get; set; }

        [JsonPropertyName("p")]
        public ProtocolSwap Protocol { get; set; }

        [JsonIgnore]
        public ZMDecimal Rate
        {
            get
            {
                return AmountOut / AmountIn;
            }
        }

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

        public ZMDecimal? InUsdRate()
        {
            if (AmountInUsd == null) return null;
            if (AmountIn < Num.EpsilonAmount) return null;
            return (ZMDecimal)AmountInUsd / AmountIn;
        }

        public ZMDecimal? OutUsdRate()
        {
            if (AmountOutUsd == null) return null;
            if (AmountOut < Num.EpsilonAmount) return null;
            return (ZMDecimal)AmountOutUsd / AmountOut;
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
            sb.Append(AmountIn.Shorten());
            sb.Append(" ($");
            sb.Append(AmountInUsd != null ? AmountInUsd.ToString() : "?");
            sb.Append(") > ");
            sb.Append(mevBlock.GetSymbolName(SymbolOutIndex));
            sb.Append(" ");
            sb.Append(AmountOut.Shorten());
            sb.Append(" ($");
            sb.Append(AmountOutUsd != null ? AmountOutUsd.ToString() : "?");
            sb.Append(") @");
            sb.Append(Rate.Shorten());
        }
    }

    public class MEVContractSwaps : IMEV
    {
        public MEVContractSwaps()
        {
        }

        public MEVContractSwaps(int? txIndex)
        {
            TxIndex = txIndex;
        }

        [JsonPropertyName("s")]
        public List<MEVSwap> Swaps { get; set; } = new List<MEVSwap>();

        [JsonPropertyName("i")]
        public int? TxIndex { get; set; }

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
            if (Swaps == null || Swaps.Count == 0) return "no swaps";
            Swaps[0].BuildActionSummary(mevBlock, sb);
            if (Swaps.Count > 1)
            {
                sb.Append(" +");
                sb.Append(Swaps.Count - 1);
            }
            sb.AppendLine();
            return sb.ToString();
        }

        public string BuildActionDetail(MEVBlock mevBlock, StringBuilder sb)
        {
            sb.Append(Swaps.Count);
            sb.AppendLine(" swaps in contract.");
            foreach (var swap in Swaps)
            {
                swap.BuildActionDetail(mevBlock, sb);
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }

    public class MEVFrontrun : IMEV
    {
        private const string ErrorParameters = "can't get parameters to calculate.";
        private const string ErrorPool = "can't extract AMM pool to calculate.";

        public MEVFrontrun()
        {
        }

        public MEVFrontrun(int? txIndex, MEVSwap swap)
        {
            TxIndex = txIndex;
            Swap = swap;
        }

        [JsonPropertyName("s")]
        public MEVSwap Swap { get; set; }

        [JsonPropertyName("i")]
        public int? TxIndex { get; set; }

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
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

        [JsonIgnore]
        public decimal? SandwichProfitUsd { get; set; }
        
        [JsonIgnore]
        public ZMDecimal? X { get; set; }

        [JsonIgnore]
        public ZMDecimal? Y { get; set; }

        public void Cache(MEVBlock mevBlock, int mevIndex)
        {
            // mev
            CalculateMev(mevBlock, mevIndex);

            // action
            StringBuilder sb = new StringBuilder();
            Swap.BuildActionSummary(mevBlock, sb);
            ActionSummary = sb.ToString();

            sb.Clear();
            Swap.BuildActionDetail(mevBlock, sb);
            ActionDetail = sb.ToString();
            return;
        }

        private void CalculateMev(MEVBlock mevBlock, int mevIndex)
        {
#if (DEBUG)
            if (mevBlock.BlockNumber == 13389128)
                Console.Write("");
#endif

            // mev (calculate for itself and all related sandwiched and backrun instances)
            if (!MEVCalc.GetSandwichParameters(mevBlock, mevIndex, out var a, out var b, out var front, out var back, out var sandwiched))
            {
                MEVDetail = ErrorParameters;
                back.MEVDetail = ErrorParameters;
                return;
            }

            // extract AMM x y pool values
            ZMDecimal c = 0.997; // our baseline protocol model of Uniswap 2 with 0.3% commission
            ZMDecimal x, y;
            MEVCalc.PoolFromSwapsABAB(a, b, c, out x, out y);
            if (x < 0 || y < 0)
            {
                // pool values must be positive
                MEVDetail = ErrorPool;
                back.MEVDetail = ErrorPool;
                return;
            }

            this.X = x;
            this.Y = y;

            // the backrun trades against all other swaps in a sandwich attack
            bool[] isBA = new bool[b.Length];
            isBA[isBA.Length - 1] = true;

            // get the recalculated original set used for error reduction
            MEVCalc.CalculateSwaps(x, y, c, a, b, isBA, out var x_, out var y_, out var a_, out var b_);

            // frontrun victim impact
            var bNoFrontrun = MEVCalc.FrontrunVictimImpact(x, y, c, a, b, isBA, 1, a.Length - 1, a_, b_);
            decimal sumFrontrunVictimImpactUsd = 0;
            int prevTxIndex = -1;
            decimal txUsd = 0;
            ZMDecimal sandwichInput = 0;
            for (int i = 0; i < sandwiched.Length; i++)
            {
                int index = i + 1;
                var frontrunVictimImpact = b[index] - bNoFrontrun[index];
                if (sandwiched[i].TxIndex != prevTxIndex) // handle multiple sandwiched swaps in one tx
                {
                    txUsd = 0;
                    prevTxIndex = sandwiched[i].TxIndex.Value;
                }
                var usd = MEVCalc.SwapUsd(back.Swap, frontrunVictimImpact); // use backswap for consistency
                if (usd != null)
                {
                    txUsd += usd.Value;
                    sumFrontrunVictimImpactUsd += usd.Value;
                }
                sandwiched[i].MEVAmountUsd = txUsd;
                sandwichInput += a[index];

                /*
                var rateViaUsdBack = back.Swap.OutUsdRate() / sandwiched[i].Swap.OutUsdRate();
                var rateViaUsdFront = front.Swap.InUsdRate() / sandwiched[i].Swap.OutUsdRate();
                var rate = sandwiched[i].Swap.Rate;
                */
            }

            // sandwich profit / backrun victim impact
            ZMDecimal sandwichProfit;
            ZMDecimal? backrunVictimImpact = null;
            int backIndex = a.Length - 1;

            if (b[backIndex] >= b[0])
                sandwichProfit = MEVCalc.SandwichProfitBackHeavy(x, y, c, a, b, isBA, 1, a.Length - 1, a_, b_, out backrunVictimImpact);
            else
                sandwichProfit = MEVCalc.SandwichProfitFrontHeavy(x, y, c, a, b, isBA, 1, a.Length - 1, a_, b_);

            var sandwichProfitUsd = MEVCalc.SwapUsd(back.Swap, sandwichProfit);
            SandwichProfitUsd = sandwichProfitUsd;
            //back.MEVAmountUsd = MEVCalc.SwapUsd(back.Swap, backrunVictimImpact, true); TODO TEMPORARILY REMOVED, CALCULATE BY DIFFERENCING SANDWICH PROFIT BELOW WITH NAIVE P&L where back in> front out

            // use negated sandwich profits in place of the victim impact where
            // exchange rates are not available for the frontrun calc
            // or the victim impact is oversized compared to the sandwich profit 
            ZMDecimal maxMultiple = 1.5;
            if (sandwichInput != 0)
                maxMultiple += a[0] / sandwichInput;
            if (sandwichProfitUsd != null)
            {
                if (sumFrontrunVictimImpactUsd == 0 && sandwichProfitUsd != 0 ||
                    sumFrontrunVictimImpactUsd < -sandwichProfitUsd.Value * maxMultiple)
                {
                    sumFrontrunVictimImpactUsd = -sandwichProfitUsd.Value;
                    foreach (MEVSandwiched s in sandwiched)
                        s.MEVAmountUsd = null;
                    sandwiched[0].MEVAmountUsd = sumFrontrunVictimImpactUsd;
                }
            }

            // mev detail
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{sandwiched.Length} sandwiched swaps.\nvictim impact (frontrun) = ${sumFrontrunVictimImpactUsd}.");
            if (back.MEVAmountUsd != null)
                sb.AppendLine($"victim impact (backrun) = ${back.MEVAmountUsd}.");
            sb.AppendLine($"sandwich profit = ${sandwichProfitUsd}.");
            if (sandwichProfitUsd > -sumFrontrunVictimImpactUsd)
                sb.AppendLine("sandwich profit exceeds victim impact due to usd exchange rate availability / differences.");
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
            Swap = swap;
        }

        [JsonPropertyName("s")]
        public MEVSwap Swap { get; set; }

        [JsonPropertyName("i")]
        public int? TxIndex { get; set; }

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
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

        public void Cache(MEVBlock mevBlock, int mevIndex)
        {
            StringBuilder sb = new StringBuilder();
            Swap.BuildActionSummary(mevBlock, sb);
            ActionSummary = sb.ToString();

            sb.Clear();
            Swap.BuildActionDetail(mevBlock, sb);
            ActionDetail = sb.ToString();
            return;
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
            Swap = swap;
        }

        [JsonPropertyName("s")]
        public MEVSwap Swap { get; set; }

        [JsonPropertyName("i")]
        public int? TxIndex { get; set; }

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
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

        public void Cache(MEVBlock mevBlock, int mevIndex)
        {
            StringBuilder sb = new StringBuilder();
            Swap.BuildActionSummary(mevBlock, sb);
            ActionSummary = sb.ToString();

            sb.Clear();
            Swap.BuildActionDetail(mevBlock, sb);
            ActionDetail = sb.ToString();
            return;
        }
    }

    public class MEVArb : IMEV
    {
        public MEVArb()
        {
        }

        public MEVArb(int? txIndex, MEVClass mevClass, decimal? mevAmountUsd)
        {
            TxIndex = txIndex;
            MEVClass = mevClass;
            MEVAmountUsd = mevAmountUsd;
        }

        [JsonPropertyName("s")]
        public List<MEVSwap> Swaps { get; set; } = new List<MEVSwap>();

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
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

        public void Cache(MEVBlock mevBlock, int mevIndex)
        {
            StringBuilder sb = new StringBuilder();

            if (MEVAmountUsd == null)
            {
                sb.AppendLine("arb missing exchange rates, can't calculate.");
            }
            else
            {
                if (Swaps == null || Swaps.Count == 0)
                {
                    sb.AppendLine("no arb swaps.");
                }
                else
                {
                    sb.Append(Swaps.Count);
                    sb.AppendLine(" swaps in arb.");
                }
                sb.Append("arb victim impact $");
                sb.Append(MEVAmountUsd);
                sb.AppendLine(".");
            }
            MEVDetail = sb.ToString();

            sb.Clear();
            BuildActionSummary(mevBlock, sb);
            ActionSummary = sb.ToString();

            sb.Clear();
            BuildActionDetail(mevBlock, sb);
            ActionDetail = sb.ToString();
        }

        public string BuildActionSummary(MEVBlock mevBlock, StringBuilder sb)
        {
            if (Swaps == null || Swaps.Count == 0) return "no swaps.";
            Swaps[0].BuildActionSummary(mevBlock, sb);
            if (Swaps.Count > 1)
            {
                sb.Append(" +");
                sb.Append(Swaps.Count - 1);
            }
            sb.AppendLine();
            return sb.ToString();
        }

        public string BuildActionDetail(MEVBlock mevBlock, StringBuilder sb)
        {
            sb.Append(Swaps.Count);
            sb.AppendLine(" swaps in arb.");
            foreach (var swap in Swaps)
            {
                swap.BuildActionDetail(mevBlock, sb);
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }

    public class MEVLiquidation : IMEV
    {
        public MEVLiquidation()
        {
        }

        public MEVLiquidation(string txHash, ProtocolLiquidation protocol, BigInteger? debtPurchaseAmount, decimal? debtPurchaseAmountUsd, int debtSymbolIndex, BigInteger? receivedAmount, decimal? receivedAmountUsd, int receivedSymbolIndex, bool? isReverted)
        {
            TxHash = txHash;
            Protocol = protocol;
            DebtPurchaseAmount = debtPurchaseAmount;
            DebtPurchaseAmountUsd = debtPurchaseAmountUsd;
            ReceivedAmount = receivedAmount;
            DebtSymbolIndex = debtSymbolIndex;
            MEVAmountUsd = receivedAmountUsd;
            ReceivedSymbolIndex = receivedSymbolIndex;
            IsReverted = isReverted;
        }

        [JsonPropertyName("p")]
        public ProtocolLiquidation Protocol { get; set; }

        [JsonPropertyName("d")]
        public BigInteger? DebtPurchaseAmount { get; set; }

        [JsonPropertyName("du")]
        public decimal? DebtPurchaseAmountUsd { get; set; }

        [JsonPropertyName("r")]
        public BigInteger? ReceivedAmount { get; set; }

        [JsonPropertyName("a")]
        public int DebtSymbolIndex { get; set; }

        [JsonPropertyName("b")]
        public int ReceivedSymbolIndex { get; set; }

        [JsonPropertyName("v")]
        public bool? IsReverted { get; set; }

        [JsonPropertyName("u")]
        public decimal? MEVAmountUsd { get; set; }

        [JsonPropertyName("h")]
        public string TxHash { get; set; }

        [JsonIgnore]
        public int? TxIndex => null;

        [JsonIgnore]
        public MEVType MEVType => MEVType.Liquid;

        [JsonIgnore]
        public MEVClass MEVClass => MEVClass.Unclassified;

        [JsonIgnore]
        public string? MEVDetail { get; set; } = null;

        [JsonIgnore]
        public string? ActionSummary { get; set; }

        [JsonIgnore]
        public string? ActionDetail { get; set; }

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
            if (MEVAmountUsd != null)
                sb.Append(MEVAmountUsd);
            else
                sb.Append("?");
            sb.Append(" (");
            sb.Append(ReceivedAmount);
            sb.AppendLine(").");

            return sb.ToString();
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
        public string? MEVDetail { get; set; } = null;

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

        public MEVDisplay(MEVClass mevClass, string name, bool isVisible, string cssClass)
        {
            Index = (int)mevClass;
            Class = mevClass;
            Name = name;
            IsVisible = isVisible;
            CssClass = cssClass;
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
            new MEVDisplay(MEVClass.All, "", false, "mev-any"),
            new MEVDisplay(MEVClass.Unclassified, "Unclassified", true, "mev-un"),
            new MEVDisplay(MEVClass.Positive, "Positive", true, "mev-pos"),
            new MEVDisplay(MEVClass.Neutral, "Neutral", true, "mev-neu"),
            new MEVDisplay(MEVClass.Toxic, "Toxic", true, "mev-tox"),
            new MEVDisplay(MEVClass.Info, "Info", true, "mev-inf")};

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
        public static bool GetSandwichParameters(MEVBlock mb, int index, out ZMDecimal[]? a, out ZMDecimal[]? b, out MEVFrontrun front, out MEVBackrun back, out MEVSandwiched[] sandwiched)
        {
            if (index >= mb.Frontruns.Count ||
                index >= mb.Backruns.Count ||
                index >= mb.Sandwiched.Count)
            {
                a = null;
                b = null;
                front = null;
                back = null;
                sandwiched = null;
                return false;
            }

            front = mb.Frontruns[index];
            back = mb.Backruns[index];
            sandwiched = mb.Sandwiched[index];

            a = new ZMDecimal[sandwiched.Length + 2];
            b = new ZMDecimal[sandwiched.Length + 2];

            int txIndex = front.Swap.TxIndex.Value;
            a[0] = front.Swap.AmountIn;
            b[0] = front.Swap.AmountOut;

            for (int i = 0; i < sandwiched.Length; i++)
            {
                if (txIndex != sandwiched[i].TxIndex.Value) // deal with multiple sandwiched swaps in the same transaction
                    if (++txIndex != sandwiched[i].TxIndex.Value) return false;
                a[i + 1] = sandwiched[i].Swap.AmountIn;
                b[i + 1] = sandwiched[i].Swap.AmountOut;
            }

            if (++txIndex != back.TxIndex.Value) return false;
            a[sandwiched.Length + 1] = back.Swap.AmountOut; // amounts reversed as backruns trade against frontrun and sandwiched
            b[sandwiched.Length + 1] = back.Swap.AmountIn;
            return true;
        }

        public static void CalculateSwaps(ZMDecimal x, ZMDecimal y, ZMDecimal c, ZMDecimal[] a, ZMDecimal[] b, bool[] isBA, out ZMDecimal x_, out ZMDecimal y_, out ZMDecimal[] a_, out ZMDecimal[] b_)
        {
            x_ = x;
            y_ = y;

            a_ = new ZMDecimal[a.Length];
            b_ = new ZMDecimal[b.Length];

            for (int i = 0; i < a.Length; i++)
            {
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
        // victim impact = returned b - original b
        public static ZMDecimal[] FrontrunVictimImpact(ZMDecimal x, ZMDecimal y, ZMDecimal c, ZMDecimal[] a, ZMDecimal[] b, bool[] isBA, int sandwichedFrom, int sandwichedTo, ZMDecimal[] a_, ZMDecimal[] b_)
        {
            MEVCalc.CopySwaps(a, b, out var an, out var bn);

            // zero the frontrun in amount and recalculate to find what each victim transaction would have recieved had it not been frontrun
            an[0] = 0;
            MEVCalc.CalculateSwaps(x, y, c, an, bn, isBA, out var xv, out var yv, out var av, out var bv);
            MEVCalc.FinalizeSwapCalculations(a, b, a_, b_, av, bv, out var af, out var bf);
            return bf;
        }

        public static ZMDecimal SandwichProfitBackHeavy(ZMDecimal x, ZMDecimal y, ZMDecimal c, ZMDecimal[] a, ZMDecimal[] b, bool[] isBA, int sandwichedFrom, int sandwichedTo, ZMDecimal[] a_, ZMDecimal[] b_, out ZMDecimal? backrunVictimImpact)
        {
            MEVCalc.CopySwaps(a, b, out var an, out var bn);

            // set back in to front out and recalculate
            int backIndex = b.Length - 1;
            if (b[0] < bn[backIndex])
                bn[backIndex] = b[0];
            MEVCalc.CalculateSwaps(x, y, c, an, bn, isBA, out var xv, out var yv, out var av, out var bv);
            MEVCalc.FinalizeSwapCalculations(a, b, a_, b_, av, bv, out var af, out var bf);

            // backrun victim impact = calculated back out - original back out
            backrunVictimImpact = af[backIndex] - a[backIndex];

            // sandwich profit = calculated back out - calculated front in
            return af[backIndex] - af[0];
        }

        public static ZMDecimal SandwichProfitFrontHeavy(ZMDecimal x, ZMDecimal y, ZMDecimal c, ZMDecimal[] a, ZMDecimal[] b, bool[] isBA, int sandwichedFrom, int sandwichedTo, ZMDecimal[] a_, ZMDecimal[] b_)
        {
            MEVCalc.CopySwaps(a, b, out var an, out var bn);

            // use back in as front out and reverse swap the front in from this new front out
            int backIndex = b.Length - 1;
            var newFrontIn = MEVCalc.GetAFromB(x, y, c, b[backIndex]);
            if (newFrontIn < an[0])
                an[0] = newFrontIn;
            MEVCalc.CalculateSwaps(x, y, c, an, bn, isBA, out var xv, out var yv, out var av, out var bv);
            MEVCalc.FinalizeSwapCalculations(a, b, a_, b_, av, bv, out var af, out var bf);

            // sandwich profit = calculated back out - calculated front in
            return af[backIndex] - af[0];
        }

        public static void PoolFromSwapsABAB(ZMDecimal[] a, ZMDecimal[] b, ZMDecimal c, out ZMDecimal x, out ZMDecimal y)
        {
            x = ((a[0] * a[1] * b[1] * c) + (a[0].Pow(2) * b[1])) / ((a[1] * b[0]) - (a[0] * b[1]));
            y = ((((b[0] * ((a[1] * b[1]) - (a[0] * b[1]))) + (a[1] * b[0].Pow(2))) * c) + (a[0] * b[0] * b[1])) / ((a[1] * b[0] * c) - (a[0] * b[1] * c));
        }

        public static void PoolFromSwapsABBA(ZMDecimal[] a, ZMDecimal[] b, ZMDecimal c, out ZMDecimal x, out ZMDecimal y)
        {
            var cPow2 = c.Pow(2);
            x = -(((a[0].Pow(2) * b[1]) - (a[0] * a[1] * b[1])) * cPow2) / ((a[0] * b[1] * cPow2) - (a[1] * b[0]));
            y = ((a[0] * b[0] * b[1] * cPow2) + (b[0] * a[1] * b[1] * c) - (b[0] * a[0] * b[1] * c) - (a[1] * b[0].Pow(2))) / ((a[0] * b[1] * cPow2) - (a[1] * b[0]));
        }

        public static void PoolFromSwapsABBA(ZMDecimal a0, ZMDecimal a1, ZMDecimal b0, ZMDecimal b1, ZMDecimal c, out ZMDecimal x, out ZMDecimal y)
        {
            var cPow2 = c.Pow(2);
            x = -(((a0.Pow(2) * b1) - (a0 * a1 * b1)) * cPow2) / ((a0 * b1 * cPow2) - (a1 * b0));
            y = ((a0 * b0 * b1 * cPow2) + (b0 * a1 * b1 * c) - (b0 * a0 * b1 * c) - (a1 * b0.Pow(2))) / ((a0 * b1 * cPow2) - (a1 * b0));
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

        public static decimal? SwapUsd(MEVSwap rateSwap, ZMDecimal? amount, bool doNullTiny = true)
        {
            if (amount == null)
                return null;

            var usdRate = rateSwap.OutUsdRate();
            if (usdRate != null)
            {
                var victimImpactUsd = amount.Value * usdRate.Value;
                if (!doNullTiny || victimImpactUsd > 0.01 || victimImpactUsd < -0.01) // ignore tiny amounts
                    return victimImpactUsd.ToUsd();
            }
            return null;
        }
    }
}