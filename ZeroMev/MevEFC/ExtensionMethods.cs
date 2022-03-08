using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ZeroMev.MevEFC
{
    public static class ExtensionMethods
    {
        public static long GetLastProcessedMevInspectBlock(this zeromevContext db)
        {
            return (long)db.LatestBlockUpdates.Max(x => x.BlockNumber);
        }

        public static long GetLastProcessedBlock(this zeromevContext db)
        {
            return (long)db.ZmLatestBlockUpdates.Max(x => x.BlockNumber);
        }

        public static async Task<ZmBlock> AddZmBlock(this zeromevContext db, long blockNumber, int txCount, DateTime blockTime, byte[] txData)
        {
            var zmb = new ZmBlock() { BlockNumber = blockNumber, TransactionCount = txCount, BlockTime = blockTime, TxData = txData };

            try
            {
                db.ZmBlocks.Add(zmb);
                await db.SaveChangesAsync();
                return zmb;
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                {
                    // allow failure if we're trying to add a duplicate
                    if (ex.InnerException.Message.Contains("duplicate key"))
                        return zmb;
                }
                return null;
            }
        }

        public static async Task SetLastProcessedBlock(this zeromevContext db, long blockNumber)
        {
            // remove any previous rows
            var lastProcessed = await db.ZmLatestBlockUpdates.ToListAsync();
            if (lastProcessed != null)
                foreach (var update in lastProcessed)
                    db.Remove(update);

            // add the new row
            ZmLatestBlockUpdate lastUpdate = new ZmLatestBlockUpdate()
            {
                BlockNumber = blockNumber,
                UpdatedAt = DateTime.Now
            };
            db.ZmLatestBlockUpdates.Add(lastUpdate);
            await db.SaveChangesAsync();
        }
    }
}