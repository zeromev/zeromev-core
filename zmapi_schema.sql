CREATE DATABASE zmapi;

\c zmapi

CREATE FUNCTION public.mev_transaction_summary(address_from character varying) RETURNS TABLE(mev_type character varying, sum_user_loss_usd numeric, sum_user_swap_count integer, user_swap_volume_usd numeric, user_swap_count integer)
    LANGUAGE sql
    AS $$
	SELECT mev_type,
		sum(user_loss_usd) as sum_user_loss_usd,
		sum(user_swap_count) as sum_user_swap_count, 
		sum(user_swap_volume_usd) as user_swap_volume_usd, 
		sum(user_swap_count) as user_swap_count
	FROM public.v_zm_mev_transaction
	WHERE public.v_zm_mev_transaction.address_from = mev_transaction_summary.address_from
	GROUP BY mev_type;
$$;

CREATE TABLE public.zm_mev_transaction (
    block_number numeric NOT NULL,
    tx_index smallint NOT NULL,
    mev_type smallint NOT NULL,
    protocol character varying(16),
    user_loss_usd numeric,
    extractor_profit_usd numeric,
    swap_volume_usd numeric,
    swap_count smallint,
    extractor_swap_volume_usd numeric,
    extractor_swap_count smallint,
    imbalance real,
    address_from character varying(256),
    address_to character varying(256),
    arrival_time_us timestamp with time zone,
    arrival_time_eu timestamp with time zone,
    arrival_time_as timestamp with time zone
);

CREATE TABLE public.zm_mev_type (
    index smallint NOT NULL,
    name character varying(16)
);

COPY public.zm_mev_type (index, name) FROM stdin;
0	none
1	swap
2	swaps
3	frontrun
4	sandwich
5	backrun
6	arb
7	liquid
8	nft
9	punk_bid
10	punk_accept
11	punk_snipe
\.

CREATE VIEW public.v_zm_mev_transaction AS
 SELECT zm_mev_transaction.block_number,
    zm_mev_transaction.tx_index,
    zm_mev_type.name AS mev_type,
    zm_mev_transaction.protocol,
        CASE
            WHEN (zm_mev_transaction.mev_type = 5) THEN NULL::numeric
            ELSE zm_mev_transaction.user_loss_usd
        END AS user_loss_usd,
        CASE
            WHEN (zm_mev_transaction.mev_type = 5) THEN NULL::numeric
            ELSE zm_mev_transaction.extractor_profit_usd
        END AS extractor_profit_usd,
        CASE
            WHEN (zm_mev_transaction.extractor_swap_count = zm_mev_transaction.swap_count) THEN NULL::numeric
            WHEN (zm_mev_transaction.extractor_swap_volume_usd IS NULL) THEN zm_mev_transaction.swap_volume_usd
            WHEN (zm_mev_transaction.swap_volume_usd IS NULL) THEN NULL::numeric
            ELSE (zm_mev_transaction.swap_volume_usd - zm_mev_transaction.extractor_swap_volume_usd)
        END AS user_swap_volume_usd,
        CASE
            WHEN (zm_mev_transaction.extractor_swap_count = zm_mev_transaction.swap_count) THEN NULL::numeric
            WHEN (zm_mev_transaction.extractor_swap_count IS NULL) THEN (zm_mev_transaction.swap_count)::numeric
            WHEN (zm_mev_transaction.swap_count IS NULL) THEN NULL::numeric
            ELSE ((zm_mev_transaction.swap_count - zm_mev_transaction.extractor_swap_count))::numeric
        END AS user_swap_count,
    zm_mev_transaction.extractor_swap_volume_usd,
    zm_mev_transaction.extractor_swap_count,
    zm_mev_transaction.imbalance,
    zm_mev_transaction.address_from,
    zm_mev_transaction.address_to,
    zm_mev_transaction.arrival_time_us,
    zm_mev_transaction.arrival_time_eu,
    zm_mev_transaction.arrival_time_as
   FROM public.zm_mev_transaction,
    public.zm_mev_type
  WHERE (zm_mev_transaction.mev_type = zm_mev_type.index)
  ORDER BY zm_mev_transaction.block_number, zm_mev_transaction.tx_index;

CREATE VIEW public.v_zm_mev_transaction_summary AS
 SELECT v_zm_mev_transaction.address_from,
    v_zm_mev_transaction.mev_type,
    sum(v_zm_mev_transaction.user_loss_usd) AS sum_user_loss_usd,
    sum(v_zm_mev_transaction.user_swap_volume_usd) AS sum_user_swap_volume_usd,
    sum(v_zm_mev_transaction.user_swap_count) AS sum_user_swap_count,
    sum(v_zm_mev_transaction.extractor_profit_usd) AS sum_extractor_profit_usd,
    sum(v_zm_mev_transaction.extractor_swap_volume_usd) AS sum_extractor_swap_volume_usd,
    sum(v_zm_mev_transaction.extractor_swap_count) AS sum_extractor_swap_count
   FROM public.v_zm_mev_transaction
  GROUP BY v_zm_mev_transaction.address_from, v_zm_mev_transaction.mev_type;

CREATE VIEW public.v_zm_mev_transaction_summary_to AS
 SELECT v_zm_mev_transaction.address_to,
    v_zm_mev_transaction.mev_type,
    sum(v_zm_mev_transaction.user_loss_usd) AS sum_user_loss_usd,
    sum(v_zm_mev_transaction.user_swap_volume_usd) AS sum_user_swap_volume_usd,
    sum(v_zm_mev_transaction.user_swap_count) AS sum_user_swap_count,
    sum(v_zm_mev_transaction.extractor_profit_usd) AS sum_extractor_profit_usd,
    sum(v_zm_mev_transaction.extractor_swap_volume_usd) AS sum_extractor_swap_volume_usd,
    sum(v_zm_mev_transaction.extractor_swap_count) AS sum_extractor_swap_count
   FROM public.v_zm_mev_transaction
  GROUP BY v_zm_mev_transaction.address_to, v_zm_mev_transaction.mev_type;

ALTER TABLE ONLY public.zm_mev_type
    ADD CONSTRAINT mev_type_pkey_1 PRIMARY KEY (index);

ALTER TABLE ONLY public.zm_mev_transaction
    ADD CONSTRAINT zm_mev_transaction_pkey PRIMARY KEY (block_number, tx_index, mev_type);

CREATE INDEX idx_address_from ON public.zm_mev_transaction USING btree (address_from);

CREATE INDEX idx_address_to ON public.zm_mev_transaction USING btree (address_to);

CREATE INDEX idx_protocol ON public.zm_mev_transaction USING btree (protocol);

REVOKE USAGE ON SCHEMA public FROM PUBLIC;
GRANT ALL ON SCHEMA public TO PUBLIC;
GRANT USAGE ON SCHEMA public TO web_anon;
GRANT SELECT ON TABLE public.zm_mev_transaction TO web_anon;
GRANT SELECT ON TABLE public.v_zm_mev_transaction TO web_anon;
GRANT SELECT ON TABLE public.v_zm_mev_transaction_summary TO web_anon;
GRANT SELECT ON TABLE public.v_zm_mev_transaction_summary_to TO web_anon;