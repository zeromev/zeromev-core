using System;
using System.Collections;
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

        public static long GetLastZmProcessedBlock(this zeromevContext db)
        {
            return (long)db.ZmLatestBlockUpdates.Max(x => x.BlockNumber);
        }

        public static async Task<ZmBlock> AddZmBlock(this zeromevContext db, long blockNumber, int txCount, DateTime? blockTime, byte[]? txData, BitArray txStatus, byte[]? txAddresses)
        {
            var zmb = new ZmBlock() { BlockNumber = blockNumber, TransactionCount = txCount, BlockTime = blockTime, TxData = txData, TxStatus = txStatus, TxAddresses = txAddresses };

            try
            {
                db.ZmBlocks.Add(zmb);
                await db.SaveChangesAsync();
                return zmb;
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                // allow failure if we're trying to add a duplicate
                return zmb;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public static async Task SetLastProcessedBlock(this zeromevContext db, long blockNumber)
        {
            // only update a higher value
            var lastProcessed = await db.ZmLatestBlockUpdates.ToListAsync();
            if (blockNumber <= lastProcessed[0].BlockNumber) return;

            // remove any previous rows
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