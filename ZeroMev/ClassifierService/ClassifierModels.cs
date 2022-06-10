using C5;
using Microsoft.EntityFrameworkCore;
using System.Collections;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Text.Json;
using ZeroMev.MevEFC;
using ZeroMev.Shared;
using ZeroMev.SharedServer;

namespace ZeroMev.ClassifierService
{
    // lessens the code needed in light client classes such as MEVBlock
    public static class MEVHelper
    {
        // for 0.3% commission, c = 0.997 
        public static void SimUniswap2(int count, double isBaAbove, bool isABBA, bool isSandwichSim, ZMDecimal c, ZMDecimal x, ZMDecimal y, out ZMDecimal[] a, out ZMDecimal[] b, out ZMDecimal[] xOut, out ZMDecimal[] yOut, out bool[] isBA)
        {
            double amountFrac = 0.01;

            a = new ZMDecimal[count];
            b = new ZMDecimal[count];
            xOut = new ZMDecimal[count];
            yOut = new ZMDecimal[count];
            isBA = new bool[count];

            Random r = new Random();

            for (int i = 0; i < count; i++)
            {
                if (isSandwichSim)
                {
                    isBA[i] = (i == count - 1);
                }
                else
                {
                    // force the first two txs to be either ABAB or ABBA (based on isABBA), and the rest trade random directions (based on isBaAbove)
                    if (i == 0)
                        isBA[0] = false;
                    else if (i == 1)
                        isBA[1] = isABBA ? true : false;
                    else
                        isBA[i] = (r.NextDouble() > isBaAbove);
                }

                if (!isBA[i])
                {
                    // a to b
                    a[i] = x * ((ZMDecimal)(amountFrac * r.NextDouble())); // input amount as a random % of pool (to stop the pool going to zero)
                    b[i] = MEVCalc.SwapOutputAmount(ref x, ref y, c, a[i]);
                }
                else
                {
                    // b to a
                    b[i] = y * ((ZMDecimal)(amountFrac * r.NextDouble())); // input amount as a random % of pool (to stop the pool going to zero)
                    if (isSandwichSim)
                        b[i] = b[0];
                    a[i] = MEVCalc.SwapOutputAmount(ref y, ref x, c, b[i]);
                }

                xOut[i] = x;
                yOut[i] = y;
            }
        }

        public static void RunSimUniswap(bool isABBA)
        {
            // test our uniswap pool solvers for ABAB or ABBA (based on isABBA)
            ZMDecimal x = 10000;
            ZMDecimal y = 10;
            ZMDecimal c = 0.997;
            const int dec = 5;

            SimUniswap2(100, 0.5, isABBA, false, c, x, y, out var real_a, out var real_b, out var xOut, out var yOut, out var isBA);

            ZMDecimal x_, y_;
            if (!isABBA)
                MEVCalc.PoolFromSwapsABAB(real_a, real_b, c, out x_, out y_);
            else
                MEVCalc.PoolFromSwapsABBA(real_a, real_b, c, out x_, out y_);

            Debug.WriteLine("_ = calculated");
            Debug.WriteLine($"x\t{x.RoundAwayFromZero(dec)}\tx_\t{x_.RoundAwayFromZero(dec)}");
            Debug.WriteLine($"y\t{y.RoundAwayFromZero(dec)}\ty_\t{y_.RoundAwayFromZero(dec)}");
            Debug.WriteLine("");

            // test forwards
            Debug.WriteLine($"ba?\ta\tb\tout_\tx\tx_\ty\ty_");
            for (int i = 0; i < real_a.Length; i++)
            {
                ZMDecimal out_;
                if (!isBA[i])
                    out_ = MEVCalc.SwapOutputAmount(ref x_, ref y_, c, real_a[i]);
                else
                    out_ = MEVCalc.SwapOutputAmount(ref y_, ref x_, c, real_b[i]);
                Debug.WriteLine($"{isBA[i]}\t{real_a[i].Shorten()}\t{real_b[i].Shorten()}\t{out_.Shorten()}\t{xOut[i].Shorten()}\t{x_.Shorten()}\t{yOut[i].Shorten()}\t{y_.Shorten()}");
            }

            Debug.WriteLine("");
            Debug.WriteLine("in reverse");
            Debug.WriteLine("");

            // test backwards
            Debug.WriteLine($"ba?\ta\tb\tin_\tx\tx_\ty\ty_");
            for (int i = real_a.Length - 1; i >= 0; i--)
            {
                ZMDecimal in_;
                if (!isBA[i])
                    in_ = MEVCalc.SwapOutputAmountReversed(ref y_, ref x_, c, real_b[i]);
                else
                    in_ = MEVCalc.SwapOutputAmountReversed(ref x_, ref y_, c, real_a[i]);
                Debug.WriteLine($"{isBA[i]}\t{real_a[i].Shorten()}\t{real_b[i].Shorten()}\t{in_.Shorten()}\t{xOut[i].Shorten()}\t{x_.Shorten()}\t{yOut[i].Shorten()}\t{y_.Shorten()}");
            }
        }

        public static bool GetSandwichParametersFiltered(MEVBlock mb, int index, out ZMDecimal[]? a, out ZMDecimal[]? b, ProtocolSwap protocol = ProtocolSwap.Unknown)
        {
            bool success = MEVCalc.GetSandwichParameters(mb, index, out a, out b, out var front, out var back, out var sandwiched);

            // optionally filter by protocol (unknown means any)
            if (protocol != ProtocolSwap.Unknown &&
                (front.Swap.Protocol != protocol ||
                back.Swap.Protocol != protocol))
                return false;

            return success;
        }

        public static int GetSymbolIndex(MEVBlock? mb, string? tokenAddress)
        {
            if (mb == null || tokenAddress == null) return Symbol.UnknownSymbolIndex;
            if (tokenAddress == "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee")
                tokenAddress = "0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2"; // interpret the eth burn address as eth
            int index = mb.Symbols.FindIndex(x => { return x.TokenAddress == tokenAddress; });
            if (index == -1)
            {
                var zmToken = Tokens.GetFromAddress(tokenAddress);
                if (zmToken == null)
                    return Symbol.UnknownSymbolIndex; // meaning unknown symbol

                string? image = null;
                if (zmToken.Image != null)
                    image = zmToken.Image.Replace(@"/images/", "");

                var symbol = new Symbol(zmToken.Name, image, tokenAddress);
                mb.Symbols.Add(symbol);
                return mb.Symbols.Count - 1;
            }
            return index;
        }

        public static ProtocolSwap GetProtocolSwap(string protocol, string abiName)
        {
            if (protocol == null || protocol == "")
            {
                switch (abiName)
                {
                    case "UniswapV3Pool":
                        return ProtocolSwap.Uniswap3;
                    case "UniswapV2Pair":
                        return ProtocolSwap.Uniswap2;
                    default:
                        return ProtocolSwap.Unknown;
                }
            }

            switch (protocol)
            {
                case "curve":
                    return ProtocolSwap.Curve;
                case "balancer_v1":
                    return ProtocolSwap.Balancer1;
                case "bancor":
                    return ProtocolSwap.Bancor;
                case "0x":
                    return ProtocolSwap.ZeroX;
                default:
                    return ProtocolSwap.Unknown;
            }
        }

        public static MEVSwap BuildMEVSwap(MEVBlock mb, Swap swap, ZMSwap zmSwap)
        {
            int symbolIn = GetSymbolIndex(mb, swap.TokenInAddress);
            int symbolOut = GetSymbolIndex(mb, swap.TokenOutAddress);
            var protocol = GetProtocolSwap(swap.Protocol, swap.AbiName);

            var mevSwap = new MEVSwap(new TraceAddress(swap.TraceAddress), protocol, symbolIn, symbolOut, zmSwap.InAmount, zmSwap.OutAmount, zmSwap.InRateUsd, zmSwap.OutRateUsd);
            return mevSwap;
        }

        public static bool DoAddMEV(MEVBlock mb, IMEV mev)
        {
            if (mev.TxIndex == null) return true;
            if (mb.ExistingMEV[mev.TxIndex.Value] == null)
            {
                mb.ExistingMEV[mev.TxIndex.Value] = mev;
                return true;
            }
            return false;
        }

        public static ProtocolLiquidation GetProtocolLiquidation(string? protocol)
        {
            if (protocol == null)
                return ProtocolLiquidation.Unknown;

            switch (protocol)
            {
                case "compound_v2":
                    return ProtocolLiquidation.CompoundV2;
                case "aave":
                    return ProtocolLiquidation.Aave;
                default:
                    return ProtocolLiquidation.Unknown;
            }
        }

        public static MEVError GetErrorFrom(string? errorText)
        {
            if (errorText == null || errorText == "")
                return MEVError.None;

            switch (errorText[0])
            {
                case 'O':
                    return MEVError.OutOfGas;
                case 'R':
                    return MEVError.Reverted;
                default:
                    return MEVError.Unknown;
            }
        }

        public static ProtocolNFT GetProtocolNFT(string protocol)
        {
            return protocol == "opensea" ? ProtocolNFT.Opensea : ProtocolNFT.Unknown;
        }

        public static string TxKey(string txHash, int[] traceAddress)
        {
            return $"{txHash}[{string.Join(",", traceAddress)}]";
        }

        public static string TxKey(Swap swap)
        {
            return $"{swap.TransactionHash}[{string.Join(",", swap.TraceAddress)}]";
        }

        public static decimal? GetUsdAmount(string token, ZMDecimal? amount, out ZMDecimal? newAmount)
        {
            if (amount == null || token == null)
            {
                newAmount = null;
                return null;
            }

            var zmToken = Tokens.GetFromAddress(token);
            var usdRate = XRates.GetUsdRate(token);

            if (zmToken == null || zmToken.Divisor == null || usdRate == null)
            {

                newAmount = null;
                return null;
            }
            
            newAmount = amount.Value / zmToken.Divisor;
            return (decimal)(newAmount.Value * usdRate.Value).ToUsd();
        }

        public static void DebugMevBlock(MEVBlock mb, StreamWriter sw)
        {
            for (int i = 0; i < mb.SwapsTx.Count; i++) DebugMev(mb.SwapsTx[i], mb, i, sw);
            for (int i = 0; i < mb.Backruns.Count; i++) DebugMev(mb.Backruns[i], mb, i, sw);
            for (int i = 0; i < mb.Sandwiched.Count; i++)
                foreach (var s in mb.Sandwiched[i])
                    DebugMev(s, mb, i, sw);
            for (int i = 0; i < mb.Frontruns.Count; i++) DebugMev(mb.Frontruns[i], mb, i, sw);
            for (int i = 0; i < mb.Arbs.Count; i++) DebugMev(mb.Arbs[i], mb, i, sw);
            for (int i = 0; i < mb.Liquidations.Count; i++) DebugMev(mb.Liquidations[i], mb, i, sw);
            for (int i = 0; i < mb.NFTrades.Count; i++) DebugMev(mb.NFTrades[i], mb, i, sw);
        }

        private static void DebugMev(IMEV mev, MEVBlock mb, int mevIndex, StreamWriter sw)
        {
            mev.Cache(mb, mevIndex);
            DebugSandwiches(mev, mb, mevIndex, sw);
        }

        private static void DebugSandwiches(IMEV mev, MEVBlock mb, int mevIndex, StreamWriter sw)
        {
            if (mev.MEVType == MEVType.Frontrun)
            {
                if (mevIndex >= mb.Backruns.Count) return;
                MEVBackrun br = mb.Backruns[mevIndex];
                if (br == null) return;

                if (mevIndex >= mb.Sandwiched.Count) return;
                MEVSandwiched[] sd = mb.Sandwiched[mevIndex];
                if (sd == null) return;
                decimal sumUserLoss = 0;
                foreach (var s in sd)
                    sumUserLoss += s.MEVAmountUsd ?? 0;

                var fr = (MEVFrontrun)mev;

                ZMDecimal arbsUsd = 0;
                foreach (var arb in mb.Arbs)
                    arbsUsd += arb.MEVAmountUsd ?? 00;

                AltSandwichProfit(mev, mb, mevIndex, out var profitNaive, out var profitFrontrun, out var profitBackrun, out var profitRateDiff2Way, out var profitFbOnePercent);
                sw.WriteLine($"{mb.BlockNumber}\t{mev.TxIndex}\t{arbsUsd}\t{sumUserLoss}\t{br.MEVAmountUsd}\t{fr.SandwichProfitUsd}\t{MEVCalc.SwapUsd(br.Swap.OutUsdRate(), profitNaive)}\t{MEVCalc.SwapUsd(br.Swap.OutUsdRate(), profitFrontrun)}\t{MEVCalc.SwapUsd(br.Swap.OutUsdRate(), profitBackrun)}\t{MEVCalc.SwapUsd(br.Swap.OutUsdRate(), profitRateDiff2Way)}\t{MEVCalc.SwapUsd(br.Swap.OutUsdRate(), profitFbOnePercent)}");
            }
        }

        public static void AltSandwichProfit(IMEV mev, MEVBlock mb, int mevIndex, out ZMDecimal? profitNaive, out ZMDecimal? profitFrontrun, out ZMDecimal? profitBackrun, out ZMDecimal? profitRateDiff2Way, out ZMDecimal? profitFbOnePercent)
        {
            profitFbOnePercent = null;
            profitNaive = null;
            profitFrontrun = null;
            profitBackrun = null;
            profitRateDiff2Way = null;

            if (!MEVCalc.GetSandwichParameters(mb, mevIndex, out var a, out var b, out var front, out var back, out var sandwiched))
                return;

            int backIndex = a.Length - 1;

            profitNaive = a[backIndex] - a[0];

            profitFrontrun = (b[0] * (a[backIndex] / b[backIndex]) - a[0]);
            profitBackrun = (a[backIndex] - (b[backIndex] * (a[0] / b[0])));
            var profitFrontrunABS = profitFrontrun < 0 ? -profitFrontrun : profitFrontrun;
            var profitBackrunABS = profitBackrun < 0 ? -profitBackrun : profitBackrun;
            profitRateDiff2Way = profitFrontrunABS < profitBackrunABS ? profitFrontrun : profitBackrun;

            var amount_percent_difference = (b[0] / b[backIndex]) - 1.0;
            if (amount_percent_difference < 0.01)
                profitFbOnePercent = profitNaive;
        }
    }

    public class SwapRecord
    {
        public SwapRecord(Swap swap, ZMSwap zMSwap)
        {
            Swap = swap;
            ZMSwap = zMSwap;
        }

        public Swap Swap { get; private set; }
        public ZMSwap ZMSwap { get; private set; }
    }

    public class ArbSwap
    {
        public ArbSwap(Arbitrage arb, ArbitrageSwap swap)
        {
            Arb = arb;
            Swap = swap;
        }

        public Arbitrage Arb { get; private set; }
        public ArbitrageSwap Swap { get; private set; }
    }

    // load mev-inspect data for a chosen block range to be passed to the zm classifier
    public class BlockProcess
    {
        public List<Swap> Swaps { get; set; }
        public Dictionary<string, ArbSwap> ArbitrageSwaps { get; set; }
        public Dictionary<string, string> Sandwiches { get; set; }
        public Dictionary<string, Swap> SandwichesFrontrun { get; set; }
        public Dictionary<string, Swap> SandwichesBackrun { get; set; }
        public Dictionary<string, Swap> SandwichedSwaps { get; set; }
        public List<Liquidation> Liquidations { get; set; }
        public List<NftTrade> NftTrades { get; set; }
        public List<PunkBid> PunkBids { get; set; }
        public List<PunkBidAcceptance> PunkBidAcceptances { get; set; }
        public List<PunkSnipe> PunkSnipes { get; set; }
        public List<ZmBlock> ZmBlocks { get; set; }

        DEXs _dexs = null;
        Dictionary<long, MEVBlock> _mevBlocks = new Dictionary<long, MEVBlock>();

        const string UNISWAP_V2_ROUTER = "0x7a250d5630b4cf539739df2c5dacb4c659f2488d";
        const string UNISWAP_V3_ROUTER = "0xe592427a0aece92de3edee1f18e0157c05861564";
        const string UNISWAP_V3_ROUTER_2 = "0x68b3465833fb72a70ecdf485e0e4c7bd8665fc45";

        static System.Collections.Generic.HashSet<string> FlashbotsSummaryTokens = new System.Collections.Generic.HashSet<string>()
            {"0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee",
            "0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2",
            "0x2260fac5e5542a773aa44fbcfedf7c193bc2c599",
            "0x514910771af9ca656af840dff83e8264ecf986ca",
            "0x0bc529c00c6401aef6d220be8c6ea1667f6ad93e",
            "0x7fc66500c84a76ad7e9c93437bfc5ac33e2ddae9",
            "0x1f9840a85d5af5bf1d1762f925bdaddc4201f984",
            "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48",
            "0x6b175474e89094c44da98b954eedeac495271d0f",
            "0x408e41876cccdc0f92210600ef50372656052a38",
            "0x39aa39c021dfbae8fac545936693ac917d5e7563",
            "0x5d3a536e4d6dbd6114cc1ead35777bab948e3643",
            "0x4ddc2d193948926d02f9b1fe9e1daa0718270ed5",
            "0xc11b1268c1a384e55c48c2391d8d480264a3a7f4"};

        static HttpClient _http = new HttpClient();

        public BlockProcess(DEXs dexs)
        {
            _dexs = dexs;
        }

        public static BlockProcess Load(long fromBlockNumber, long toBlockNumber, DEXs dexs)
        {
            BlockProcess bi = new BlockProcess(dexs);

            // due to an issue with mev-inspect sandwich detection, sandwich table rows are re-calculated before processing rather than being loaded from the db
            bi.Sandwiches = new Dictionary<string, string>();
            bi.SandwichesFrontrun = new Dictionary<string, Swap>();
            bi.SandwichesBackrun = new Dictionary<string, Swap>();
            bi.SandwichedSwaps = new Dictionary<string, Swap>();

            using (var db = new zeromevContext())
            {
                bi.Swaps = (from s in db.Swaps
                            where s.BlockNumber >= fromBlockNumber && s.BlockNumber < toBlockNumber
                            orderby s.BlockNumber, s.TransactionPosition, s.TraceAddress
                            select s).ToList();

                bi.ArbitrageSwaps = (from s in db.ArbitrageSwaps
                                     join a in db.Arbitrages on s.ArbitrageId equals a.Id
                                     where a.BlockNumber >= fromBlockNumber && a.BlockNumber < toBlockNumber
                                     select new ArbSwap(a, s)).ToSafeDictionary(x => MEVHelper.TxKey(x.Swap.SwapTransactionHash, x.Swap.SwapTraceAddress), x => x);

                bi.Liquidations = (from s in db.Liquidations
                                   where s.BlockNumber >= fromBlockNumber && s.BlockNumber < toBlockNumber
                                   orderby s.BlockNumber, s.TransactionHash, s.TraceAddress
                                   select s).ToList();

                bi.NftTrades = (from n in db.NftTrades
                                where n.BlockNumber >= fromBlockNumber && n.BlockNumber < toBlockNumber
                                orderby n.BlockNumber, n.TransactionPosition, n.TraceAddress
                                select n).ToList();

                bi.ZmBlocks = (from z in db.ZmBlocks
                               where z.BlockNumber >= fromBlockNumber && z.BlockNumber < toBlockNumber
                               orderby z.BlockNumber
                               select z).ToList();
            }

            bi.CalculateSandwiches();
            return bi;
        }

        private void CalculateSandwiches()
        {
            int i = 0;
            while (i < Swaps.Count)
                i = GetSandwichStartingWithSwap(Swaps[i], i + 1);
        }

        private int GetSandwichStartingWithSwap(Swap frontSwap, int restIndex)
        {
            if (restIndex >= Swaps.Count) return restIndex;

            var sandwicher = frontSwap.ToAddress;
            if (sandwicher == UNISWAP_V2_ROUTER ||
                sandwicher == UNISWAP_V3_ROUTER ||
                sandwicher == UNISWAP_V3_ROUTER_2)
                return restIndex;

            List<Swap> sandwichedSwaps = null;

            int frontIndex = restIndex - 1;
            for (int i = restIndex; i < Swaps.Count; i++)
            {
                Swap otherSwap = Swaps[i];
                if (otherSwap.BlockNumber != frontSwap.BlockNumber)
                    return restIndex;

                if (otherSwap.TransactionHash == frontSwap.TransactionHash)
                    continue;

                if (otherSwap.Protocol != frontSwap.Protocol ||
                    otherSwap.AbiName != frontSwap.AbiName)
                    continue;

                if (otherSwap.ContractAddress == frontSwap.ContractAddress)
                {
                    if (otherSwap.TokenInAddress == frontSwap.TokenInAddress
                            && otherSwap.TokenOutAddress == frontSwap.TokenOutAddress
                            && otherSwap.FromAddress != sandwicher
                            && otherSwap.Protocol == frontSwap.Protocol)
                    {
                        if (sandwichedSwaps == null)
                            sandwichedSwaps = new List<Swap>();
                        sandwichedSwaps.Add(otherSwap);
                    }
                    else if (otherSwap.TokenOutAddress == frontSwap.TokenInAddress
                                && otherSwap.TokenInAddress == frontSwap.TokenOutAddress
                                && otherSwap.FromAddress == sandwicher
                                && otherSwap.Protocol == frontSwap.Protocol)
                    {
                        if (sandwichedSwaps != null)
                        {
                            try
                            {
                                // groups here perform the same function as the Sandwiches table
                                string? sandwichId = Guid.NewGuid().ToString();

                                var frontKey = MEVHelper.TxKey(frontSwap);
                                var backKey = MEVHelper.TxKey(otherSwap);

                                SandwichesFrontrun.Add(frontKey, frontSwap);
                                SandwichesBackrun.Add(backKey, otherSwap);

                                Sandwiches.Add(frontKey, sandwichId);
                                Sandwiches.Add(backKey, sandwichId);

                                foreach (var ss in sandwichedSwaps)
                                {
                                    var key = MEVHelper.TxKey(ss);
                                    SandwichedSwaps.Add(key, ss);
                                    Sandwiches.Add(key, sandwichId);
                                }
                            }
                            catch (Exception ex)
                            {
                                // in case of duplicate key errors
                            }
                            return i + 1; // this skip past sandwiched transactions resolves the mev-inspect issue
                        }
                    }
                }
            }
            return restIndex;
        }

        public void Run()
        {
            Tokens.Load();

            MEVFrontrun? frontrun = null;
            string? lastArbHash = null;
            MEVArb? lastArb = null;
            string? lastSandwichedHash = null;
            MEVSandwiched? lastSandwiched = null;
            string? sandwichId = null;

            MEVBlock? mevBlock = null;
            List<DateTime>? arrivals = null;
            BitArray? txStatus = null;
            List<MEVSandwiched> sandwiched = new List<MEVSandwiched>();

            // swaps, sandwiches and arbs
            ZMSwap?[] zmSwaps = new ZMSwap[Swaps.Count];
            long? skippedBlockNumber = null;
            for (int i = 0; i < Swaps.Count; i++)
            {
                Swap s = Swaps[i];

                // detect new block number
                if (mevBlock == null || s.BlockNumber != mevBlock.BlockNumber)
                {
                    // process liquidations and nfts in parallel with swaps to ensure we get decent exchange rates
                    // note that liquidations and nfts xrates are set a block granularity, where as arbs/swaps/sandwiches are set a tx level granularity
                    var tempMevBlock = mevBlock;
                    ProcessLiquidations(skippedBlockNumber, s.BlockNumber, ref mevBlock, ref arrivals, ref txStatus);
                    ProcessNfts(skippedBlockNumber, s.BlockNumber, ref mevBlock, ref arrivals, ref txStatus);
                    skippedBlockNumber = s.BlockNumber + 1;

                    // reset on block boundaries
                    frontrun = null;
                    lastArbHash = null;
                    lastArb = null;
                    sandwiched.Clear();

                    mevBlock = tempMevBlock;
                    if (!GetMEVBlock(s.BlockNumber, ref mevBlock, ref arrivals, ref txStatus))
                        continue;
                }

                // consider swaps with zero amount invalid (they break exchange rate calculations)
                if (s.TokenInAmount == 0 || s.TokenOutAmount == 0)
                    continue;

                // skip reverted
                if (txStatus != null && s.TransactionPosition.HasValue && txStatus.Count > s.TransactionPosition && !txStatus.Get(s.TransactionPosition.Value))
                    continue;

                if (s.TransactionPosition == null || s.Error != null) continue;
                DateTime? arrival = arrivals != null ? arrivals[s.TransactionPosition.Value] : null;
                var zmSwap = _dexs.Add(s, arrival, out var pair);
                zmSwaps[i] = zmSwap;

                bool isSandwichTx = false;
                string sKey = MEVHelper.TxKey(s);

                // sandwiches
                if (this.SandwichesFrontrun.TryGetValue(sKey, out var sandwichFrontrun))
                {
                    // sandwich frontrun
                    isSandwichTx = true;
                    sandwichId = Sandwiches[sKey];
                    if (sandwichId != null)
                        frontrun = new MEVFrontrun(s.TransactionPosition.Value, MEVHelper.BuildMEVSwap(mevBlock, s, zmSwap));
                }
                else if (this.SandwichesBackrun.TryGetValue(sKey, out var sandwichBackrun))
                {
                    isSandwichTx = true;
                    if (Sandwiches[sKey] == sandwichId)
                    {
                        var backrun = new MEVBackrun(s.TransactionPosition.Value, MEVHelper.BuildMEVSwap(mevBlock, s, zmSwap));

                        // front/sandwiched and backrun instances can access each other using the same index across each collection
                        if (frontrun != null && sandwiched.Count > 0)
                        {
                            mevBlock.Frontruns.Add(frontrun);
                            mevBlock.Sandwiched.Add(sandwiched.ToArray());
                            mevBlock.Backruns.Add(backrun);
                            MEVHelper.DoAddMEV(mevBlock, frontrun);
                            foreach (var sw in sandwiched)
                                MEVHelper.DoAddMEV(mevBlock, sw);
                            MEVHelper.DoAddMEV(mevBlock, backrun);
                        }

                        frontrun = null;
                        sandwiched.Clear();
                    }
                }
                else if (this.SandwichedSwaps.TryGetValue(sKey, out var sandwichedSwap))
                {
                    isSandwichTx = true;
                    if (Sandwiches[sKey] == sandwichId)
                    {
                        if (sandwichedSwap.TransactionHash == lastSandwichedHash)
                        {
                            lastSandwiched.AddSandwichedSwap(MEVHelper.BuildMEVSwap(mevBlock, s, zmSwap));
                        }
                        else
                        {
                            lastSandwichedHash = sandwichedSwap.TransactionHash;
                            lastSandwiched = new MEVSandwiched(s.TransactionPosition, MEVHelper.BuildMEVSwap(mevBlock, s, zmSwap));
                            sandwiched.Add(lastSandwiched);
                        }
                    }
                }

                // arbs
                if (this.ArbitrageSwaps.TryGetValue(sKey, out var arb))
                {
                    if (isSandwichTx)
                    {
                        // if we are mid sandwich, remove the arb
                        if (arb.Swap.SwapTransactionHash == lastArbHash)
                            mevBlock.Arbs.Remove(lastArb);

                        // set to skip any further legs of this arb
                        lastArb = null;
                        lastArbHash = arb.Swap.SwapTransactionHash;
                    }
                    else if (arb.Swap.SwapTransactionHash != lastArbHash)
                    {
                        // create the mev arb on the first arb swap
                        ZMDecimal? newAmount;
                        decimal? arbProfitUsd = MEVHelper.GetUsdAmount(arb.Arb.ProfitTokenAddress, arb.Arb.ProfitAmount, out newAmount);
                        if (arbProfitUsd != null) arbProfitUsd = -arbProfitUsd;
                        int arbCase = (s.FromAddress == s.ToAddress) ? 2 : 1;
                        var mevArb = new MEVArb(s.TransactionPosition, MEVClass.Unclassified, arbProfitUsd, arbCase, MEVHelper.BuildMEVSwap(mevBlock, s, zmSwap));
                        lastArbHash = arb.Swap.SwapTransactionHash;

                        // only add if it would also be included in the Flashbots mev_summary table. There are problems with false negative profit values with other mev-inspect arb calculations.
                        if (FlashbotsSummaryTokens.Contains(arb.Arb.ProfitTokenAddress))
                        {
                            lastArb = mevArb;
                            MEVHelper.DoAddMEV(mevBlock, mevArb);
                            mevBlock.Arbs.Add(mevArb);
                        }
                    }
                    else if (lastArb != null)
                    {
                        // if inputs and outputs don't match, consider this a case I arb (see mev-inspect-py arb calcs for details)
                        if (s.FromAddress != s.ToAddress)
                            lastArb.ArbCase = 1;

                        lastArb.AddArbSwap(MEVHelper.BuildMEVSwap(mevBlock, s, zmSwap));
                    }
                }
            }

            // remaining liquidations and nfts
            mevBlock = null;
            ProcessLiquidations(skippedBlockNumber ?? 0, long.MaxValue, ref mevBlock, ref arrivals, ref txStatus);
            mevBlock = null;
            ProcessNfts(skippedBlockNumber ?? 0, long.MaxValue, ref mevBlock, ref arrivals, ref txStatus);

            // allocate all remaining swaps
            mevBlock = null;
            for (int i = 0; i < Swaps.Count; i++)
            {
                Swap s = Swaps[i];
                var zmSwap = zmSwaps[i];

                if (mevBlock == null || s.BlockNumber != mevBlock.BlockNumber)
                {
                    if (!GetMEVBlock(s.BlockNumber, ref mevBlock, ref arrivals, ref txStatus, false))
                        continue;
                    if (mevBlock == null)
                        continue;
                }

                if (zmSwap != null && s.TransactionPosition != null)
                {
                    int txIndex = s.TransactionPosition.Value;
                    if (mevBlock.ExistingMEV[txIndex] == null)
                    {
                        var swapsTx = new MEVSwapsTx(txIndex);
                        mevBlock.SwapsTx.Add(swapsTx);
                        mevBlock.ExistingMEV[txIndex] = swapsTx;
                    }
                    mevBlock.ExistingMEV[txIndex].AddSwap(MEVHelper.BuildMEVSwap(mevBlock, s, zmSwap));
                }
            }

            foreach (var mb in _mevBlocks.Values)
                TestMev(mb);
        }

        private void ProcessLiquidations(long? fromBlockNumber, long toBlockNumber, ref MEVBlock mevBlock, ref List<DateTime>? arrivals, ref BitArray? txStatus)
        {
            foreach (var l in Liquidations)
            {
                if ((fromBlockNumber.HasValue && l.BlockNumber < fromBlockNumber) || l.BlockNumber > toBlockNumber)
                    continue;

                if (!GetMEVBlock(l.BlockNumber, ref mevBlock, ref arrivals, ref txStatus, false))
                    continue;

                var protocol = MEVHelper.GetProtocolLiquidation(l.Protocol);
                var debtSymbolIndex = MEVHelper.GetSymbolIndex(mevBlock, l.DebtTokenAddress);
                var debtPurchaseAmountUsd = FlashbotsSummaryTokens.Contains(l.DebtTokenAddress) ? XRates.ConvertToUsd(l.DebtTokenAddress, l.DebtPurchaseAmount) : null;
                var receivedSymbolIndex = MEVHelper.GetSymbolIndex(mevBlock, l.ReceivedTokenAddress);
                var receivedAmountUsd = l.ReceivedTokenAddress != null && FlashbotsSummaryTokens.Contains(l.ReceivedTokenAddress) ? XRates.ConvertToUsd(l.ReceivedTokenAddress, l.ReceivedAmount) : null;
                bool? isReverted = l.Error == "Reverted" ? true : null;

                var mevLiquidation = new MEVLiquidation(l.TransactionHash, protocol, (ZMDecimal)l.DebtPurchaseAmount, debtPurchaseAmountUsd, debtSymbolIndex, (ZMDecimal)l.ReceivedAmount, receivedAmountUsd, receivedSymbolIndex, isReverted);
                mevBlock.Liquidations.Add(mevLiquidation);
            }
        }

        private void ProcessNfts(long? fromBlockNumber, long toBlockNumber, ref MEVBlock mevBlock, ref List<DateTime>? arrivals, ref BitArray? txStatus)
        {
            foreach (var n in NftTrades)
            {
                if ((fromBlockNumber.HasValue && n.BlockNumber < fromBlockNumber) || n.BlockNumber > toBlockNumber)
                    continue;

                if (!GetMEVBlock(n.BlockNumber, ref mevBlock, ref arrivals, ref txStatus, false))
                    continue;

                ProtocolNFT protocol = MEVHelper.GetProtocolNFT(n.Protocol);
                int paymentSymbolIndex;
                decimal? paymentAmountUsd;
                ZMDecimal? paymentAmount;
                if (n.PaymentTokenAddress == "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee") // mev-inspect seems to use this burn address to signify ETH in nft trades
                {
                    paymentSymbolIndex = Symbol.EthSymbolIndex;
                    paymentAmount = (ZMDecimal)n.PaymentAmount / Tokens.Pow18;
                    paymentAmountUsd = (XRates.ETHBaseRate != null) ? (paymentAmount.Value * XRates.ETHBaseRate.Value).ToUsd() : null;
                }
                else
                {
                    paymentSymbolIndex = MEVHelper.GetSymbolIndex(mevBlock, n.PaymentTokenAddress);
                    paymentAmountUsd = MEVHelper.GetUsdAmount(n.PaymentTokenAddress, n.PaymentAmount, out paymentAmount);
                }
                MEVError error = MEVHelper.GetErrorFrom(n.Error);
                var mevNft = new MEVNFT(n.TransactionPosition, protocol, paymentSymbolIndex, n.CollectionAddress, n.TokenId.ToString(), paymentAmount, paymentAmountUsd, error);
                if (MEVHelper.DoAddMEV(mevBlock, mevNft))
                    mevBlock.NFTrades.Add(mevNft);
            }
        }

        public async Task Save(long trimBefore = -1)
        {
            var mevBlocks = _mevBlocks.Values.Where<MEVBlock>(x => (x.BlockNumber > trimBefore)).ToList<MEVBlock>();
            if (mevBlocks != null && mevBlocks.Count > 0)
                await DB.QueueWriteMevBlocksAsync(mevBlocks);
        }

        public void TestMev(MEVBlock mb)
        {
            if (mb == null) return;
            IMEV[] mevs = new IMEV[5000];

            for (int i = 0; i < mb.SwapsTx.Count; i++) TestMev(mb.SwapsTx[i], mb, i, mevs);
            for (int i = 0; i < mb.Arbs.Count; i++) TestMev(mb.Arbs[i], mb, i, mevs);
            for (int i = 0; i < mb.Liquidations.Count; i++) TestMev(mb.Liquidations[i], mb, i, mevs);
            for (int i = 0; i < mb.NFTrades.Count; i++) TestMev(mb.NFTrades[i], mb, i, mevs);
            for (int i = 0; i < mb.Sandwiched.Count; i++)
                foreach (var s in mb.Sandwiched[i])
                    TestMev(s, mb, i, mevs);
            for (int i = 0; i < mb.Backruns.Count; i++) TestMev(mb.Backruns[i], mb, i, mevs);
            for (int i = 0; i < mb.Frontruns.Count; i++) TestMev(mb.Frontruns[i], mb, i, mevs);
        }

        private void TestMev(IMEV mev, MEVBlock mb, int mevIndex, IMEV[] mevs)
        {
            // calculate members
            mev.Cache(mb, mevIndex);

            // allocate to transactions by index where possible
            if (mev.TxIndex != null)
            {
                if (mevs[mev.TxIndex.Value] != null)
                    Debug.WriteLine($"{mevs[mev.TxIndex.Value]} {mev.TxIndex.Value} overwritten by {mev} {mev.TxIndex.Value} blocknumber {mb.BlockNumber}");
                mevs[mev.TxIndex.Value] = mev;
                return;
            }
        }

        public void DebugSwaps(long blockNumber)
        {
            Tokens.Load();
            List<DateTime>? arrivals = null;
            BitArray txStatus = null;
            DEXs dexs = new DEXs();
            MEVBlock mevBlock = null;

            foreach (var s in Swaps)
            {
                // detect new block number
                if (mevBlock == null || s.BlockNumber != mevBlock.BlockNumber)
                {
                    if (!GetMEVBlock(s.BlockNumber, ref mevBlock, ref arrivals, ref txStatus, false))
                        continue;
                }

                if (s.TransactionPosition == null || s.Error != null) continue;
                var zmSwap = dexs.Add(s, arrivals[s.TransactionPosition.Value], out var pair);
#if (DEBUG)
                if (s.BlockNumber == blockNumber)
                {
                    string sKey = MEVHelper.TxKey(s);
                    Debug.WriteLine($"{s.TransactionPosition} {sKey} {zmSwap.SymbolA}:{zmSwap.SymbolB} amounts {zmSwap.AmountA} {zmSwap.AmountB} contract {s.ContractAddress} from {s.FromAddress} to {s.FromAddress}");
                }
#endif
            }
        }

        private void UserLoss(ZMSwap zmSwap, Pair pair)
        {
            // TODO under development
            ZMSwap zmSwapPrevByTime;
            if (zmSwap != null && pair != null && zmSwap.ImpactDelta != null && pair.PreviousZmSwap != null && zmSwap.BRateUsd != null && pair.PreviousZmSwap.ImpactDelta != null)
            {
                if (pair.TimeOrder.TryPredecessor(zmSwap.Order, out var prevEntry))
                {
                    // if the prev swap is of the same sign, it acts against the current swap
                    // if the prev swap is of a different sign, it benefits the current swap
                    // user loss is expressed as positive when good for the current swap, and negative when bad (frontrun)
                    zmSwapPrevByTime = prevEntry.Value;
                    if (zmSwapPrevByTime.ImpactDelta != null)
                    {
                        ZMDecimal? userLoss = -(pair.PreviousZmSwap.ImpactDelta - zmSwapPrevByTime.ImpactDelta);
                        ZMDecimal? userLossUsd = userLoss * zmSwap.BRateUsd;
                        if (zmSwap.ImpactDelta < 0) userLoss = -userLoss; // switch sells to give a consistent user loss
                        if (pair.ToString() == "WETH:UsdC")
                            Debug.WriteLine($"{pair} {zmSwap.Order} user loss ${userLossUsd.Value.RoundAwayFromZero(4)} {userLoss} prev block {pair.PreviousZmSwap.ImpactDelta} prev time {zmSwapPrevByTime.ImpactDelta} current {zmSwap.ImpactDelta} {zmSwap.IsSell}");
                    }
                }
            }
        }

        private bool GetMEVBlock(long blockNumber, ref MEVBlock? mevBlock, ref List<DateTime>? arrivals, ref BitArray? txStatus, bool doSetEthUsd = true)
        {
            // only get new references if we have changed block number (iterate sorted by block number to minimize this)
            if (mevBlock == null || blockNumber != mevBlock.BlockNumber)
            {
                // get or create mev block
                if (!_mevBlocks.TryGetValue(blockNumber, out mevBlock))
                {
                    mevBlock = new MEVBlock(blockNumber);
                    _mevBlocks.Add(blockNumber, mevBlock);
                }

                arrivals = null;
                ZmBlock? zmb = null;
                zmb = ZmBlocks.FirstOrDefault<ZmBlock>(x => x.BlockNumber == blockNumber);

                if (zmb == null)
                {
                    // attempt to get and write arrivals on the fly
                    txStatus = APIEnhanced.GetBlockTransactionStatus(_http, blockNumber.ToString()).Result;
                    if (txStatus != null)
                    {
                        // filter out invalid tx length extractor rows and calculate arrival times
                        var zb = DB.GetZMBlock(blockNumber);
                        ZMView? zv = null;
                        if (zb != null)
                        {
                            zv = new ZMView(blockNumber);
                            zv.RefreshOffline(zb, txStatus.Count);
                        }

                        if (blockNumber >= API.EarliestZMBlock)
                        {
                            // if we don’t have enough zm blocks to process by now, wait up until the longer PollTimeoutSecs (it will likely mean the zeromevdb is down or something)
                            if (zb != null && zv != null && zb.UniquePoPCount() >= 2)
                            {
                                using (var db = new zeromevContext())
                                {
                                    if (zb != null && zv != null && zv.PoPs != null && zv.PoPs.Count != 0)
                                    {
                                        // write the count to the db (useful for later bulk reprocessing/restarts)
                                        var txDataComp = Binary.Compress(Binary.WriteFirstSeenTxData(zv));
                                        zmb = db.AddZmBlock(blockNumber, txStatus.Count, zv.BlockTimeAvg, txDataComp, txStatus).Result;
                                        ZmBlocks.Add(zmb);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // if this block is before the earliest zm block, write txStatus data without arrival times
                            using (var db = new zeromevContext())
                            {
                                zmb = db.AddZmBlock(blockNumber, txStatus.Count, null, null, txStatus).Result;
                                ZmBlocks.Add(zmb);
                            }
                        }
                    }
                }

                if (zmb != null)
                {
                    byte[]? txData = null;
                    zmb.TxData = null;
                    if (zmb.TxData != null)
                    {
                        txData = Binary.Decompress(zmb.TxData);
                        arrivals = Binary.ReadFirstSeenTxData(txData);
                        Debug.Assert(arrivals.Count == zmb.TransactionCount);
                    }
                    txStatus = zmb.TxStatus;
                    if (mevBlock.ExistingMEV == null)
                        mevBlock.ExistingMEV = new IMEV[zmb.TransactionCount];
                    mevBlock.BlockTime = zmb.BlockTime;
                }
                else
                {
                    // don't fail just because we can't get or update txcount / arrival times at this point, just return null arrivals
                    // a fatal error will be an unexpected exception caused by a network error or a server going down
                    arrivals = null;
                    txStatus = null;
                    if (mevBlock.ExistingMEV == null)
                        mevBlock.ExistingMEV = new IMEV[10000]; // big enough to handle any block
                }
            }

            if (doSetEthUsd)
            {
                var ethUsd = XRates.ETHBaseRate;
                if (ethUsd != null)
                    mevBlock.EthUsd = (decimal?)ethUsd.Value.ToUsd();
            }

            return true;
        }
    }

    public class XRate
    {
        public ZMDecimal Rate;

        public XRate()
        {
        }

        public XRate(ZMDecimal rate)
        {
            Rate = rate;
        }
    }

    // not threadsafe
    public static class XRates
    {
        public static ZMDecimal MaxRateMove = 1.5;
        public static ZMDecimal MaxUsdRate = 100000;

        public static ZMDecimal? ETHBaseRate { get; set; }
        private static Dictionary<string, XRate> _usdBaseRate = new Dictionary<string, XRate>();
        public static Dictionary<string, BaseCurrency> _baseTokens;

        static XRates()
        {
            _baseTokens = new Dictionary<string, BaseCurrency>
                {
                    // USD
                    {"0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48" , BaseCurrency.USD },  // USDC
                    {"0x6b175474e89094c44da98b954eedeac495271d0f" , BaseCurrency.USD },  // DAI
                    {"0xdac17f958d2ee523a2206206994597c13d831ec7" , BaseCurrency.USD },  // TUSD
                    {"0x4fabb145d64652a948d72533023f6e7a623c7c53" , BaseCurrency.USD },  // BUSD
                    {"0x0000000000085d4780B73119b644AE5ecd22b376" , BaseCurrency.USD },  // TrueUSD
                    {"0x056fd409e1d7a124bd7017459dfea2f387b6d5cd" , BaseCurrency.USD },  // GUSD

                    // ETH
                    {"0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2" , BaseCurrency.ETH }   // wETH
                };

            foreach (var t in _baseTokens)
            {
                if (t.Value == BaseCurrency.USD)
                    _usdBaseRate.Add(t.Key, new XRate(1));
            }
        }

        public static bool GetCurrency(string token, out BaseCurrency? currency)
        {
            BaseCurrency c;
            if (!_baseTokens.TryGetValue(token, out c))
            {
                currency = null;
                return false;
            }
            currency = c;
            return true;
        }

        public static ZMDecimal? GetUsdRate(string token)
        {
            _usdBaseRate.TryGetValue(token, out var rate);
            if (rate == null || rate.Rate > XRates.MaxUsdRate)
                return null;
            return rate.Rate;
        }

        public static void SetUsdBaseRate(string token, ZMDecimal? newRate, bool IsSell, bool doLimitRateChange)
        {
            if (newRate == null || newRate == 0)
                return;

            XRate rate;
            if (!_usdBaseRate.TryGetValue(token, out rate))
            {
                // it is easy to lose money converting your naff-coin into ETH, and is therefore too risky when establishing an initial rate
                // getting ETH for your naff-coin is a higher bar, and is trusted for establishing an initial xrate
                if (IsSell)
                    return;

                rate = new XRate();
                _usdBaseRate.Add(token, rate);
            }
            if (rate.Rate == 0 || !doLimitRateChange || (newRate / rate.Rate < XRates.MaxRateMove && rate.Rate / newRate < XRates.MaxRateMove))
                rate.Rate = newRate.Value;
        }

        public static decimal? ConvertToUsd(string? token, ZMDecimal? amount)
        {
            if (token == null || amount == null)
                return null;

            // get xrate
            _usdBaseRate.TryGetValue(token, out var rate);
            if (rate == null)
                return null;

            // get token divisor
            var t = Tokens.GetFromAddress(token);
            if (t == null || t.Divisor == null)
                return null;

            amount = (amount / t.Divisor) * rate.Rate;
            decimal? usd = amount.Value.ToUsd();
            return usd;
        }
    }

    // not threadsafe
    public static class Tokens
    {
        public const string Unknown = "???";
        public static readonly BigInteger Pow18 = new BigInteger(1000000000000000000);
        private static Dictionary<string, ZmToken> _tokens = null;

        public static void Load()
        {
            if (_tokens != null)
                return;

            Dictionary<string, ZmToken> newTokens = new Dictionary<string, ZmToken>();
            using (var db = new zeromevContext())
            {
                var tokens = (from t in db.ZmTokens
                              where (t.Symbol != null && t.Symbol != Unknown)
                              select t).ToList();

                foreach (var t in tokens)
                    newTokens.Add(t.Address, t);
            }
            _tokens = newTokens;
        }

        public static void Add(List<ZmToken> addTokens)
        {
            foreach (var t in addTokens)
                _tokens.TryAdd(t.Address, t);
        }

        public static bool Add(ZmToken addToken)
        {
            return _tokens.TryAdd(addToken.Address, addToken);
        }

        public static ZmToken? GetFromAddress(string tokenAddress)
        {
            if (tokenAddress == null) return null;
            if (_tokens.TryGetValue(tokenAddress, out ZmToken? token))
                return token;
            return null;
        }

        public static string GetPairName(string tokenA, string tokenB)
        {
            string a = GetSymbol(tokenA);
            string b = GetSymbol(tokenB);
            return a + ":" + b;
        }

        public static string GetSymbol(string token)
        {
            var t = Tokens.GetFromAddress(token);
            if (t == null || t.Symbol == null || t.Symbol == Unknown) return token;
            return t.Symbol;
        }

        public static string GetSymbol(string token, out ZMDecimal? divisor)
        {
            divisor = null;
            var t = Tokens.GetFromAddress(token);
            if (t == null || t.Symbol == null || t.Symbol == Unknown) return token;
            divisor = t.Divisor;
            return t.Symbol;
        }
    }

    // a root object of a data structure for handling swaps
    public class DEXs : Dictionary<string, DEX>
    {
        public ZMSwap? Add(Swap swap, DateTime? arrivalTime, out Pair? pair)
        {
            pair = null;
            if (swap == null) return null;

            // build dex key
            string? key = DEX.BuildKey(swap);
            if (key == null) return null;

            // add new or get existing
            DEX dex;
            if (!this.TryGetValue(key, out dex))
            {
                dex = new DEX(swap.AbiName, swap.Protocol);
                this.Add(key, dex);
            }

            // add the swap to the dex
            return dex.Add(swap, arrivalTime, out pair);
        }
    }

    public class DEX : Dictionary<string, Pair>
    {
        public string? AbiName { get; private set; }
        public string? Protocol { get; private set; }

        public DEX(string? abiName, string? protocol)
        {
            AbiName = abiName;
            Protocol = protocol;
        }

        public ZMSwap Add(Swap swap, DateTime? arrivalTime, out Pair pair)
        {
            XRates.GetCurrency(swap.TokenInAddress, out var currencyIn);
            XRates.GetCurrency(swap.TokenOutAddress, out var currencyOut);

            bool isSell;
            pair = GetOrAddPair(swap.TokenInAddress, swap.TokenOutAddress, currencyIn, currencyOut, out isSell);
            BlockOrder blockOrder = new BlockOrder((long)swap.BlockNumber, (int)swap.TransactionPosition.Value, swap.TraceAddress); // if transaction position is ever null, we want it to raise an exception
            return pair.Add(swap, blockOrder, arrivalTime, isSell);
        }

        private Pair GetOrAddPair(string tokenIn, string tokenOut, BaseCurrency? currencyIn, BaseCurrency? currencyOut, out bool IsSell)
        {
            // sort in/out tokens to get a single pair containing both buys and sells
            string tokenA, tokenB;
            BaseCurrency? currencyA, currencyB;
            Pair.ConformPair(tokenIn, tokenOut, currencyIn, currencyOut, out tokenA, out tokenB, out currencyA, out currencyB, out IsSell);

            // create and add if required, then return
            Pair? pair;
            string key = tokenA + tokenB;
            if (!this.TryGetValue(key, out pair))
            {
                bool doSetXRates = false;
                if (AbiName == "UniswapV2Pair" || AbiName == "UniswapV3Pool") // only use uniswap for exchange rates for consistency
                    doSetXRates = true;
                pair = new Pair(this, key, tokenA, tokenB, currencyA, currencyB, doSetXRates);
                this.Add(key, pair);
            }
            return pair;
        }

        public static string? BuildKey(Swap swap)
        {
            if (swap == null)
                return null;

            return (swap.AbiName ?? "") + (swap.Protocol ?? "");
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    public enum BaseCurrency
    {
        USD,
        ETH
    }

    public class Pair
    {
        public string Key { get; private set; }
        public DEX Parent { get; private set; }
        public string TokenA { get; private set; }
        public string TokenB { get; private set; }
        public BaseCurrency? BaseCurrencyA { get; private set; }
        public BaseCurrency? BaseCurrencyB { get; private set; }
        public bool DoSetXRates { get; private set; }

        // we always progress by block time, so don't need a list
        public ZMSwap? CurrentZmSwap = null;
        public ZMSwap? PreviousZmSwap = null;

        public Swap CurrentSwap = null;
        public Swap PreviousSwap = null;

        // we need to hop around in arrival time order, so need a sorted collection
        public readonly TreeDictionary<Order, ZMSwap> TimeOrder = new TreeDictionary<Order, ZMSwap>();

        public Pair(DEX parent, string key, string tokenA, string tokenB, BaseCurrency? currencyA, BaseCurrency? currencyB, bool doSetXRates)
        {
            Parent = parent;
            Key = key;
            TokenA = tokenA;
            TokenB = tokenB;
            BaseCurrencyA = currencyA;
            BaseCurrencyB = currencyB;
            DoSetXRates = doSetXRates;
        }

        // add must be called in block order without gaps for accurate mev classification 
        public ZMSwap Add(Swap swap, BlockOrder blockOrder, DateTime? arrivalTime, bool isSell)
        {
            // get symbol and decimals for each token
            ZMDecimal? inDivisor, outDivisor;
            string symbolIn = Tokens.GetSymbol(swap.TokenInAddress, out inDivisor);
            string symbolOut = Tokens.GetSymbol(swap.TokenOutAddress, out outDivisor);

            // apply the decimal divisor
            ZMDecimal? tokenIn = (inDivisor != null) ? swap.TokenInAmount / inDivisor : null;
            ZMDecimal? tokenOut = (outDivisor != null) ? swap.TokenOutAmount / outDivisor : null;

            // create a smaller footprint zeromev swap which includes our timing data
            ZMSwap zmSwap = new ZMSwap(blockOrder, arrivalTime, isSell, tokenIn, tokenOut, symbolIn, symbolOut);

            // update block order (represented as current and previous swaps)
            PreviousZmSwap = CurrentZmSwap;
            PreviousSwap = CurrentSwap;
            CurrentZmSwap = zmSwap;
            CurrentSwap = swap;
            if (PreviousZmSwap != null)
                CurrentZmSwap.ImpactDelta = CurrentZmSwap.ExchangeRate() - PreviousZmSwap.ExchangeRate();

            // update time order (a sorted collection)
            //TimeOrder.Add(zmSwap.Order, zmSwap);

            // some dexs are responsible for setting base rates
            if (DoSetXRates)
            {
                var newRate = zmSwap.ExchangeRate();
                if (newRate != null && newRate != 0)
                {
                    /*
#if (DEBUG)
                    if (TokenA == "0xc18360217d8f7ab5e7c516566761ea12ce7f9d72")
                    {
                        Console.Write("");
                    }
#endif
                    */
                    if (BaseCurrencyA == BaseCurrency.ETH && BaseCurrencyB == BaseCurrency.USD)
                    {
                        if (XRates.ETHBaseRate == null || XRates.ETHBaseRate == 0 || (newRate / XRates.ETHBaseRate < XRates.MaxRateMove && XRates.ETHBaseRate / newRate < 1.5))
                        {
                            XRates.ETHBaseRate = newRate;
                            XRates.SetUsdBaseRate(TokenA, newRate, zmSwap.IsSell, true);
                        }
                    }
                    else if (BaseCurrencyA == null && BaseCurrencyB == BaseCurrency.USD)
                    {
                        XRates.SetUsdBaseRate(TokenA, newRate, zmSwap.IsSell, zmSwap.IsSell); // only set the initial rate on a buy, and limit rate changes when we are selling
                    }
                    else if (BaseCurrencyA == null && BaseCurrencyB == BaseCurrency.ETH)
                    {
                        if (XRates.ETHBaseRate.HasValue)
                        {
                            ZMDecimal? usd = null;
                            if (newRate != null && XRates.ETHBaseRate.HasValue)
                                usd = newRate * XRates.ETHBaseRate.Value;
                            XRates.SetUsdBaseRate(TokenA, usd, zmSwap.IsSell, zmSwap.IsSell);
                        }
                    }
                    /*
#if (DEBUG)
                    if (TokenA == "0xc18360217d8f7ab5e7c516566761ea12ce7f9d72")
                    {
                        Console.Write($"{newRate} {XRates.GetUsdRate(TokenA).Value}");
                    }
#endif
                    */
                }
            }

            // set the latest dollar rates for both sides of the swap
            zmSwap.ARateUsd = XRates.GetUsdRate(TokenA);
            zmSwap.BRateUsd = XRates.GetUsdRate(TokenB);

            return zmSwap;
        }

        // sort in/out tokens to get a single pair which can contain both buys and sells
        // known currency base pairs are placed as tokenB in a predefined priority (eg: ETH:LINK -> LINK:ETH, USDC:ETH -> ETH:USDC, USDC:DAI -> DAI:USDC)
        public static void ConformPair(string tokenIn, string tokenOut, BaseCurrency? currencyIn, BaseCurrency? currencyOut, out string tokenA, out string tokenB, out BaseCurrency? currencyA, out BaseCurrency? currencyB, out bool IsSell)
        {
            // decide symbol priority in pair
            IsSell = false;
            if (currencyIn == currencyOut) // WETH:WETH, USDC:USDC, other:other
            {
                IsSell = (string.Compare(tokenIn, tokenOut) > 0); // alphanumeric order
            }
            else if (currencyIn != null && currencyOut == null) // WETH:other, USDC:other
            {
                IsSell = true;
            }
            else if (currencyIn == BaseCurrency.USD && currencyOut == BaseCurrency.ETH) // USDC:WETH
            {
                IsSell = true;
            }

            if (!IsSell)
            {
                tokenA = tokenIn;
                tokenB = tokenOut;
                currencyA = currencyIn;
                currencyB = currencyOut;
                IsSell = false;
            }
            else
            {
                tokenA = tokenOut;
                tokenB = tokenIn;
                currencyA = currencyOut;
                currencyB = currencyIn;
                IsSell = true;
            }
        }

        public override string ToString()
        {
            return Tokens.GetPairName(TokenA, TokenB);
        }
    }

    public class Order : IComparable<Order>
    {
        public DateTime? TimeOrder { get; set; }
        public BlockOrder BlockOrder;

        int IComparable<Order>.CompareTo(Order? other)
        {
            // take nulls to mean earlier values, as they are likely from before we have arrival time data
            if (other == null)
                return 1;

            if (this.TimeOrder != null && other.TimeOrder != null)
            {
                int r = this.TimeOrder.Value.CompareTo(other.TimeOrder.Value);
                if (r != 0) return r;
            }
            else if (this.TimeOrder == null && other.TimeOrder == null)
            {
                return 0;
            }
            else if (other.TimeOrder == null)
            {
                return 1;
            }
            else if (this.TimeOrder == null)
            {
                return -1;
            }

            return this.BlockOrder.Compare(other.BlockOrder);
        }

        public override string ToString()
        {
            return TimeOrder.ToString() + " " + BlockOrder.ToString();
        }
    }

    public class BlockOrder : IComparable<BlockOrder>
    {
        public long Blocknum;
        public int TxIndex;
        public TraceAddress TraceAddress;

        public BlockOrder(long blocknum, int txIndex, int[] traceAddress)
        {
            Blocknum = blocknum;
            TxIndex = txIndex;
            TraceAddress = new TraceAddress(traceAddress);
        }

        public int Compare(BlockOrder? other)
        {
            if (other == null) return -1;

            // compare by block number
            int r;
            r = this.Blocknum.CompareTo(other.Blocknum);
            if (r != 0) return r;

            // then by transaction index
            r = this.TxIndex.CompareTo(other.TxIndex);
            if (r != 0) return r;

            // and finally by trace address
            return this.TraceAddress.CompareTo(other.TraceAddress);
        }

        int IComparable<BlockOrder>.CompareTo(BlockOrder? other)
        {
            return Compare(other);
        }

        public override string ToString()
        {
            return $"{Blocknum} {TxIndex} [{string.Join(",", TraceAddress)}]";
        }
    }

    public class ZMSwap
    {
        // holds both time and block orderings
        public Order Order;

        // buy or sell
        public bool IsSell;

        // token symbols are smaller and easier to read than addresses (but they are not guaranteed to be unique, and should not be used as keys)
        public string SymbolA;
        public string SymbolB;

        // set if this swap is counted as contributing to another mev type (eg: arb or sandwich - if so, its MEV amount belongs to the parent)
        public MEVType ParentType;
        public BlockOrder? Parent;

        public ZMDecimal? ImpactDelta;

        // amounts are already adjusted by the token divisor, so rate calculations are reliable
        // ensure in/outs are can be zero if token/divisors are unknown
        public ZMDecimal? AmountA;
        public ZMDecimal? AmountB;
        public ZMDecimal? MEVAmount; // 0 = neutral, >0 = positive <0 = negative, null = unclassified

        // determined directly from a USD stable-coin, or via ETH to a USD stable-coin after the swap. Determined within block order, so does not change. Set to 1 if it is already in USD, and null if unknown
        public ZMDecimal? ARateUsd;
        public ZMDecimal? BRateUsd;

        public ZMSwap(BlockOrder blockOrder, DateTime? arrivalTime, bool isSell, ZMDecimal? amountIn, ZMDecimal? amountOut, string symbolIn, string symbolOut)
        {
            Order = new Order();
            Order.TimeOrder = arrivalTime;
            Order.BlockOrder = blockOrder;
            IsSell = isSell;

            if (!isSell)
            {
                AmountA = amountIn;
                AmountB = amountOut;
                SymbolA = symbolIn;
                SymbolB = symbolOut;
            }
            else
            {
                AmountA = amountOut;
                AmountB = amountIn;
                SymbolA = symbolOut;
                SymbolB = symbolIn;
            }
        }

        public ZMDecimal? ExchangeRate()
        {
            if (AmountA == null || AmountB == null) return null;
            return AmountB / AmountA;
        }

        public ZMDecimal? InverseExchangeRate()
        {
            if (AmountA == null || AmountB == null) return null;
            return AmountA / AmountB;
        }

        public ZMDecimal? InAmount
        {
            get
            {
                return IsSell ? AmountB : AmountA;
            }
        }

        public ZMDecimal? OutAmount
        {
            get
            {
                return IsSell ? AmountA : AmountB;
            }
        }

        public ZMDecimal? InRateUsd
        {
            get
            {
                return IsSell ? BRateUsd : ARateUsd;
            }
        }

        public ZMDecimal? OutRateUsd
        {
            get
            {
                return IsSell ? ARateUsd : BRateUsd;
            }
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, ZMSerializeOptions.Default);
        }
    }
}