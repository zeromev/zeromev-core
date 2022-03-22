using C5;
using Microsoft.EntityFrameworkCore;
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
        public static void SimUniswap2(int count, double isBaAbove, bool isABBA, ZMDecimal c, ZMDecimal x, ZMDecimal y, out ZMDecimal[] a, out ZMDecimal[] b, out ZMDecimal[] xOut, out ZMDecimal[] yOut, out bool[] isBA)
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
                // force the first two txs to be either ABAB or ABBA (based on isABBA), and the rest trade random directions (based on isBaAbove)
                if (i == 0)
                    isBA[0] = false;
                else if (i == 1)
                    isBA[1] = isABBA ? true : false;
                else
                    isBA[i] = (r.NextDouble() > isBaAbove);

                if (!isBA[i])
                {
                    // a to b
                    a[i] = x * ((ZMDecimal)(amountFrac * r.NextDouble())); // input amount as a random % of pool (to stop the pool going to zero)
                    a[i] = 100;
                    b[i] = MEVCalc.SwapOutputAmount(ref x, ref y, c, a[i]);
                }
                else
                {
                    // b to a
                    b[i] = y * ((ZMDecimal)(amountFrac * r.NextDouble())); // input amount as a random % of pool (to stop the pool going to zero)
                    b[i] = 0.1;
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

            SimUniswap2(100, 0.5, isABBA, c, 10000, 10, out var real_a, out var real_b, out var xOut, out var yOut, out var isBA);

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
                back.Swap.Protocol != protocol ||
                sandwiched[0].Swap.Protocol != protocol))
                return false;

            return success;
        }

        public static ZMDecimal SandwichProfitDireV2(ZMSwap front, ZMSwap back, ZMDecimal front_in_decimal)
        {
            var front_in_amount = front.InAmount;
            var front_out_amount = front.OutAmount;
            var back_in_amount = back.InAmount;
            var back_out_amount = back.OutAmount;

            var in_price = front_in_amount / front_out_amount;
            if (front_out_amount > front_in_amount)
                in_price = front_out_amount / front_in_amount;

            var out_price = back_in_amount / back_out_amount;
            bool out_price_per_out = false;
            if (back_out_amount > back_in_amount)
            {
                out_price = back_out_amount / back_in_amount;
                out_price_per_out = true;
            }

            ZMDecimal profit_decimal = 0;
            if (back_in_amount > front_out_amount)
            {
                if (out_price_per_out)
                    profit_decimal = front_out_amount * (out_price - in_price);
                else
                    profit_decimal = front_out_amount / out_price - front_out_amount / in_price;
            }

            if (back_in_amount < front_out_amount)
            {
                if (out_price_per_out)
                    profit_decimal = back_in_amount * (out_price - in_price);
                else
                    profit_decimal = back_in_amount / out_price - back_in_amount / in_price;
            }

            if (back_in_amount == front_out_amount)
                profit_decimal = back_out_amount - front_in_amount;

            return profit_decimal;
        }

        public static int GetSymbolIndex(MEVBlock? mb, string? tokenAddress)
        {
            if (mb == null || tokenAddress == null) return -1;
            int index = mb.Symbols.FindIndex(x => { return x.TokenAddress == tokenAddress; });
            if (index == -1)
            {
                var zmToken = Tokens.GetFromAddress(tokenAddress);
                if (zmToken == null)
                    return -1; // meaning unknown symbol

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

        public static void AddArbSwap(MEVArb arb, MEVBlock mb, Swap swap, ZMSwap zmSwap)
        {
            var arbSwap = BuildMEVSwap(mb, swap, zmSwap);
            arb.Swaps.Add(arbSwap);
        }

        public static void AddSwap(MEVBlock mevBlock, Swap s, ZMSwap zmSwap, ref MEVSwap lastSwap, ref MEVContractSwaps lastContractSwaps)
        {
            var newSwap = MEVHelper.BuildMEVSwap(mevBlock, s, zmSwap);
            if (lastSwap != null && lastSwap.TxIndex == newSwap.TxIndex)
            {
                if (lastContractSwaps != null)
                {
                    if (newSwap.TxIndex == lastContractSwaps.TxIndex)
                        lastContractSwaps.Swaps.Add(newSwap);
                    else
                        lastContractSwaps = null;
                }
                if (lastContractSwaps == null)
                {
                    var contractSwaps = new MEVContractSwaps(newSwap.TxIndex);
                    mevBlock.ContractSwaps.Add(contractSwaps);
                    mevBlock.Swaps.Remove(lastSwap); // remove the last swap
                    contractSwaps.Swaps.Add(lastSwap); // and reassign it to a contract swap containing more than one swap
                    contractSwaps.Swaps.Add(newSwap); // before adding the latest swap
                    lastContractSwaps = contractSwaps;
                }
                return;
            }
            mevBlock.Swaps.Add(newSwap);
            lastSwap = newSwap;
        }

        public static MEVSwap BuildMEVSwap(MEVBlock mb, Swap swap, ZMSwap zmSwap)
        {
            int symbolIn = GetSymbolIndex(mb, swap.TokenInAddress);
            int symbolOut = GetSymbolIndex(mb, swap.TokenOutAddress);
            var protocol = GetProtocolSwap(swap.Protocol, swap.AbiName);
            var mevSwap = new MEVSwap(swap.TransactionPosition.Value, protocol, symbolIn, symbolOut, zmSwap.InAmount, zmSwap.OutAmount, zmSwap.InRateUsd, zmSwap.OutRateUsd);
            return mevSwap;
        }

        public static bool DoAddMEV(MEVBlock mb, int? txIndex)
        {
            if (txIndex == null) return true;
            if (!mb.ExistingMEV[txIndex.Value])
            {
                mb.ExistingMEV[txIndex.Value] = true;
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
            if (amount == null)
            {
                newAmount = null;
                return null;
            }

            if (token == null)
            {
                newAmount = amount.Value / Tokens.Pow18;
                return null;
            }

            var zmToken = Tokens.GetFromAddress(token);
            var usdRate = XRates.GetUsdRate(token);
            if (zmToken != null)
                newAmount = amount.Value / zmToken.Divisor;
            else
                newAmount = amount.Value / Tokens.Pow18;
            if (usdRate != null)
                return (decimal)(newAmount.Value * usdRate).Value.ToUsd();
            return null;
        }

        public static void DebugMevBlocks(Dictionary<long, MEVBlock> mevBlocks)
        {
            foreach (var mb in mevBlocks.Values)
                DebugMevBlock(mb);
        }

        public static void DebugMevBlock(MEVBlock mb)
        {
            Debug.WriteLine(mb.BlockNumber);
            for (int i = 0; i < mb.Swaps.Count; i++) DebugMEV(mb.Swaps[i], mb, i);
            for (int i = 0; i < mb.ContractSwaps.Count; i++) DebugMEV(mb.ContractSwaps[i], mb, i);
            for (int i = 0; i < mb.Backruns.Count; i++) DebugMEV(mb.Backruns[i], mb, i);
            for (int i = 0; i < mb.Sandwiched.Count; i++)
                foreach (var s in mb.Sandwiched[i])
                    DebugMEV(s, mb, i);
            for (int i = 0; i < mb.Frontruns.Count; i++) DebugMEV(mb.Frontruns[i], mb, i);
            for (int i = 0; i < mb.Arbs.Count; i++) DebugMEV(mb.Arbs[i], mb, i);
            for (int i = 0; i < mb.Liquidations.Count; i++) DebugMEV(mb.Liquidations[i], mb, i);
            for (int i = 0; i < mb.NFTrades.Count; i++) DebugMEV(mb.NFTrades[i], mb, i);
            Debug.WriteLine("");

            var json = JsonSerializer.Serialize<MEVBlock>(mb, ZMSerializeOptions.Default);
            var compJson = Binary.Compress(Encoding.ASCII.GetBytes(json));
            Debug.WriteLine($"{mb.BlockNumber} {json.Length} bytes {compJson.Length} comp bytes {json}");
        }

        private static void DebugMEV(IMEV mev, MEVBlock mb, int mevIndex)
        {
            Debug.WriteLine($"{mev.MEVType} {mev.MEVClass}");
            mev.Cache(mb, mevIndex);
            Debug.WriteLine("summary: " + mev.ActionSummary);
            Debug.WriteLine(" detail: " + mev.ActionDetail);
            Debug.WriteLine("    mev: " + mev.MEVDetail);
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

        static HttpClient _http = new HttpClient();

        public BlockProcess(DEXs dexs)
        {
            _dexs = dexs;
        }

        public static BlockProcess Load(long fromBlockNumber, long toBlockNumber, DEXs dexs)
        {
            BlockProcess bi = new BlockProcess(dexs);

            // due to an issue with mev-inspect sandwich detection, sandwich table rows are re-calculated before processing rather than being loaded from the db
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
                                     select new ArbSwap(a, s)).ToDictionary(x => MEVHelper.TxKey(x.Swap.SwapTransactionHash, x.Swap.SwapTraceAddress), x => x);

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

            for (int i = restIndex; i < Swaps.Count; i++)
            {
                Swap otherSwap = Swaps[i];
                if (otherSwap.BlockNumber != frontSwap.BlockNumber)
                    return restIndex;

                if (otherSwap.TransactionHash == frontSwap.TransactionHash)
                    continue;

                if (otherSwap.ContractAddress == frontSwap.ContractAddress)
                {
                    if (otherSwap.TokenInAddress == frontSwap.TokenInAddress
                            && otherSwap.TokenOutAddress == frontSwap.TokenOutAddress
                            && otherSwap.FromAddress != sandwicher)
                    {
                        if (sandwichedSwaps == null)
                            sandwichedSwaps = new List<Swap>();
                        sandwichedSwaps.Add(otherSwap);
                    }
                    else if (otherSwap.TokenOutAddress == frontSwap.TokenInAddress
                                && otherSwap.TokenInAddress == frontSwap.TokenOutAddress
                                && otherSwap.FromAddress == sandwicher)
                    {
                        if (sandwichedSwaps != null)
                        {
                            SandwichesFrontrun.Add(MEVHelper.TxKey(frontSwap), frontSwap);
                            SandwichesBackrun.Add(MEVHelper.TxKey(otherSwap), otherSwap);
                            foreach (var ss in sandwichedSwaps)
                                SandwichedSwaps.Add(MEVHelper.TxKey(ss), ss);
                            return i + 1; // this skip past sandwiched transactions resolves the mev-inspect issue
                        }
                    }
                }
            }
            return restIndex;
        }

        public async void Run()
        {
            Tokens.Load();

            MEVFrontrun? frontrun = null;
            string? lastArbHash = null;
            MEVArb? lastArb = null;

            MEVBlock? mevBlock = null;
            List<DateTime>? arrivals = null;
            List<MEVSandwiched> sandwiched = new List<MEVSandwiched>();

            // swaps, sandwiches and arbs
            ZMSwap?[] zmSwaps = new ZMSwap[Swaps.Count];
            long? skippedBlockNumber = null;
            for (int i = 0; i < Swaps.Count; i++)
            {
                Swap s = Swaps[i];

                // consider swaps with zero amount invalid (they break exchange rate calculations)
                if (s.TokenInAmount == 0 || s.TokenOutAmount == 0)
                    continue;

                // detect new block number
                if (mevBlock == null || s.BlockNumber != mevBlock.BlockNumber)
                {
                    // process liquidations and nfts along with swaps to ensure we get decent exchange rates
                    // note that liquidations and nfts xrates are set a block granularity, where as arbs/swaps/sandwiches are set a tx level granularity
                    var tempMevBlock = mevBlock;
                    ProcessLiquidations(skippedBlockNumber, s.BlockNumber, ref mevBlock, ref arrivals);
                    ProcessNfts(skippedBlockNumber, s.BlockNumber, ref mevBlock, ref arrivals);
                    skippedBlockNumber = s.BlockNumber + 1;

                    if (frontrun != null)
                        Debug.WriteLine("new block on unfinished sandwich");

                    // reset on block boundaries
                    frontrun = null;
                    lastArbHash = null;
                    lastArb = null;
                    sandwiched.Clear();

                    mevBlock = tempMevBlock;
                    if (!GetMEVBlock(s.BlockNumber, ref mevBlock, ref arrivals))
                        continue;
                }

                if (s.TransactionPosition == null || s.Error != null) continue;
                DateTime? arrival = arrivals != null ? arrivals[s.TransactionPosition.Value] : null;
                var zmSwap = _dexs.Add(s, arrival, out var pair);
                zmSwaps[i] = zmSwap;

                bool isSandwichTx = false;
                string sKey = MEVHelper.TxKey(s);

                // sandwiches
                if (this.SandwichesFrontrun.TryGetValue(sKey, out var sandwichFrontrun))
                {
                    isSandwichTx = true;

                    if (frontrun != null)
                        Debug.WriteLine("sandwich reentry");

                    // sandwich frontrun
                    frontrun = new MEVFrontrun(s.TransactionPosition.Value, MEVHelper.BuildMEVSwap(mevBlock, s, zmSwap));
                }
                else if (this.SandwichesBackrun.TryGetValue(sKey, out var sandwichBackrun))
                {
                    isSandwichTx = true;

                    if (frontrun == null)
                        Debug.WriteLine("backrun swap when frontrun not set");

                    if (sandwiched.Count == 0)
                        Debug.WriteLine("backrun swap when sandwiched not set");

                    var backrun = new MEVBackrun(s.TransactionPosition.Value, MEVHelper.BuildMEVSwap(mevBlock, s, zmSwap));

                    // front/sandwiched and backrun instances can access each other using the same index across each collection
                    if (frontrun != null && sandwiched.Count > 0)
                    {
                        mevBlock.Frontruns.Add(frontrun);
                        mevBlock.Sandwiched.Add(sandwiched.ToArray());
                        mevBlock.Backruns.Add(backrun);
                        MEVHelper.DoAddMEV(mevBlock, frontrun.TxIndex);
                        foreach (var sw in sandwiched)
                            MEVHelper.DoAddMEV(mevBlock, sw.TxIndex);
                        MEVHelper.DoAddMEV(mevBlock, backrun.TxIndex);
                    }

                    frontrun = null;
                    sandwiched.Clear();
                }
                else if (this.SandwichedSwaps.TryGetValue(sKey, out var sandwichedSwap))
                {
                    isSandwichTx = true;

                    if (frontrun == null)
                        Debug.WriteLine("sandwich swap when frontrun not set");

                    // sandwiched
                    sandwiched.Add(new MEVSandwiched(s.TransactionPosition, MEVHelper.BuildMEVSwap(mevBlock, s, zmSwap)));
                }

                // arbs
                if (this.ArbitrageSwaps.TryGetValue(sKey, out var arb))
                {
                    if (isSandwichTx)
                    {
                        // if we were mid arb, remove it, reverting it's swaps to normal swaps
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
                        decimal? arbProfitUsd = -MEVHelper.GetUsdAmount(arb.Arb.ProfitTokenAddress, arb.Arb.ProfitAmount, out newAmount);
                        var mevArb = new MEVArb(s.TransactionPosition, MEVClass.Unclassified, arbProfitUsd);
                        lastArb = mevArb;
                        lastArbHash = arb.Swap.SwapTransactionHash;
                        MEVHelper.AddArbSwap(mevArb, mevBlock, s, zmSwap);
                        MEVHelper.DoAddMEV(mevBlock, mevArb.TxIndex);
                        mevBlock.Arbs.Add(mevArb);
                    }
                    else
                    {
                        // then add the remaining arb swaps
                        if (lastArb != null) // will be null if we're skipping due to a clash with sandwiches
                            MEVHelper.AddArbSwap(lastArb, mevBlock, s, zmSwap);
                    }
                }
            }

            // remaining liquidations and nfts
            mevBlock = null;
            ProcessLiquidations(skippedBlockNumber ?? 0, long.MaxValue, ref mevBlock, ref arrivals);
            mevBlock = null;
            ProcessNfts(skippedBlockNumber ?? 0, long.MaxValue, ref mevBlock, ref arrivals);

            // add any swaps that are not part of a sandwich or arb
            MEVSwap? lastSwap = null;
            MEVContractSwaps? lastContractSwaps = null;
            mevBlock = null;

            for (int i = 0; i < Swaps.Count; i++)
            {
                Swap s = Swaps[i];
                var zmSwap = zmSwaps[i];

                if (mevBlock == null || s.BlockNumber != mevBlock.BlockNumber)
                {
                    // reset on block boundaries
                    lastSwap = null;
                    lastContractSwaps = null;
                    if (!GetMEVBlock(s.BlockNumber, ref mevBlock, ref arrivals, false))
                        continue;
                }

                if (zmSwap != null && s.TransactionPosition != null && !mevBlock.ExistingMEV[s.TransactionPosition.Value])
                    MEVHelper.AddSwap(mevBlock, s, zmSwap, ref lastSwap, ref lastContractSwaps);
            }

            foreach (var mb in _mevBlocks.Values)
                TestMev(mb);
        }

        private void ProcessLiquidations(long? fromBlockNumber, long toBlockNumber, ref MEVBlock mevBlock, ref List<DateTime>? arrivals)
        {
            foreach (var l in Liquidations)
            {
                if ((fromBlockNumber.HasValue && l.BlockNumber < fromBlockNumber) || l.BlockNumber > toBlockNumber)
                    continue;

                if (!GetMEVBlock(l.BlockNumber, ref mevBlock, ref arrivals, false))
                    continue;

                var protocol = MEVHelper.GetProtocolLiquidation(l.Protocol);
                var debtSymbolIndex = MEVHelper.GetSymbolIndex(mevBlock, l.DebtTokenAddress);
                var debtPurchaseAmountUsd = XRates.ConvertToUsd(l.DebtTokenAddress, l.DebtPurchaseAmount);
                var receivedSymbolIndex = MEVHelper.GetSymbolIndex(mevBlock, l.ReceivedTokenAddress);
                var receivedAmountUsd = XRates.ConvertToUsd(l.ReceivedTokenAddress, l.ReceivedAmount);
                bool? isReverted = l.Error == "Reverted" ? true : null;

                var mevLiquidation = new MEVLiquidation(l.TransactionHash, protocol, l.DebtPurchaseAmount, debtPurchaseAmountUsd, debtSymbolIndex, l.ReceivedAmount, receivedAmountUsd, receivedSymbolIndex, isReverted);
                mevBlock.Liquidations.Add(mevLiquidation);
            }
        }

        private void ProcessNfts(long? fromBlockNumber, long toBlockNumber, ref MEVBlock mevBlock, ref List<DateTime>? arrivals)
        {
            foreach (var n in NftTrades)
            {
                if ((fromBlockNumber.HasValue && n.BlockNumber < fromBlockNumber) || n.BlockNumber > toBlockNumber)
                    continue;

                if (!GetMEVBlock(n.BlockNumber, ref mevBlock, ref arrivals, false))
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
                if (MEVHelper.DoAddMEV(mevBlock, mevNft.TxIndex))
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

            for (int i = 0; i < mb.Swaps.Count; i++) TestMev(mb.Swaps[i], mb, i, mevs);
            for (int i = 0; i < mb.ContractSwaps.Count; i++) TestMev(mb.ContractSwaps[i], mb, i, mevs);
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

        public void Test()
        {
            Tokens.Load();

            Swap frontSwap = null;
            ZMSwap? zmFrontrun = null;
            MEVFrontrun? frontrun = null;
            ZMDecimal? sandwichedOut = 0;
            int frontrunIndex = 0;
            string? lastArbHash = null;
            MEVArb? lastArb = null;
            MEVSwap? lastSwap = null;
            List<SwapRecord> revertableArbSwaps = new List<SwapRecord>();
            MEVContractSwaps? lastContractSwaps = null;

            MEVBlock? mevBlock = null;
            List<DateTime>? arrivals = null;
            List<MEVSandwiched> sandwiched = new List<MEVSandwiched>();

            ZMDecimal? sandwichNaive = 0;
            ZMDecimal? sandwichProfitBackrun = 0;
            ZMDecimal? sandwichProfitConservative = 0;
            ZMDecimal? sandwichProfitDire = 0;
            ZMDecimal? sandwichProfitDireV2 = 0;
            ZMDecimal? sandwichProfitFrontrun = 0;
            ZMDecimal? sandwichProfitSandwiched = 0;
            ZMDecimal? sandwichVictimImpact = 0;
            ZMDecimal? sandwichTotalUsd = 0;
            ZMDecimal? sandwichNaiveFiltered = 0;
            ZMDecimal? arbTotalUsd = 0;
            ZMDecimal? victimImpact = 0;

            // swaps, sandwiches and arbs
            foreach (Swap s in Swaps)
            {
                // detect new block number
                if (mevBlock == null || s.BlockNumber != mevBlock.BlockNumber)
                {
                    if (frontrun != null)
                        throw new Exception("new block on unfinished sandwich");

                    // reset on block boundaries
                    zmFrontrun = null;
                    frontSwap = null;
                    frontrun = null;
                    frontrunIndex = 0;
                    lastArbHash = null;
                    lastArb = null;
                    revertableArbSwaps.Clear();
                    lastSwap = null;
                    lastContractSwaps = null;
                    sandwiched.Clear();

                    if (!GetMEVBlock(s.BlockNumber, ref mevBlock, ref arrivals))
                        continue;
                }

                if (s.TransactionPosition == null || s.Error != null) continue;
                var zmSwap = _dexs.Add(s, arrivals[s.TransactionPosition.Value], out var pair);

                bool isSandwichTx = false, isArbTx = false;
                string sKey = MEVHelper.TxKey(s);

                // sandwiches
                if (this.SandwichesFrontrun.TryGetValue(sKey, out var sandwichFrontrun))
                {
                    isSandwichTx = true;

                    if (frontrun != null)
                        throw new Exception("sandwich reentry");

                    // sandwich frontrun
                    frontSwap = s;
                    frontrun = new MEVFrontrun(frontrunIndex, MEVHelper.BuildMEVSwap(mevBlock, s, zmSwap));
                    zmFrontrun = zmSwap;
                    sandwichedOut = 0;
                    victimImpact = 0;
                }
                else if (this.SandwichesBackrun.TryGetValue(sKey, out var sandwichBackrun))
                {
                    isSandwichTx = true;

                    if (frontrun == null)
                        throw new Exception("sandwich backrun when frontrun not set");

                    // sandwich backrun
                    ZMDecimal? frontIn = zmFrontrun.InAmount;
                    ZMDecimal? frontOut = zmFrontrun.OutAmount;
                    ZMDecimal? backIn = zmSwap.InAmount;
                    ZMDecimal? backOut = zmSwap.OutAmount;

                    // sandwiched profit
                    ZMDecimal? frontRate = frontIn / frontOut;
                    ZMDecimal? backRate = backOut / backIn; // reversed to make it comparable (this is enforced in sandwich detection, and so will always be valid)
                    ZMDecimal? rateDiff = backRate - frontRate; // gets us the rate difference

                    // dire profit
                    ZMDecimal? direProfitFrontrun = (frontOut * (backOut / backIn) - frontIn) * zmSwap.OutRateUsd;
                    ZMDecimal? direProfitBackrun = (backOut - (backIn * (frontIn / frontOut))) * zmSwap.OutRateUsd;
                    ZMDecimal? direProfitFrontrunAbs = direProfitFrontrun < 0 ? -direProfitFrontrun : direProfitFrontrun;
                    ZMDecimal? direProfitBackrunAbs = direProfitBackrun < 0 ? -direProfitBackrun : direProfitBackrun;
                    ZMDecimal? direProfitConservative = direProfitFrontrunAbs < direProfitBackrunAbs ? direProfitFrontrun : direProfitBackrun;
                    sandwichProfitDire += direProfitConservative;

                    // dire v2 profit
                    string symbolIn = Tokens.GetSymbol(frontSwap.TokenInAddress, out var inDivisor);
                    var direV2 = MEVHelper.SandwichProfitDireV2(zmFrontrun, zmSwap, inDivisor) * zmSwap.OutRateUsd;
                    sandwichProfitDireV2 += direV2;

                    // rate difference calculation
                    ZMDecimal? profitFrontrun = frontOut * rateDiff * zmSwap.OutRateUsd;
                    ZMDecimal? profitBackrun = backIn * rateDiff * zmSwap.OutRateUsd;
                    ZMDecimal? profitFrontrunAbs = profitFrontrun < 0 ? -profitFrontrun : profitFrontrun;
                    ZMDecimal? profitBackrunAbs = profitBackrun < 0 ? -profitBackrun : profitBackrun;
                    ZMDecimal? profitConservative = profitFrontrunAbs < profitBackrunAbs ? profitFrontrun : profitBackrun;

                    // sandwiched naive
                    ZMDecimal? naive = (backOut - frontIn) * zmSwap.OutRateUsd;
                    sandwichNaive += naive;

                    // flashbots filtering <=1% profits
                    double amount_percent_difference = Math.Abs((double)((frontOut / backIn)) - 1.0);
                    ZMDecimal? naiveFiltered = 0;
                    if (amount_percent_difference <= 0.01)
                    {
                        sandwichNaiveFiltered += naive;
                        naiveFiltered = naive;
                    }

                    sandwichProfitFrontrun += profitFrontrun;
                    sandwichProfitBackrun += profitBackrun;
                    sandwichProfitConservative += profitConservative;

                    double ratio = ((double)(frontIn / (backOut + frontIn)) - 0.5) * 2;
                    Debug.WriteLine($"k,{s.TokenInAmount * s.TokenOutAmount},backTx,{s.TransactionHash},ratio,{ratio},frontIn,{frontIn},backOut,{backOut},naive,{naive.Value.ToUsd()},direProfit,{direProfitFrontrun.Value.ToUsd()},direV2,{direV2.Value.ToUsd()},profitConservative,{profitConservative.Value.ToUsd()},profitFrontrun,{profitFrontrun.Value.ToUsd()},profitBackrun,{profitBackrun.Value.ToUsd()},victimLoss,{(victimImpact == null ? string.Empty : victimImpact.Value.ToUsd())}");

                    var backrun = new MEVBackrun(s.TransactionPosition.Value, MEVHelper.BuildMEVSwap(mevBlock, s, zmSwap));

                    // front/sandwiched and backrun instances can access each other using the same index across each collection
                    mevBlock.Frontruns.Add(frontrun);
                    mevBlock.Sandwiched.Add(sandwiched.ToArray());
                    mevBlock.Backruns.Add(backrun);

                    frontSwap = null;
                    frontrun = null;
                    zmFrontrun = null;
                    sandwiched.Clear();
                }
                else if (this.SandwichedSwaps.TryGetValue(sKey, out var sandwichedSwap))
                {
                    isSandwichTx = true;

                    if (frontrun == null)
                        throw new Exception("sandwich swap when frontrun not set");

                    // sandwiched
                    sandwiched.Add(new MEVSandwiched(s.TransactionPosition, MEVHelper.BuildMEVSwap(mevBlock, s, zmSwap)));
                    sandwichedOut += zmSwap.OutAmount;
                }

                // arbs
                if (this.ArbitrageSwaps.TryGetValue(sKey, out var arb))
                {
                    if (isSandwichTx)
                    {
                        // if we were mid arb, remove it, reverting it's swaps to normal swaps
                        if (arb.Swap.SwapTransactionHash == lastArbHash)
                        {
                            mevBlock.Arbs.Remove(lastArb);
                            foreach (var revertArbSwap in revertableArbSwaps)
                                MEVHelper.AddSwap(mevBlock, revertArbSwap.Swap, revertArbSwap.ZMSwap, ref lastSwap, ref lastContractSwaps);
                        }

                        // set to skip any further legs of this arb
                        lastArb = null;
                        revertableArbSwaps.Clear();
                        lastArbHash = arb.Swap.SwapTransactionHash;
                        isArbTx = false;
                    }
                    else if (arb.Swap.SwapTransactionHash != lastArbHash)
                    {
                        // create the mev arb on the first arb swap
                        ZMDecimal? newAmount;
                        decimal? arbProfitUsd = -MEVHelper.GetUsdAmount(arb.Arb.ProfitTokenAddress, arb.Arb.ProfitAmount, out newAmount);
                        var mevArb = new MEVArb(s.TransactionPosition, MEVClass.Unclassified, arbProfitUsd);
                        revertableArbSwaps.Clear();
                        lastArb = mevArb;
                        lastArbHash = arb.Swap.SwapTransactionHash;
                        MEVHelper.AddArbSwap(mevArb, mevBlock, s, zmSwap);
                        revertableArbSwaps.Add(new SwapRecord(s, zmSwap));
                        mevBlock.Arbs.Add(mevArb);
                        isArbTx = true;
                    }
                    else
                    {
                        // then add the remaining swaps
                        if (lastArb != null) // will be null if we're skipping due to a clash with sandwiches
                        {
                            MEVHelper.AddArbSwap(lastArb, mevBlock, s, zmSwap);
                            revertableArbSwaps.Add(new SwapRecord(s, zmSwap));
                        }
                        isArbTx = true;
                    }
                }

                // add as simple swap or contract swaps
                if (!isSandwichTx && !isArbTx)
                    MEVHelper.AddSwap(mevBlock, s, zmSwap, ref lastSwap, ref lastContractSwaps);
            }

            Debug.WriteLine($"{sandwichNaive.Value.ToUsd()} sandwich naive profit");
            Debug.WriteLine($"{sandwichNaiveFiltered.Value.ToUsd()} sandwich naive profit filtered");
            Debug.WriteLine($"{sandwichProfitDire.Value.ToUsd()} sandwich dire profit");
            Debug.WriteLine($"{sandwichProfitDireV2.Value.ToUsd()} sandwich dire V2 profit");
            Debug.WriteLine($"{sandwichProfitFrontrun.Value.ToUsd()} sandwich frontrun rate diff profit");
            Debug.WriteLine($"{sandwichProfitBackrun.Value.ToUsd()} sandwich backrun rate diff profit");
            Debug.WriteLine($"{sandwichProfitConservative.Value.ToUsd()} sandwich conservative rate diff profit");
            Debug.WriteLine($"{sandwichVictimImpact.Value.ToUsd()} sandwich victim impact");
            Debug.WriteLine($"{arbTotalUsd.Value.ToUsd()} arb victim loss");
        }

        public void DebugSwaps(long blockNumber)
        {
            Tokens.Load();
            List<DateTime>? arrivals = null;
            DEXs dexs = new DEXs();
            MEVBlock mevBlock = null;

            foreach (var s in Swaps)
            {
                // detect new block number
                if (mevBlock == null || s.BlockNumber != mevBlock.BlockNumber)
                {
                    if (!GetMEVBlock(s.BlockNumber, ref mevBlock, ref arrivals, false))
                        continue;
                }

                if (s.TransactionPosition == null || s.Error != null) continue;
                var zmSwap = dexs.Add(s, arrivals[s.TransactionPosition.Value], out var pair);
#if (DEBUG)
                if (s.BlockNumber == blockNumber)
                {
                    string sKey = MEVHelper.TxKey(s);
                    Debug.WriteLine($"{s.TransactionPosition} {sKey} {zmSwap.SymbolA}:{zmSwap.SymbolB} rate {zmSwap.ExchangeRate().Shorten()} amounts {zmSwap.AmountA} {zmSwap.AmountB} contract {s.ContractAddress} from {s.FromAddress} to {s.FromAddress}");
                }
#endif
            }
        }

        private void VictimImpact(ZMSwap zmSwap, Pair pair)
        {
            // TODO under development
            ZMSwap zmSwapPrevByTime;
            if (zmSwap != null && pair != null && zmSwap.ImpactDelta != null && pair.PreviousZmSwap != null && zmSwap.BRateUsd != null && pair.PreviousZmSwap.ImpactDelta != null)
            {
                if (pair.TimeOrder.TryPredecessor(zmSwap.Order, out var prevEntry))
                {
                    // if the prev swap is of the same sign, it acts against the current swap
                    // if the prev swap is of a different sign, it benefits the current swap
                    // victim impact is expressed as positive when good for the current swap, and negative when bad (frontrun)
                    zmSwapPrevByTime = prevEntry.Value;
                    if (zmSwapPrevByTime.ImpactDelta != null)
                    {
                        ZMDecimal? victimImpact = -(pair.PreviousZmSwap.ImpactDelta - zmSwapPrevByTime.ImpactDelta);
                        ZMDecimal? victimImpactUsd = victimImpact * zmSwap.BRateUsd;
                        if (zmSwap.ImpactDelta < 0) victimImpact = -victimImpact; // switch sells to give a consistent victim impact
                        if (pair.ToString() == "WETH:UsdC")
                            Debug.WriteLine($"{pair} {zmSwap.Order} victim impact ${victimImpactUsd.Value.RoundAwayFromZero(4)} {victimImpact} prev block {pair.PreviousZmSwap.ImpactDelta} prev time {zmSwapPrevByTime.ImpactDelta} current {zmSwap.ImpactDelta} {zmSwap.IsSell}");
                    }
                }
            }
        }

        private bool GetMEVBlock(long blockNumber, ref MEVBlock? mevBlock, ref List<DateTime>? arrivals, bool doSetEthUsd = true)
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

                if (zmb == null && blockNumber >= API.EarliestZMBlock) // don't hit the RPC and DB for data that cannot exist
                {
                    // attempt to get and write arrivals on the fly
                    int? txCount = null;
                    txCount = API.GetBlockTransactionCountByNumber(_http, blockNumber).Result;
                    if (txCount.HasValue)
                    {
                        // filter out invalid tx length extractor rows and calculate arrival times
                        var zb = DB.GetZMBlock(blockNumber);
                        ZMView? zv = null;
                        if (zb != null)
                        {
                            zv = new ZMView(blockNumber);
                            zv.RefreshOffline(zb, txCount.Value);
                        }

                        // if we don’t have enough zm blocks to process by now, wait up until the longer PollTimeoutSecs (it will likely mean the zeromevdb is down or something)
                        if (zb != null && zv != null && zb.UniquePoPCount() >= 2)
                        {
                            using (var db = new zeromevContext())
                            {
                                if (zb != null && zv != null && zv.PoPs != null && zv.PoPs.Count != 0)
                                {
                                    // write the count to the db (useful for later bulk reprocessing/restarts)
                                    var txDataComp = Binary.Compress(Binary.WriteFirstSeenTxData(zv));
                                    zmb = db.AddZmBlock(blockNumber, txCount.Value, zv.BlockTimeAvg, txDataComp).Result;
                                }
                            }
                        }
                    }
                }

                if (zmb != null)
                {
                    var txData = Binary.Decompress(zmb.TxData);
                    arrivals = Binary.ReadFirstSeenTxData(txData);
                    Debug.Assert(arrivals.Count == zmb.TransactionCount);
                    if (mevBlock.ExistingMEV == null)
                        mevBlock.ExistingMEV = new bool[arrivals.Count];
                    mevBlock.BlockTime = zmb.BlockTime;
                }
                else
                {
                    // don't fail just because we can't get or update txcount / arrival times at this point, just return null arrivals
                    // a fatal error will be an unexpected exception caused by a network error or a server going down
                    arrivals = null;
                    if (mevBlock.ExistingMEV == null)
                        mevBlock.ExistingMEV = new bool[10000]; // big enough to handle any block
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
            if (rate == null)
                return null;
            return rate.Rate;
        }

        public static void SetUsdBaseRate(string token, ZMDecimal newRate)
        {
            XRate rate;
            if (!_usdBaseRate.TryGetValue(token, out rate))
            {
                rate = new XRate();
                _usdBaseRate.Add(token, rate);
            }
            rate.Rate = newRate;
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
            if (t == null)
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

        public static string GetSymbol(string token, out ZMDecimal divisor)
        {
            divisor = Pow18; // default to 18 decimals
            var t = Tokens.GetFromAddress(token);
            if (t == null || t.Symbol == null || t.Symbol == Unknown) return token;
            if (t.Decimals.HasValue)
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
            ZMDecimal inDivisor, outDivisor;
            string symbolIn = Tokens.GetSymbol(swap.TokenInAddress, out inDivisor);
            string symbolOut = Tokens.GetSymbol(swap.TokenOutAddress, out outDivisor);

            // apply the decimal divisor
            ZMDecimal tokenIn = (ZMDecimal)(swap.TokenInAmount / inDivisor);
            ZMDecimal tokenOut = (ZMDecimal)(swap.TokenOutAmount / outDivisor);

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
                if (BaseCurrencyA == BaseCurrency.ETH && BaseCurrencyB == BaseCurrency.USD)
                {
                    XRates.ETHBaseRate = zmSwap.ExchangeRate();
                    XRates.SetUsdBaseRate(TokenA, zmSwap.ExchangeRate());
                }
                else if (BaseCurrencyA == null && BaseCurrencyB == BaseCurrency.USD)
                {
                    XRates.SetUsdBaseRate(TokenA, zmSwap.ExchangeRate());
                }
                else if (BaseCurrencyA == null && BaseCurrencyB == BaseCurrency.ETH)
                {
                    if (XRates.ETHBaseRate.HasValue)
                    {
                        ZMDecimal usd = zmSwap.ExchangeRate() * XRates.ETHBaseRate.Value;
                        XRates.SetUsdBaseRate(TokenA, usd);
                    }
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
        public int[] TraceAddress;

        public BlockOrder(long blocknum, int txIndex, int[] traceAddress)
        {
            Blocknum = blocknum;
            TxIndex = txIndex;
            TraceAddress = traceAddress;
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

            // handle null and empty trace address values
            bool noTrace = (this.TraceAddress == null || this.TraceAddress.Length == 0);
            bool noOtherTrace = (other.TraceAddress == null || other.TraceAddress.Length == 0);
            if (noTrace && noOtherTrace) return 0;
            if (!noTrace && noOtherTrace) return 1;
            if (noTrace && !noOtherTrace) return -1;

            // compare trace address arrays directly now we know they both exist
            for (int i = 0; i < this.TraceAddress.Length; i++)
            {
                if (other.TraceAddress.Length < i) break;
                r = this.TraceAddress[i].CompareTo(other.TraceAddress[i]);
                if (r != 0) return r;
            }

            // if the are both equivalent as far as they go, the shorter one takes priority
            return this.TraceAddress.Length.CompareTo(other.TraceAddress.Length);
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
        // ensure in/outs are never zero, the div by zero errors we will get if not are appropriate
        public ZMDecimal AmountA;
        public ZMDecimal AmountB;
        public ZMDecimal? MEVAmount; // 0 = neutral, >0 = positive <0 = negative, null = unclassified

        // determined directly from a USD stable-coin, or via ETH to a USD stable-coin after the swap. Determined within block order, so does not change. Set to 1 if it is already in USD, and null if unknown
        public ZMDecimal? ARateUsd;
        public ZMDecimal? BRateUsd;

        public ZMSwap(BlockOrder blockOrder, DateTime? arrivalTime, bool isSell, ZMDecimal amountIn, ZMDecimal amountOut, string symbolIn, string symbolOut)
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

        public ZMDecimal ExchangeRate()
        {
            return AmountB / AmountA;
        }

        public ZMDecimal InverseExchangeRate()
        {
            return AmountA / AmountB;
        }

        public ZMDecimal InAmount
        {
            get
            {
                return IsSell ? AmountB : AmountA;
            }
        }

        public ZMDecimal OutAmount
        {
            get
            {
                return IsSell ? AmountA : AmountB;
            }
        }
        public ZMDecimal OrigExchangeRate()
        {
            return IsSell ? InverseExchangeRate() : ExchangeRate();
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