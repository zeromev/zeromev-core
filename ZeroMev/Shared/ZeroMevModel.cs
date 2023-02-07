using System;
using System.Text;
using System.Numerics;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
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

    public class ZMSerializeOptions
    {
        static public JsonSerializerOptions Default;
        static public JsonSerializerOptions StringToInt;

        static ZMSerializeOptions()
        {
            Default = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true };
            Default.Converters.Add(new BitArrayConverter());
            Default.Converters.Add(new ZMDecimalConverter());
            Default.Converters.Add(new DateTimeConverter());
            Default.Converters.Add(new DateTimeNullableConverter());

            StringToInt = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true };
            StringToInt.Converters.Add(new StringToIntJsonConverter());
        }
    }

    public class StringToIntJsonConverter : JsonConverter<int?>
    {
        public override int? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            int r;
            if (int.TryParse(reader.GetString()!, out r))
                return r;
            return null;
        }


        public override void Write(
            Utf8JsonWriter writer,
            int? value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString() ?? null);
        }
    }

    // a light client implementation of ITxTime without the tx hash
    public class TxTime : IComparable<TxTime>
    {
        [JsonPropertyName("t")]
        public DateTime ArrivalTime { get; set; }

        [JsonIgnore]
        public long ArrivalBlockNumber { get; set; }

        public int CompareTo(TxTime other)
        {
            return this.ArrivalTime.CompareTo(other.ArrivalTime);
        }
    }

    public class ZMBlock
    {
        [JsonPropertyName("blocknum")]
        public long BlockNumber;

        [JsonPropertyName("last")]
        public long? LastBlockNumber;

        [JsonPropertyName("pop")]
        public List<PoP> PoPs;

        [JsonPropertyName("fb")]
        public BitArray Bundles;

        [JsonPropertyName("mev")]
        public MEVBlock MevBlock;

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

        public int UniquePoPCount()
        {
            if (PoPs == null || PoPs.Count == 0)
                return 0;

            HashSet<int> indexes = new HashSet<int>();
            foreach (PoP pop in PoPs)
                indexes.Add(pop.ExtractorIndex);
            return indexes.Count;
        }
    }

    public class PoP
    {
        [JsonPropertyName("index")]
        public short ExtractorIndex;

        [JsonPropertyName("name")]
        public string Name;

        [JsonPropertyName("blocktime")]
        public DateTime BlockTime;

        [JsonPropertyName("pending")]
        public int PendingCount;

        [JsonPropertyName("start")]
        public DateTime ExtractorStartTime;

        [JsonPropertyName("count")]
        public long ArrivalCount;

        [JsonPropertyName("times")]
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
        public decimal? EthUsd = null;

        // set from zm block
        public long? LastBlockNumber;
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
        public string BlockTimeDetail;
        public string PoPDetail;

        // display members
        public bool HasStats;
        public bool HasZM;
        public bool HasMEV;
        public bool IsQuotaExceeded;

        private DateTime _zmBlockResultTime;
        private bool _isZMBlockResultYoung;

        public OrderBy OrderBy { get; private set; }
        public APIResult BlockResult { get; private set; }
        public APIResult ZMBlockResult { get; private set; }

        public ZMView(long blockNumber)
        {
            BlockNumber = blockNumber;
            MEVSummaries = new MEVSummary[Enum.GetValues(typeof(MEVFilter)).Length];
            MEVSummaries.Initialize();
        }

        // offline processes such as the mev classifier can supply a zm block straight from the database to avoid using the REST api
        public async Task<bool> Refresh(HttpClient http)
        {
            Task<GetBlockByNumber?> blockTask = null;
            Task<string> zbTask = null;

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
                if (BlockNumber < API.EarliestMevBlock)
                    ZMBlockResult = APIResult.NoData;
                else
                    zbTask = API.GetZMBlockJson(http, BlockNumber);
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
                var json = await zbTask;
                var zmBlock = API.ReadZMBlockJson(json, out IsQuotaExceeded);
                if (SetZMBlock(zmBlock))
                {
                    ZMBlockResult = APIResult.Ok;
                    IsQuotaExceeded = false;
                    HasZM = true;
                    _zmBlockResultTime = DateTime.Now;
                    _isZMBlockResultYoung = DateTime.Now.AddSeconds(-API.RecentBlockSecs) < this.BlockTimeAvg;
                }
            }

            return true;
        }

        // an offline version of Refresh that does not require http
        public bool RefreshOffline(ZMBlock zmBlock, GetBlockByNumber block)
        {
            if (!SetBlock(block)) return false;
            return SetZMBlock(zmBlock);
        }

        // an offline version of Refresh that does not require http and accepts a zmblock with tx time data
        public bool RefreshOffline(ZMBlock zmBlock)
        {
            // build transactions without any details
            // supply a GetBlockByNumber to the other overload if details are required
            if (zmBlock.PoPs.Count == 0) return false;
            if (zmBlock.PoPs[0].TxTimes.Count == 0) return false;

            var txCount = zmBlock.PoPs[0].TxTimes.Count;
            Txs = new ZMTx[txCount];
            for (int i = 0; i < txCount; i++)
            {
                ZMTx zmtx = new ZMTx();
                zmtx.TxIndex = i;
                Txs[i] = zmtx;
            }
            TxCount = txCount;
            return SetZMBlock(zmBlock);
        }

        // an offline version of Refresh that does not require http and requires only a block transaction count rather than full data
        public bool RefreshOffline(ZMBlock zmBlock, int txCount)
        {
            // build transactions without any details
            // supply a GetBlockByNumber to the other overload if details are required
            Txs = new ZMTx[txCount];
            for (int i = 0; i < txCount; i++)
            {
                ZMTx zmtx = new ZMTx();
                zmtx.TxIndex = i;
                Txs[i] = zmtx;
            }
            TxCount = txCount;
            return SetZMBlock(zmBlock);
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
            BlockHashShort = Num.ShortenHexAbbr(b.Hash, 16);

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

            LastBlockNumber = zb.LastBlockNumber;

            // basic block members must have already been set and txs initialized
            if (Txs == null || Txs.Length != TxCount)
                return false;

            // set flashbots and mev
            SetFlashbotsBundles(zb.Bundles);
            SetMev(zb.MevBlock);

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
            StringBuilder sb = new StringBuilder();
            foreach (PoP pop in PoPs)
            {
                if (pop.ExtractorIndex == (int)ExtractorPoP.Inf && PoPs.Count != 1) continue; // infura is not a real node and messes up the stats, so only use it if we have to
                popCount++;
                sum += pop.BlockTime.Ticks;
                if (pop.BlockTime < BlockTimeMin) BlockTimeMin = pop.BlockTime;
                if (pop.BlockTime > BlockTimeMax) BlockTimeMax = pop.BlockTime;
                if (pop.PendingCount > PendingCountMax) PendingCountMax = pop.PendingCount;
                sb.Append(pop.Name.ToString());
                sb.Append(" ");
                sb.AppendLine(pop.BlockTime.ToString(Time.Format));
            }
            BlockTimeDetail = sb.ToString();

            // calculate avg and stdev
            sb.Clear();
            if (popCount != 0)
            {
                long avg = sum / popCount;

                double pow = 0;
                foreach (PoP pop in PoPs)
                {
                    if (pop.ExtractorIndex != (int)ExtractorPoP.Inf || PoPs.Count == 1)
                    {
                        long diff = pop.BlockTime.Ticks - avg;
                        TimeSpan ts = new TimeSpan(diff);
                        pow += Math.Pow((double)diff, 2);
                        sb.Append(pop.Name.ToString());
                        sb.Append(" ");
                        sb.AppendLine(ZMTx.DurationStrLong(ts));
                    }
                }

                BlockTimeAvg = new DateTime(avg);
                BlockTimeRangeMin = BlockTimeMin - BlockTimeAvg;
                BlockTimeRangeMax = BlockTimeMax - BlockTimeAvg;
                BlockTimeRangeStdev = new TimeSpan((long)Math.Sqrt(pow / popCount));
                HasStats = true;

                sb.AppendLine("");
                sb.Append("stdev block latency ");
                sb.AppendLine(ZMTx.DurationStrLong(BlockTimeRangeStdev));
                sb.Append("avg block time ");
                sb.AppendLine(BlockTimeAvg.ToString(Time.Format));
            }
            PoPDetail = sb.ToString();
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

        public void SetMev(MEVBlock mb)
        {
            if (mb == null) return;
            MEVSummaries.Initialize();

            for (int i = 0; i < mb.SwapsTx.Count; i++) SetMev(mb.SwapsTx[i], mb, i);
            for (int i = 0; i < mb.Arbs.Count; i++) SetMev(mb.Arbs[i], mb, i);
            for (int i = 0; i < mb.Liquidations.Count; i++) SetMev(mb.Liquidations[i], mb, i);
            for (int i = 0; i < mb.NFTrades.Count; i++) SetMev(mb.NFTrades[i], mb, i);
            for (int i = 0; i < mb.Backruns.Count; i++) SetMev(mb.Backruns[i], mb, i); // the calculation order of backruns / sandwiched / frontruns is important here
            for (int i = 0; i < mb.Sandwiched.Count; i++)
                foreach (var s in mb.Sandwiched[i])
                    SetMev(s, mb, i);
            for (int i = 0; i < mb.Frontruns.Count; i++) SetMev(mb.Frontruns[i], mb, i);

            EthUsd = mb.EthUsd;
            HasMEV = true;
        }

        private void SetMev(IMEV mev, MEVBlock mb, int mevIndex)
        {
            // calculate members
            mev.Cache(mb, mevIndex);

            // calculate summaries
            MEVWeb.UpdateSummaries(mev, MEVSummaries);

            // allocate to transactions by index where possible
            if (mev.TxIndex != null && mev.TxIndex < Txs.Length)
            {
                MEVSummaries[(int)MEVFilter.Info].Count++;
                Txs[mev.TxIndex.Value].MEV = mev;
                return;
            }

            // use hash if we have to
            if (mev.TxHash != null)
            {
                foreach (var tx in Txs)
                {
                    if (tx.TxnHash == mev.TxHash)
                    {
                        MEVSummaries[(int)MEVFilter.Info].Count++;
                        tx.MEV = mev;
                        return;
                    }
                }
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

        public List<ZMTx> GetFiltered(MEVFilter filter)
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

        public int GetPopIndex(ExtractorPoP pop)
        {
            if (PoPs == null || PoPs.Count == 0) return -1;
            return PoPs.FindIndex(a => a.ExtractorIndex == (short)pop);
        }
    }

    public class ZMTx
    {
        // display members are stored as strings for speed of render

        // set from block data (currently Infura)
        public int TxIndex { get; set; }
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
        public string InclusionDelayDetail { get; private set; }
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
        public string TimeOrderDetail { get; private set; }

        // flashbots
        public int? FBBundle { get; set; }
        public int? FBTxIndex { get; set; }
        public string Bundle { get; set; }

        // mev
        public IMEV MEV { get; set; }

        // determined from arrival times
        public int TimeOrderIndex { get; set; }
        public int UnfilteredIndex { get; set; }

        public string MEVAmountStr
        {
            get
            {
                if (MEV == null) return "";
                return Num.ToUsdStr(MEV.MEVAmountUsd);
            }
        }

        public MEVClass MEVClass
        {
            get
            {
                if (MEV == null) return MEVClass.All;
                return MEV.MEVClass;
            }
        }

        public string MEVName
        {
            get
            {
                if (MEV == null) return null;
                return MEV.MEVType.ToString();
            }
        }

        public string MEVActionSummary
        {
            get
            {
                if (MEV == null) return null;
                return MEV.ActionSummary;
            }
        }

        public string MEVActionDetail
        {
            get
            {
                if (MEV == null) return null;
                return MEV.ActionDetail;
            }
        }

        public string MEVDetail
        {
            get
            {
                if (MEV == null) return null;
                return MEV.MEVDetail;
            }
        }

        public MEVFilter MEVFilter
        {
            get
            {
                if (MEV == null) return MEVFilter.All;
                return MEVWeb.GetMEVFilter(MEV.MEVClass);
            }
        }

        public DateTime? GetArrivalTime(int popIndex)
        {
            if (popIndex == -1 || Arrivals == null) return null;
            if (popIndex >= Arrivals.Length) return null;
            if (Arrivals[popIndex] == null) return null;
            return Arrivals[popIndex].ArrivalTime;
        }

        public void SetFromInfuraTx(Transaction infTx)
        {
            if (infTx == null)
                return;

            TxnHash = infTx.Hash;
            TxIndex = Num.HexToInt(infTx.TransactionIndex);
            TxnHashShort = $"<a href=\"https://etherscan.io/tx/{infTx.Hash}\" target=\"_blank\">{Num.ShortenHexAbbr(infTx.Hash, 16)}</a>";
            From = infTx.From;
            FromShort = $"<a href=\"https://etherscan.io/address/{infTx.From}\" target=\"_blank\" >{Num.ShortenHexAbbr(infTx.From, 16)}</a>";
            To = infTx.To;
            ToShort = $"<a href=\"https://etherscan.io/address/{infTx.To}\" target=\"_blank\" >{Num.ShortenHexAbbr(infTx.To, 16)}</a>";
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
                InclusionDelayDetail = "not seen by any node, inserted by the miner.";
                TimeOrderDetail = "inserted by the miner at " + TimeOrder.ToString(Time.Format);
            }
            else
            {
                TimeOrder = min;
                InclusionDelay = zv.BlockTimeAvg - TimeOrder;
                InclusionDelayShort = DurationStr(InclusionDelay);
                InclusionDelayDetail = GetInclusionDelayDetail(zv);
                TimeOrderDetail = GetTimeOrderDetail(zv);
            }

            if (ValidCount > 1)
            {
                ArrivalStdev = new TimeSpan((long)Math.Sqrt(pow / ValidCount));
                ArrivalMean = new DateTime(avg);
            }
        }

        private string GetInclusionDelayDetail(ZMView zv)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < zv.PoPs.Count; i++)
            {
                if (Arrivals[i] == null) continue;
                TimeSpan delay = zv.BlockTimeAvg - Arrivals[i].ArrivalTime;
                sb.Append(zv.PoPs[i].Name);
                sb.Append(" ");
                sb.Append(DurationStrLong(delay));
                sb.Append(" ");
                if (Arrivals[i].ArrivalTime == ArrivalMin)
                    sb.Append(" (first seen)");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private string GetTimeOrderDetail(ZMView zv)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < zv.PoPs.Count; i++)
            {
                if (Arrivals[i] == null) continue;
                sb.Append(zv.PoPs[i].Name);
                sb.Append(" ");
                sb.Append(Arrivals[i].ArrivalTime.ToString(Time.Format));
                sb.Append(" ");
                if (Arrivals[i].ArrivalTime == ArrivalMin)
                    sb.Append(" (first seen)");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public bool Filter(MEVFilter filter)
        {
            if (filter == MEVFilter.All) return true;
            if (filter == MEVFilter.Info && MEV != null) return true;
            return this.MEVFilter == filter;
        }

        public bool FilterOut(MEVFilter filter)
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
            double msAbs = Math.Abs(ms);
            string neg = ms < 0 ? "-" : "";

            if (msAbs < 1)
                return "0 ms";
            else if (msAbs < 1000)
                return $"{neg}{((int)ts.TotalMilliseconds)} ms";
            else if (msAbs < 1000D * 60)
                return $"{neg}{ts.Seconds} secs";
            else if (msAbs < 1000D * 60 * 60)
                return $"{neg}{ts.Minutes} mins {ts.Seconds} secs";
            else if (msAbs < 1000D * 60 * 60 * 24)
                return $"{neg}{ts.Hours} hrs {ts.Minutes} mins";
            else
                return $"{neg}{ts.Days} days  {ts.Hours} hrs";
        }

        public static string DurationStrLong(TimeSpan ts)
        {
            // this doesn't work in the static Time library for some reason, and must be copied locally
            double ms = ts.TotalMilliseconds;
            double msAbs = Math.Abs(ms);
            string neg = ms < 0 ? "-" : "";

            if (msAbs < 1000D * 60)
                return $"{neg}{ts.ToString("s\\.fff")} secs";
            else if (msAbs < 1000D * 60 * 60)
                return $"{neg}{ts.Minutes} mins {ts.ToString("s\\.fff")} secs";
            else if (msAbs < 1000D * 60 * 60 * 24)
                return $"{neg}{ts.Hours} hrs {ts.Minutes} mins {ts.ToString("s\\.fff")} secs";
            else
                return $"{neg}{ts.Days} days {ts.Hours} hrs {ts.Minutes} mins {ts.ToString("s\\.fff")} secs";
        }

        public static int CompareByTimeOrder(ZMTx a, ZMTx b)
        {
            if (a.TimeOrder == b.TimeOrder)
                return a.TxIndex.CompareTo(b.TxIndex);
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
        public long? LastBlockNumber;
        public ZMViewCache ZMViewCache = new ZMViewCache();
        public TxhCache TxhCache = new TxhCache();
        public AccountCache AccountCache = new AccountCache();

        public long? SetLastBlockNumber(long? lastBlockNumber)
        {
            if (lastBlockNumber != null && (LastBlockNumber == null || lastBlockNumber > LastBlockNumber))
                LastBlockNumber = lastBlockNumber;
            return LastBlockNumber;
        }
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
        int _page;
        int _offset;
        TxList _txList;

        public async Task<TxList> Get(HttpClient http, string address, int page, int offset)
        {
            // return null on format error
            if (address.Length != 42 || !Num.IsValidHex(address))
                return null;

            // return the cached value if we have it
            if (_address == address && _page == page && _offset == offset)
                return _txList;

            // retrieve it if we don't
            try
            {
                var txList = await API.GetAccountByAddress(http, address, page, offset);

                // cache it for next time
                if (txList != null)
                {
                    _txList = txList;
                    _address = address;
                    _page = page;
                    _offset = offset;
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