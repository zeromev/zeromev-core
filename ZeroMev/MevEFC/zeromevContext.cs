﻿using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ZeroMev.MevEFC
{
    public partial class zeromevContext : DbContext
    {
        public zeromevContext()
        {
        }

        public zeromevContext(DbContextOptions<zeromevContext> options)
            : base(options)
        {
        }

        public virtual DbSet<AlembicVersion> AlembicVersions { get; set; } = null!;
        public virtual DbSet<Arbitrage> Arbitrages { get; set; } = null!;
        public virtual DbSet<ArbitrageSwap> ArbitrageSwaps { get; set; } = null!;
        public virtual DbSet<Block> Blocks { get; set; } = null!;
        public virtual DbSet<ClassifiedTrace> ClassifiedTraces { get; set; } = null!;
        public virtual DbSet<LatestBlockUpdate> LatestBlockUpdates { get; set; } = null!;
        public virtual DbSet<Liquidation> Liquidations { get; set; } = null!;
        public virtual DbSet<MinerPayment> MinerPayments { get; set; } = null!;
        public virtual DbSet<NftTrade> NftTrades { get; set; } = null!;
        public virtual DbSet<Price> Prices { get; set; } = null!;
        public virtual DbSet<PunkBid> PunkBids { get; set; } = null!;
        public virtual DbSet<PunkBidAcceptance> PunkBidAcceptances { get; set; } = null!;
        public virtual DbSet<PunkSnipe> PunkSnipes { get; set; } = null!;
        public virtual DbSet<Sandwich> Sandwiches { get; set; } = null!;
        public virtual DbSet<SandwichedSwap> SandwichedSwaps { get; set; } = null!;
        public virtual DbSet<Swap> Swaps { get; set; } = null!;
        public virtual DbSet<Token> Tokens { get; set; } = null!;
        public virtual DbSet<Transfer> Transfers { get; set; } = null!;
        public virtual DbSet<ZmLatestBlockUpdate> ZmLatestBlockUpdates { get; set; } = null!;
        public virtual DbSet<ZmTime> ZmTimes { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseNpgsql(Config.Get("ZM_MEV_DB"));
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlembicVersion>(entity =>
            {
                entity.HasKey(e => e.VersionNum)
                    .HasName("alembic_version_pkc");

                entity.ToTable("alembic_version");

                entity.Property(e => e.VersionNum)
                    .HasMaxLength(32)
                    .HasColumnName("version_num");
            });

            modelBuilder.Entity<Arbitrage>(entity =>
            {
                entity.ToTable("arbitrages");

                entity.Property(e => e.Id)
                    .HasMaxLength(256)
                    .HasColumnName("id");

                entity.Property(e => e.AccountAddress)
                    .HasMaxLength(256)
                    .HasColumnName("account_address");

                entity.Property(e => e.BlockNumber).HasColumnName("block_number");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.EndAmount).HasColumnName("end_amount");

                entity.Property(e => e.Error)
                    .HasMaxLength(256)
                    .HasColumnName("error");

                entity.Property(e => e.ProfitAmount).HasColumnName("profit_amount");

                entity.Property(e => e.ProfitTokenAddress)
                    .HasMaxLength(256)
                    .HasColumnName("profit_token_address");

                entity.Property(e => e.StartAmount).HasColumnName("start_amount");

                entity.Property(e => e.TransactionHash)
                    .HasMaxLength(256)
                    .HasColumnName("transaction_hash");
            });

            modelBuilder.Entity<ArbitrageSwap>(entity =>
            {
                entity.HasKey(e => new { e.ArbitrageId, e.SwapTransactionHash, e.SwapTraceAddress })
                    .HasName("arbitrage_swaps_pkey");

                entity.ToTable("arbitrage_swaps");

                entity.HasIndex(e => new { e.SwapTransactionHash, e.SwapTraceAddress }, "arbitrage_swaps_swaps_idx");

                entity.Property(e => e.ArbitrageId)
                    .HasMaxLength(1024)
                    .HasColumnName("arbitrage_id");

                entity.Property(e => e.SwapTransactionHash)
                    .HasMaxLength(66)
                    .HasColumnName("swap_transaction_hash");

                entity.Property(e => e.SwapTraceAddress).HasColumnName("swap_trace_address");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("now()");

                entity.HasOne(d => d.Arbitrage)
                    .WithMany(p => p.ArbitrageSwaps)
                    .HasForeignKey(d => d.ArbitrageId)
                    .HasConstraintName("arbitrage_swaps_arbitrage_id_fkey");
            });

            modelBuilder.Entity<Block>(entity =>
            {
                entity.HasKey(e => e.BlockNumber)
                    .HasName("blocks_pkey");

                entity.ToTable("blocks");

                entity.Property(e => e.BlockNumber).HasColumnName("block_number");

                entity.Property(e => e.BlockTimestamp)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("block_timestamp");
            });

            modelBuilder.Entity<ClassifiedTrace>(entity =>
            {
                entity.HasKey(e => new { e.BlockNumber, e.TransactionHash, e.TraceAddress })
                    .HasName("classified_traces_pkey");

                entity.ToTable("classified_traces");

                entity.Property(e => e.BlockNumber).HasColumnName("block_number");

                entity.Property(e => e.TransactionHash)
                    .HasMaxLength(66)
                    .HasColumnName("transaction_hash");

                entity.Property(e => e.TraceAddress).HasColumnName("trace_address");

                entity.Property(e => e.AbiName)
                    .HasMaxLength(1024)
                    .HasColumnName("abi_name");

                entity.Property(e => e.Classification)
                    .HasMaxLength(256)
                    .HasColumnName("classification");

                entity.Property(e => e.ClassifiedAt)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("classified_at")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Error)
                    .HasMaxLength(256)
                    .HasColumnName("error");

                entity.Property(e => e.FromAddress)
                    .HasMaxLength(256)
                    .HasColumnName("from_address");

                entity.Property(e => e.FunctionName)
                    .HasMaxLength(2048)
                    .HasColumnName("function_name");

                entity.Property(e => e.FunctionSignature)
                    .HasMaxLength(2048)
                    .HasColumnName("function_signature");

                entity.Property(e => e.Gas).HasColumnName("gas");

                entity.Property(e => e.GasUsed).HasColumnName("gas_used");

                entity.Property(e => e.Inputs)
                    .HasColumnType("json")
                    .HasColumnName("inputs");

                entity.Property(e => e.Protocol)
                    .HasMaxLength(256)
                    .HasColumnName("protocol");

                entity.Property(e => e.ToAddress)
                    .HasMaxLength(256)
                    .HasColumnName("to_address");

                entity.Property(e => e.TraceType)
                    .HasMaxLength(256)
                    .HasColumnName("trace_type");

                entity.Property(e => e.TransactionPosition).HasColumnName("transaction_position");

                entity.Property(e => e.Value).HasColumnName("value");
            });

            modelBuilder.Entity<LatestBlockUpdate>(entity =>
            {
                entity.HasKey(e => e.BlockNumber)
                    .HasName("latest_block_update_pkey");

                entity.ToTable("latest_block_update");

                entity.Property(e => e.BlockNumber).HasColumnName("block_number");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("updated_at")
                    .HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<Liquidation>(entity =>
            {
                entity.HasKey(e => new { e.TransactionHash, e.TraceAddress })
                    .HasName("liquidations_pkey");

                entity.ToTable("liquidations");

                entity.Property(e => e.TransactionHash)
                    .HasMaxLength(66)
                    .HasColumnName("transaction_hash");

                entity.Property(e => e.TraceAddress)
                    .HasMaxLength(256)
                    .HasColumnName("trace_address");

                entity.Property(e => e.BlockNumber).HasColumnName("block_number");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.DebtPurchaseAmount).HasColumnName("debt_purchase_amount");

                entity.Property(e => e.DebtTokenAddress)
                    .HasMaxLength(256)
                    .HasColumnName("debt_token_address");

                entity.Property(e => e.Error)
                    .HasMaxLength(256)
                    .HasColumnName("error");

                entity.Property(e => e.LiquidatedUser)
                    .HasMaxLength(256)
                    .HasColumnName("liquidated_user");

                entity.Property(e => e.LiquidatorUser)
                    .HasMaxLength(256)
                    .HasColumnName("liquidator_user");

                entity.Property(e => e.Protocol)
                    .HasMaxLength(256)
                    .HasColumnName("protocol");

                entity.Property(e => e.ReceivedAmount).HasColumnName("received_amount");

                entity.Property(e => e.ReceivedTokenAddress)
                    .HasMaxLength(256)
                    .HasColumnName("received_token_address");
            });

            modelBuilder.Entity<MinerPayment>(entity =>
            {
                entity.HasKey(e => new { e.BlockNumber, e.TransactionHash })
                    .HasName("miner_payments_pkey");

                entity.ToTable("miner_payments");

                entity.Property(e => e.BlockNumber).HasColumnName("block_number");

                entity.Property(e => e.TransactionHash)
                    .HasMaxLength(66)
                    .HasColumnName("transaction_hash");

                entity.Property(e => e.BaseFeePerGas).HasColumnName("base_fee_per_gas");

                entity.Property(e => e.CoinbaseTransfer).HasColumnName("coinbase_transfer");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.GasPrice).HasColumnName("gas_price");

                entity.Property(e => e.GasPriceWithCoinbaseTransfer).HasColumnName("gas_price_with_coinbase_transfer");

                entity.Property(e => e.GasUsed).HasColumnName("gas_used");

                entity.Property(e => e.MinerAddress)
                    .HasMaxLength(256)
                    .HasColumnName("miner_address");

                entity.Property(e => e.TransactionFromAddress)
                    .HasMaxLength(256)
                    .HasColumnName("transaction_from_address");

                entity.Property(e => e.TransactionIndex).HasColumnName("transaction_index");

                entity.Property(e => e.TransactionToAddress)
                    .HasMaxLength(256)
                    .HasColumnName("transaction_to_address");
            });

            modelBuilder.Entity<NftTrade>(entity =>
            {
                entity.HasKey(e => new { e.TransactionHash, e.TraceAddress })
                    .HasName("nft_trades_pkey");

                entity.ToTable("nft_trades");

                entity.Property(e => e.TransactionHash)
                    .HasMaxLength(66)
                    .HasColumnName("transaction_hash");

                entity.Property(e => e.TraceAddress)
                    .HasMaxLength(256)
                    .HasColumnName("trace_address");

                entity.Property(e => e.AbiName)
                    .HasMaxLength(1024)
                    .HasColumnName("abi_name");

                entity.Property(e => e.BlockNumber).HasColumnName("block_number");

                entity.Property(e => e.BuyerAddress)
                    .HasMaxLength(256)
                    .HasColumnName("buyer_address");

                entity.Property(e => e.CollectionAddress)
                    .HasMaxLength(256)
                    .HasColumnName("collection_address");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Error)
                    .HasMaxLength(256)
                    .HasColumnName("error");

                entity.Property(e => e.PaymentAmount).HasColumnName("payment_amount");

                entity.Property(e => e.PaymentTokenAddress)
                    .HasMaxLength(256)
                    .HasColumnName("payment_token_address");

                entity.Property(e => e.Protocol)
                    .HasMaxLength(256)
                    .HasColumnName("protocol");

                entity.Property(e => e.SellerAddress)
                    .HasMaxLength(256)
                    .HasColumnName("seller_address");

                entity.Property(e => e.TokenId).HasColumnName("token_id");

                entity.Property(e => e.TransactionPosition).HasColumnName("transaction_position");
            });

            modelBuilder.Entity<Price>(entity =>
            {
                entity.HasKey(e => new { e.TokenAddress, e.Timestamp })
                    .HasName("prices_pkey");

                entity.ToTable("prices");

                entity.Property(e => e.TokenAddress)
                    .HasMaxLength(256)
                    .HasColumnName("token_address");

                entity.Property(e => e.Timestamp)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("timestamp");

                entity.Property(e => e.UsdPrice).HasColumnName("usd_price");
            });

            modelBuilder.Entity<PunkBid>(entity =>
            {
                entity.HasKey(e => new { e.BlockNumber, e.TransactionHash, e.TraceAddress })
                    .HasName("punk_bids_pkey");

                entity.ToTable("punk_bids");

                entity.Property(e => e.BlockNumber).HasColumnName("block_number");

                entity.Property(e => e.TransactionHash)
                    .HasMaxLength(66)
                    .HasColumnName("transaction_hash");

                entity.Property(e => e.TraceAddress)
                    .HasMaxLength(256)
                    .HasColumnName("trace_address");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.FromAddress)
                    .HasMaxLength(256)
                    .HasColumnName("from_address");

                entity.Property(e => e.Price).HasColumnName("price");

                entity.Property(e => e.PunkIndex).HasColumnName("punk_index");
            });

            modelBuilder.Entity<PunkBidAcceptance>(entity =>
            {
                entity.HasKey(e => new { e.BlockNumber, e.TransactionHash, e.TraceAddress })
                    .HasName("punk_bid_acceptances_pkey");

                entity.ToTable("punk_bid_acceptances");

                entity.Property(e => e.BlockNumber).HasColumnName("block_number");

                entity.Property(e => e.TransactionHash)
                    .HasMaxLength(66)
                    .HasColumnName("transaction_hash");

                entity.Property(e => e.TraceAddress)
                    .HasMaxLength(256)
                    .HasColumnName("trace_address");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.FromAddress)
                    .HasMaxLength(256)
                    .HasColumnName("from_address");

                entity.Property(e => e.MinPrice).HasColumnName("min_price");

                entity.Property(e => e.PunkIndex).HasColumnName("punk_index");
            });

            modelBuilder.Entity<PunkSnipe>(entity =>
            {
                entity.HasKey(e => new { e.BlockNumber, e.TransactionHash, e.TraceAddress })
                    .HasName("punk_snipes_pkey");

                entity.ToTable("punk_snipes");

                entity.Property(e => e.BlockNumber).HasColumnName("block_number");

                entity.Property(e => e.TransactionHash)
                    .HasMaxLength(66)
                    .HasColumnName("transaction_hash");

                entity.Property(e => e.TraceAddress)
                    .HasMaxLength(256)
                    .HasColumnName("trace_address");

                entity.Property(e => e.AcceptancePrice).HasColumnName("acceptance_price");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.FromAddress)
                    .HasMaxLength(256)
                    .HasColumnName("from_address");

                entity.Property(e => e.MinAcceptancePrice).HasColumnName("min_acceptance_price");

                entity.Property(e => e.PunkIndex).HasColumnName("punk_index");
            });

            modelBuilder.Entity<Sandwich>(entity =>
            {
                entity.ToTable("sandwiches");

                entity.HasIndex(e => new { e.BlockNumber, e.BackrunSwapTransactionHash, e.BackrunSwapTraceAddress }, "ik_sandwiches_backrun");

                entity.HasIndex(e => new { e.BlockNumber, e.FrontrunSwapTransactionHash, e.FrontrunSwapTraceAddress }, "ik_sandwiches_frontrun");

                entity.Property(e => e.Id)
                    .HasMaxLength(256)
                    .HasColumnName("id");

                entity.Property(e => e.BackrunSwapTraceAddress).HasColumnName("backrun_swap_trace_address");

                entity.Property(e => e.BackrunSwapTransactionHash)
                    .HasMaxLength(256)
                    .HasColumnName("backrun_swap_transaction_hash");

                entity.Property(e => e.BlockNumber).HasColumnName("block_number");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.FrontrunSwapTraceAddress).HasColumnName("frontrun_swap_trace_address");

                entity.Property(e => e.FrontrunSwapTransactionHash)
                    .HasMaxLength(256)
                    .HasColumnName("frontrun_swap_transaction_hash");

                entity.Property(e => e.SandwicherAddress)
                    .HasMaxLength(256)
                    .HasColumnName("sandwicher_address");
            });

            modelBuilder.Entity<SandwichedSwap>(entity =>
            {
                entity.HasKey(e => new { e.SandwichId, e.BlockNumber, e.TransactionHash, e.TraceAddress })
                    .HasName("sandwiched_swaps_pkey");

                entity.ToTable("sandwiched_swaps");

                entity.HasIndex(e => new { e.BlockNumber, e.TransactionHash, e.TraceAddress }, "ik_sandwiched_swaps_secondary");

                entity.Property(e => e.SandwichId)
                    .HasMaxLength(1024)
                    .HasColumnName("sandwich_id");

                entity.Property(e => e.BlockNumber).HasColumnName("block_number");

                entity.Property(e => e.TransactionHash)
                    .HasMaxLength(66)
                    .HasColumnName("transaction_hash");

                entity.Property(e => e.TraceAddress).HasColumnName("trace_address");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("now()");

                entity.HasOne(d => d.Sandwich)
                    .WithMany(p => p.SandwichedSwaps)
                    .HasForeignKey(d => d.SandwichId)
                    .HasConstraintName("sandwiched_swaps_sandwich_id_fkey");
            });

            modelBuilder.Entity<Swap>(entity =>
            {
                entity.HasKey(e => new { e.BlockNumber, e.TransactionHash, e.TraceAddress })
                    .HasName("swaps_pkey");

                entity.ToTable("swaps");

                entity.Property(e => e.BlockNumber).HasColumnName("block_number");

                entity.Property(e => e.TransactionHash)
                    .HasMaxLength(66)
                    .HasColumnName("transaction_hash");

                entity.Property(e => e.TraceAddress).HasColumnName("trace_address");

                entity.Property(e => e.AbiName)
                    .HasMaxLength(1024)
                    .HasColumnName("abi_name");

                entity.Property(e => e.ContractAddress)
                    .HasMaxLength(256)
                    .HasColumnName("contract_address");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Error)
                    .HasMaxLength(256)
                    .HasColumnName("error");

                entity.Property(e => e.FromAddress)
                    .HasMaxLength(256)
                    .HasColumnName("from_address");

                entity.Property(e => e.Protocol)
                    .HasMaxLength(256)
                    .HasColumnName("protocol");

                entity.Property(e => e.ToAddress)
                    .HasMaxLength(256)
                    .HasColumnName("to_address");

                entity.Property(e => e.TokenInAddress)
                    .HasMaxLength(256)
                    .HasColumnName("token_in_address");

                entity.Property(e => e.TokenInAmount).HasColumnName("token_in_amount");

                entity.Property(e => e.TokenOutAddress)
                    .HasMaxLength(256)
                    .HasColumnName("token_out_address");

                entity.Property(e => e.TokenOutAmount).HasColumnName("token_out_amount");

                entity.Property(e => e.TransactionPosition).HasColumnName("transaction_position");
            });

            modelBuilder.Entity<Token>(entity =>
            {
                entity.HasKey(e => e.TokenAddress)
                    .HasName("tokens_pkey");

                entity.ToTable("tokens");

                entity.Property(e => e.TokenAddress)
                    .HasMaxLength(256)
                    .HasColumnName("token_address");

                entity.Property(e => e.Decimals).HasColumnName("decimals");
            });

            modelBuilder.Entity<Transfer>(entity =>
            {
                entity.HasKey(e => new { e.BlockNumber, e.TransactionHash, e.TraceAddress })
                    .HasName("transfers_pkey");

                entity.ToTable("transfers");

                entity.Property(e => e.BlockNumber).HasColumnName("block_number");

                entity.Property(e => e.TransactionHash)
                    .HasMaxLength(66)
                    .HasColumnName("transaction_hash");

                entity.Property(e => e.TraceAddress).HasColumnName("trace_address");

                entity.Property(e => e.Amount).HasColumnName("amount");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.Error)
                    .HasMaxLength(256)
                    .HasColumnName("error");

                entity.Property(e => e.FromAddress)
                    .HasMaxLength(256)
                    .HasColumnName("from_address");

                entity.Property(e => e.Protocol)
                    .HasMaxLength(256)
                    .HasColumnName("protocol");

                entity.Property(e => e.ToAddress)
                    .HasMaxLength(256)
                    .HasColumnName("to_address");

                entity.Property(e => e.TokenAddress)
                    .HasMaxLength(256)
                    .HasColumnName("token_address");
            });

            modelBuilder.Entity<ZmLatestBlockUpdate>(entity =>
            {
                entity.HasKey(e => e.BlockNumber)
                    .HasName("zm_latest_block_update_pkey");

                entity.ToTable("zm_latest_block_update");

                entity.Property(e => e.BlockNumber).HasColumnName("block_number");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("updated_at")
                    .HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<ZmTime>(entity =>
            {
                entity.HasKey(e => new { e.TransactionHash, e.BlockNumber, e.TransactionPosition })
                    .HasName("zm_time_pkey");

                entity.ToTable("zm_time");

                entity.HasIndex(e => e.ArrivalTime, "zm_arrival_time_idx");

                entity.Property(e => e.TransactionHash)
                    .HasMaxLength(66)
                    .HasColumnName("transaction_hash");

                entity.Property(e => e.BlockNumber).HasColumnName("block_number");

                entity.Property(e => e.TransactionPosition).HasColumnName("transaction_position");

                entity.Property(e => e.ArrivalTime)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("arrival_time");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}