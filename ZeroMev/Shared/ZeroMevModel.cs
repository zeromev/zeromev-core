using System;
using System.Numerics;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZeroMev.Shared
{
    public enum ExtractorPoP
    {
        Inf = 0,
        Qn = 1,
        US = 2,
        EU = 3,
        AS = 4
    }

    public enum OrderBy
    {
        Block,
        Time,
        Gas
    }

    public enum APIResult
    {
        Retry,
        NoData,
        Ok
    }

    public enum FilterTxsBy
    {
        All,
        MEV,
        ToxicMEV,
        OtherMEV
    }

    public class ZMSerializeOptions
    {
        static public JsonSerializerOptions Default;

        static ZMSerializeOptions()
        {
            Default = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true };
            Default.Converters.Add(new BitArrayConverter());
        }
    }

    // a light client implementation of ITxTime without the tx hash
    public class TxTime : IComparable<TxTime>
    {
        [JsonPropertyName("t")]
        public DateTime ArrivalTime { get; set; }

        [JsonPropertyName("b")]
        public long ArrivalBlockNumber { get; set; }

        public int CompareTo(TxTime other)
        {
            return this.ArrivalTime.CompareTo(other.ArrivalTime);
        }
    }

    public class ZMBlock
    {
        [JsonPropertyName("blockNumber")]
        public long BlockNumber;

        [JsonPropertyName("PoPs")]
        public List<PoP> PoPs;

        [JsonPropertyName("fbBundles")]
        public BitArray Bundles;

        [JsonConstructor]
        public ZMBlock()
        {
        }

        public ZMBlock(long blockNumber, List<PoP> extractors)
        {
            BlockNumber = blockNumber;
            PoPs = extractors;
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, ZMSerializeOptions.Default);
        }
    }

    public class PoP
    {
        [JsonPropertyName("index")]
        public short ExtractorIndex;

        [JsonPropertyName("name")]
        public string Name;

        [JsonPropertyName("blockTime")]
        public DateTime BlockTime;

        [JsonPropertyName("pendingCount")]
        public int PendingCount;

        [JsonPropertyName("extractorStartTime")]
        public DateTime ExtractorStartTime;

        [JsonPropertyName("arrivalCount")]
        public long ArrivalCount;

        [JsonPropertyName("txTimes")]
        public List<TxTime> TxTimes;

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, ZMSerializeOptions.Default);
        }
    }

    public class ZMView
    {
        public long BlockNumber;
        public int TxCount;
        public string BlockHash;
        public string BlockHashShort;

        // set from zm block
        public List<PoP> PoPs;
        public DateTime BlockTimeAvg;
        public DateTime BlockTimeMin;
        public DateTime BlockTimeMax;
        public TimeSpan BlockTimeRangeStdev;
        public TimeSpan BlockTimeRangeMin;
        public TimeSpan BlockTimeRangeMax;
        public TimeSpan TxMeanStdev;
        public TimeSpan InclusionDelayMean;
        public TimeSpan InclusionDelayStdev;
        public TimeSpan InclusionDelayMax;
        public string InclusionDelayMeanShort;
        public string InclusionDelayStdevShort;
        public string InclusionDelayMaxShort;
        public int PendingCountMax;
        public int ValidPopCount;
        public ZMTx[] Txs;
        public MEVSummary[] MEVSummaries;
        public int MEVCount;
        public int MEVOtherCount;
        public int MEVToxicCount;
        public decimal MEVAmount;
        public decimal MEVOtherAmount;
        public decimal MEVToxicAmount;

        // display members
        public bool HasStats;
        public bool HasZM;

        private DateTime _zmBlockResultTime;
        private bool _isZMBlockResultYoung;

        public OrderBy OrderBy { get; private set; }
        public APIResult BlockResult { get; private set; }
        public APIResult ZMBlockResult { get; private set; }

        public ZMView(long blockNumber)
        {
            BlockNumber = blockNumber;
        }

        public async Task<bool> Refresh(HttpClient http)
        {
            Task<GetBlockByNumber?> blockTask = null;
            Task<ZMBlock?> zbTask = null;

            // churn caches of recent blocks as data may still be arriving
            if (ZMBlockResult == APIResult.Ok)
            {
                if (_isZMBlockResultYoung && DateTime.Now.AddSeconds(-API.ExpireRecentCacheSecs) > _zmBlockResultTime)
                    ZMBlockResult = APIResult.Retry;
            }

            // request all async
            if (BlockResult == APIResult.Retry) blockTask = API.GetBlockByNumber(http, BlockNumber);
            if (ZMBlockResult == APIResult.Retry)
            {
                if (BlockNumber < API.EarliestFlashbotsBlock)
                    ZMBlockResult = APIResult.NoData;
                else
                    zbTask = API.GetZMBlock(http, BlockNumber);
            }

            // await block result
            if (BlockResult == APIResult.Retry)
            {
                var block = await blockTask;
                if (!SetBlock(block))
                    return false; // no progress without valid block data
                BlockResult = APIResult.Ok;
            }

            // await zm block result
            if (ZMBlockResult == APIResult.Retry)
            {
                ZMBlockResult = APIResult.NoData;
                var zb = await zbTask;
                if (SetZMBlock(zb))
                {
                    ZMBlockResult = APIResult.Ok;
                    HasZM = true;
                    _zmBlockResultTime = DateTime.Now;
                    _isZMBlockResultYoung = DateTime.Now.AddSeconds(-API.RecentBlockSecs) < this.BlockTimeAvg;
                }
            }

            return true;
        }

        public bool SetBlock(GetBlockByNumber block)
        {
            if (block == null || block.Result == null)
                return false;

            // set block basics
            var b = block.Result;
            BlockNumber = Num.HexToLong(b.Number);
            TxCount = b.Transactions == null ? 0 : b.Transactions.Count;
            BlockHash = b.Hash;
            BlockHashShort = Num.ShortenHex(b.Hash, 16);

            // build transactions
            Txs = new ZMTx[TxCount];
            bool success = true;
            for (int i = 0; i < TxCount; i++)
            {
                ZMTx zmtx = new ZMTx();
                zmtx.SetFromInfuraTx(b.Transactions[i]);

                // validate that tx indexes are ordinal
                if (zmtx.TxIndex != i)
                    success = false;
                Txs[i] = zmtx;
            }

            return success;
        }

        public bool SetZMBlock(ZMBlock zb)
        {
            if (zb == null)
                return false;

            // basic block members must have already been set and txs initialized
            if (Txs == null || Txs.Length != TxCount)
                return false;

            // set flashbots bundle indexes
            SetFlashbotsBundles(zb.Bundles);

            // temporarily mock-up mev to build the front end TODO replace this
            MEVBlock mockMev = MockMEV();
            SetMEV(mockMev);

            // require zm block data
            if (zb.PoPs == null || zb.PoPs.Count == 0)
                return false;

            // filter out uncles
            List<PoP> newPoPs = new List<PoP>(zb.PoPs.Count);
            short lastIndex = -1;
            foreach (PoP p in zb.PoPs)
            {
                if (p.ExtractorIndex != lastIndex && p.TxTimes.Count == TxCount)
                {
                    newPoPs.Add(p);
                    lastIndex = p.ExtractorIndex;
                }
            }
            PoPs = newPoPs;

            // calculate block time members
            long sum = 0;
            BlockTimeMax = DateTime.MinValue;
            BlockTimeMin = DateTime.MaxValue;
            PendingCountMax = 0;
            int popCount = 0;
            foreach (PoP pop in PoPs)
            {
                if (pop.ExtractorIndex == (int)ExtractorPoP.Inf && PoPs.Count != 1) continue; // infura is not a real node and messes up the stats, so only use it if we have to
                popCount++;
                sum += pop.BlockTime.Ticks;
                if (pop.BlockTime < BlockTimeMin) BlockTimeMin = pop.BlockTime;
                if (pop.BlockTime > BlockTimeMax) BlockTimeMax = pop.BlockTime;
                if (pop.PendingCount > PendingCountMax) PendingCountMax = pop.PendingCount;
            }

            // calculate avg and stdev
            if (popCount != 0)
            {
                long avg = sum / popCount;

                double pow = 0;
                foreach (PoP pop in PoPs)
                    if (pop.ExtractorIndex != (int)ExtractorPoP.Inf)
                        pow += Math.Pow((double)(pop.BlockTime.Ticks - avg), 2);

                BlockTimeAvg = new DateTime(avg);
                BlockTimeRangeMin = BlockTimeMin - BlockTimeAvg;
                BlockTimeRangeMax = BlockTimeMax - BlockTimeAvg;
                BlockTimeRangeStdev = new TimeSpan((long)Math.Sqrt(pow / popCount));
                HasStats = true;
            }
            ValidPopCount = popCount;

            // pivot multiple pop arrival times into single tx rows
            long max = long.MinValue;
            long sumStDev = 0;
            int count = 0;
            long delay = 0;
            long maxDelay = 0;
            for (int i = 0; i < TxCount; i++)
            {
                Txs[i].SetFromZMTx(this, PoPs, i);
                if (!Txs[i].ArrivalStdev.HasValue) continue;
                count++;
                sumStDev += Txs[i].ArrivalStdev.Value.Ticks;
                delay += Txs[i].InclusionDelay.Ticks;
                if (Txs[i].InclusionDelay.Ticks > maxDelay)
                    maxDelay = Txs[i].InclusionDelay.Ticks;
            }
            if (count != 0)
            {
                TxMeanStdev = new TimeSpan(sumStDev / count);
                InclusionDelayMean = new TimeSpan(delay / count);
                InclusionDelayMeanShort = ZMTx.DurationStr(InclusionDelayMean);
                InclusionDelayMax = new TimeSpan(maxDelay);
                InclusionDelayMaxShort = ZMTx.DurationStr(InclusionDelayMax);
            }

            // calculate time order and heatmap
            SetOrderBy(OrderBy.Time);
            byte r, g, b;
            for (int i = 0; i < TxCount; i++)
            {
                Heatmap.GetLookup(i, TxCount, out r, out g, out b);
                ZMTx tx = Txs[i];
                tx.TimeOrderIndex = i;
                tx.R = r;
                tx.G = g;
                tx.B = b;
                string rgb = $"rgb({r},{g},{b})";
                Txs[i].HeatmapRGB = rgb;
            }
            SetOrderBy(OrderBy.Block);

            return true;
        }

        public void SetFlashbotsBundles(BitArray bundles)
        {
            if (bundles == null || bundles.Length > Txs.Length)
                return;

            // convert the bits into bundles using option base 1 to match the flashbots explorer, although the api is option base 0
            int bundle = 1;
            int txIndex = 1;
            for (int i = 0; i < bundles.Length; i++)
            {
                if (bundles[i])
                {
                    bundle++;
                    txIndex = 1;
                }
                ZMTx tx = Txs[i];
                tx.FBBundle = bundle;
                tx.FBTxIndex = txIndex;
                tx.Bundle = bundle + "." + txIndex;
                txIndex++;
            }
        }

        public MEVBlock MockMEV()
        {
            // create a model instance that will in time have been returned via the api
            MEVBlock mm = new MEVBlock(BlockNumber);
            mm.MockMEV(TxCount);
            mm.BuildMEVSummaries();
            return mm;
        }

        public void SetMEV(MEVBlock mm)
        {
            MEVCount = mm.MEVCount;
            MEVToxicCount = mm.MEVToxicCount;
            MEVOtherCount = mm.MEVOtherCount;

            MEVAmount = mm.MEVAmount;
            MEVToxicAmount = mm.MEVToxicAmount;
            MEVOtherAmount = mm.MEVOtherAmount;

            if (mm.Rows == null)
                return;

            foreach (var r in mm.Rows)
            {
                ZMTx tx = Txs[r.Index];
                tx.MEVType = r.MEVType;
                tx.MEVAmount = r.MEVAmount;
                tx.MEVComment = r.MEVComment;
            }
        }

        public bool SetOrderBy(OrderBy newOrderBy)
        {
            if (newOrderBy == OrderBy || Txs == null)
                return false;

            switch (newOrderBy)
            {
                case OrderBy.Block:
                    Array.Sort<ZMTx>(Txs, ZMTx.CompareByTxIndex);
                    break;

                case OrderBy.Time:
                    Array.Sort<ZMTx>(Txs, ZMTx.CompareByTimeOrder);
                    break;

                case OrderBy.Gas:
                    Array.Sort<ZMTx>(Txs, ZMTx.CompareByGas);
                    break;
            }

            OrderBy = newOrderBy;
            return true;
        }

        public List<ZMTx> GetFiltered(FilterTxsBy filter)
        {
            if (Txs.Length == 0)
                return new List<ZMTx>();

            List<ZMTx> f = new List<ZMTx>(Txs.Length);
            int i = 0;
            foreach (var tx in Txs)
            {
                tx.UnfilteredIndex = i++;
                if (tx.Filter(filter))
                    f.Add(tx);
            }
            return f;
        }
    }

    public class ZMTx
    {
        // display members are stored as strings for speed of render

        // set from block data (currently Infura)
        public int TxIndex { get; private set; }
        public string TxnHash { get; private set; }
        public string TxnHashShort { get; private set; }
        public string From { get; private set; }
        public string FromShort { get; private set; }
        public string To { get; private set; }
        public string ToShort { get; private set; }
        public string ValueHex { get; private set; }
        public string Value { get; private set; }
        public string GasPriceHex { get; private set; }
        public BigInteger GasPriceBigInt { get; private set; }
        public string GasPrice { get; private set; }

        // set from zm block data
        public TimeSpan InclusionDelay { get; private set; }
        public string InclusionDelayShort { get; private set; }
        public string HeatmapRGB { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public bool IsMiner { get; private set; }
        public int ValidCount { get; private set; }
        public DateTime TimeOrder { get; private set; }
        public TxTime[] Arrivals { get; private set; }
        public DateTime ArrivalMin { get; private set; }
        public DateTime? ArrivalMean { get; private set; }
        public TimeSpan? ArrivalStdev { get; private set; }

        // flashbots
        public int? FBBundle { get; set; }
        public int? FBTxIndex { get; set; }
        public string Bundle { get; set; }

        // mev
        public MEVType MEVType { get; set; }
        public decimal? MEVAmount { get; set; }
        public string MEVComment { get; set; }

        // determined from arrival times
        public int TimeOrderIndex { get; set; }
        public int UnfilteredIndex { get; set; }

        public string MEVAmountStr
        {
            get
            {
                if (!MEVAmount.HasValue) return "";
                return "$" + MEVAmount.Value.ToString("0.00");
            }
        }

        public void SetFromInfuraTx(Transaction infTx)
        {
            if (infTx == null)
                return;

            TxnHash = infTx.Hash;
            TxIndex = Num.HexToInt(infTx.TransactionIndex);
            TxnHashShort = Num.ShortenHex(infTx.Hash, 16);
            From = infTx.From;
            FromShort = Num.ShortenHex(infTx.From, 16);
            To = infTx.To;
            ToShort = Num.ShortenHex(infTx.To, 16);
            ValueHex = infTx.Value;
            Value = Num.HexToValue(infTx.Value);
            GasPriceHex = infTx.GasPrice;
            GasPriceBigInt = Num.HexToBigInt(infTx.GasPrice);
            GasPrice = Num.BigIntToGasPrice(GasPriceBigInt);
        }

        public void SetFromZMTx(ZMView zv, List<PoP> PoPs, int txIndex)
        {
            Arrivals = new TxTime[PoPs.Count];
            for (int i = 0; i < PoPs.Count; i++)
                Arrivals[i] = PoPs[i].TxTimes[txIndex];

            CalculateZMMembers(zv);
        }

        private void CalculateZMMembers(ZMView zv)
        {
            // set the eariest arrival time
            DateTime min = DateTime.MaxValue;
            foreach (TxTime tx in Arrivals)
            {
                if (tx.ArrivalTime < min)
                    min = tx.ArrivalTime;
            }

            ValidCount = 0;
            int maxPending = 0;
            for (int i = 0; i < zv.PoPs.Count; i++)
            {
                // set the maximum pending count
                PoP pop = zv.PoPs[i];
                if (pop.PendingCount > maxPending)
                    maxPending = pop.PendingCount;

                // discard samples where the earliest arrival time is before that extractor's start time as it was not running
                if (min < zv.PoPs[i].ExtractorStartTime)
                {
                    Arrivals[i] = null;
                    continue;
                }
                ValidCount++;
            }
            if (ValidCount == 0) return;

            // determine miner txs and calculate arrival time mean
            long sum = 0;
            int minerCount = 0;
            for (int i = 0; i < zv.PoPs.Count; i++)
            {
                if (Arrivals[i] == null) continue;
                TimeSpan diff = Arrivals[i].ArrivalTime - zv.PoPs[i].BlockTime;
                if (diff.Ticks > -100 && diff.Ticks < 100) // range needed for now due to conversion errors TODO data fix so this is not required
                    minerCount++; // miner txs are indicated by having matching block/tx arrival times
                sum += Arrivals[i].ArrivalTime.Ticks;
            }
            long avg = sum / ValidCount;

            // all valid PoP's must agree for it to be considered a miner tx
            IsMiner = (ValidCount == minerCount);

            // set arrival time min, mean and stdev
            double pow = 0;
            foreach (TxTime tx in Arrivals)
                if (tx != null)
                    pow += Math.Pow((double)(tx.ArrivalTime.Ticks - avg), 2);

            ArrivalMin = min;
            TimeOrder = min; // for now, we will take first seen as send time order approximation

            if (IsMiner)
            {
                TimeOrder = zv.BlockTimeAvg;
                InclusionDelay = new TimeSpan(0);
                InclusionDelayShort = "miner";
            }
            else
            {
                TimeOrder = min;
                InclusionDelay = zv.BlockTimeAvg - TimeOrder;
                InclusionDelayShort = DurationStr(InclusionDelay);
            }

            if (ValidCount > 1)
            {
                ArrivalStdev = new TimeSpan((long)Math.Sqrt(pow / ValidCount));
                ArrivalMean = new DateTime(avg);
            }
        }

        public bool Filter(FilterTxsBy filter)
        {
            switch (filter)
            {
                case FilterTxsBy.All:
                    return true;

                case FilterTxsBy.MEV:
                    return MEV.Get(this.MEVType).IsVisible;

                case FilterTxsBy.ToxicMEV:
                    return MEV.Get(this.MEVType).IsVisible && MEV.Get(this.MEVType).IsToxic;

                case FilterTxsBy.OtherMEV:
                    return MEV.Get(this.MEVType).IsVisible && !MEV.Get(this.MEVType).IsToxic;

                default:
                    return false;
            }
        }

        public bool FilterOut(FilterTxsBy filter)
        {
            return !Filter(filter);
        }

        public string TrId(int? selectedIndex)
        {
            string trId = null;
            if (selectedIndex.HasValue && selectedIndex.Value == TxIndex)
                trId = "selected";
            return trId;
        }

        public string TrClass(int? selectedIndex)
        {
            string trClass = null;
            if (selectedIndex.HasValue && selectedIndex.Value == TxIndex)
            {
                // active row
                trClass = "table-active";
            }
            else if (FBBundle.HasValue)
            {
                // flashbots bundle row
                trClass = (FBBundle.Value % 2 != 0) ? " tr-fb-odd" : " tr-fb-even";
            }
            return trClass;
        }

        public static string DurationStr(TimeSpan ts)
        {
            // this doesn't work in the static Time library for some reason, and must be copied locally
            double ms = ts.TotalMilliseconds;

            if (ms < 1)
                return "0 ms";
            else if (ms < 1000)
                return ((int)ts.TotalMilliseconds) + " ms";
            else if (ms < 1000D * 60)
                return ts.Seconds + " secs";
            else if (ms < 1000D * 60 * 60)
                return ts.Minutes + " mins " + ts.Seconds + " secs";
            else if (ms < 1000D * 60 * 60 * 24)
                return ts.Hours + " hrs " + ts.Minutes + " mins";
            else
                return ts.Days + " days " + ts.Hours + " hrs";
        }

        public static int CompareByTimeOrder(ZMTx a, ZMTx b)
        {
            return a.TimeOrder.CompareTo(b.TimeOrder);
        }

        public static int CompareByTxIndex(ZMTx a, ZMTx b)
        {
            return a.TxIndex.CompareTo(b.TxIndex);
        }

        public static int CompareByGas(ZMTx a, ZMTx b)
        {
            if (b.GasPriceBigInt == a.GasPriceBigInt)
                return a.TxIndex.CompareTo(b.TxIndex);
            return b.GasPriceBigInt.CompareTo(a.GasPriceBigInt);
        }
    }

    public class ZMCache
    {
        public ZMViewCache ZMViewCache = new ZMViewCache();
        public TxhCache TxhCache = new TxhCache();
        public AccountCache AccountCache = new AccountCache();
    }

    public class ZMViewCache
    {
        // implements complex caching of each data source over multiple blocks

        public int Size { get; private set; }

        private Dictionary<long, ZMView> _cache = new Dictionary<long, ZMView>();

        public ZMViewCache()
        {
            Size = 10;
        }

        public ZMViewCache(int size)
        {
            Size = size;
        }

        public async Task<ZMView> Get(HttpClient http, long blockNumber)
        {
            // get from the cache and update if possible
            ZMView zv;
            if (_cache.TryGetValue(blockNumber, out zv))
                return await zv.Refresh(http) ? zv : null;

            // create new and add to the cache if not
            zv = new ZMView(blockNumber);

            // halve the cache before adding if we hit our limit
            if (_cache.Count >= Size)
            {
                int trimmed = 0;
                int targetTrim = Size / 2;
                foreach (var k in _cache.Keys)
                {
                    trimmed++;
                    _cache.Remove(k);
                    if (trimmed > targetTrim) break;
                }
            }
            _cache.Add(blockNumber, zv);
            return await zv.Refresh(http) ? zv : null;
        }
    }

    public class TxhCache
    {
        // cache many tx hashes as they are memory light and we want to save calls
        public int Size { get; private set; }

        private Dictionary<string, Txh> _cache = new Dictionary<string, Txh>();

        public TxhCache()
        {
            Size = 4;
        }

        public TxhCache(int size)
        {
            Size = size;
        }

        public async Task<Txh> Get(HttpClient http, string fromTxh)
        {
            // return null on format error
            if (fromTxh.Length != 66 || !Num.IsValidHex(fromTxh))
                return new Txh(APIResult.NoData); // no point adding to the cache, it is quick to determine

            // return from the cache if we can
            Txh r;
            if (_cache.TryGetValue(fromTxh, out r))
            {
                if (r.APIResult != APIResult.Retry)
                    return r;
                _cache.Remove(fromTxh);
            }

            try
            {
                // if not get if from etherscan
                var getTxnByHash = await API.GetTxByHash(http, fromTxh);

                // always return retry, because a hash that is not currently visible may become visible
                if (getTxnByHash == null || getTxnByHash.Result == null)
                    return new Txh(APIResult.Retry); // no point adding to the cache, we will retry anyway without it

                var tx = getTxnByHash.Result;
                r = new Txh(fromTxh, Num.HexToLong(tx.BlockNumber), Num.HexToInt(tx.TransactionIndex));

                // halve the cache before adding if we hit our limit
                if (_cache.Count >= Size)
                {
                    int trimmed = 0;
                    int targetTrim = Size / 2;
                    foreach (var k in _cache.Keys)
                    {
                        trimmed++;
                        _cache.Remove(k);
                        if (trimmed > targetTrim) break;
                    }
                }
                _cache.Add(fromTxh, r);
                return r;
            }
            catch (Exception ex)
            {
                return new Txh(APIResult.Retry);
            }
        }
    }

    public class Txh
    {
        public Txh(APIResult result)
        {
            APIResult = result;
        }

        public Txh(string txHash, long blockNumber, int txIndex)
        {
            TxHash = txHash;
            BlockNumber = blockNumber;
            TxIndex = txIndex;
            APIResult = APIResult.Ok;
        }

        public string TxHash { get; private set; }
        public long BlockNumber { get; private set; }
        public int TxIndex { get; private set; }
        public APIResult APIResult { get; set; }
    }

    public class AccountCache
    {
        // cache a single account to avoid repeated calls as users iterate each transaction in an account
        string _address;
        TxList _txList;

        public async Task<TxList> Get(HttpClient http, string address)
        {
            // return null on format error
            if (address.Length != 42 || !Num.IsValidHex(address))
                return null;

            // return the cached value if we have it
            if (_address == address)
                return _txList;

            // retrieve it if we don't
            try
            {
                var txList = await API.GetAccountByAddress(http, address);

                // cache it for next time
                if (txList != null)
                {
                    _txList = txList;
                    _address = address;
                }
                return txList;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}