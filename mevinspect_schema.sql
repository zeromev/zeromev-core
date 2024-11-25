\c mev_inspect

CREATE TABLE public.mev_block
(
    block_number bigint NOT NULL,
    mev_data bytea,
    CONSTRAINT mev_block_new_pkey PRIMARY KEY (block_number)
);

CREATE TABLE public.latest_mev_block
(
    block_number bigint NOT NULL,
    updated_at timestamp without time zone DEFAULT now(),
    CONSTRAINT latest_mev_block_pkey PRIMARY KEY (block_number)
);

CREATE TABLE public.zm_blocks
(
    block_number numeric NOT NULL,
    transaction_count integer NOT NULL,
    block_time timestamp without time zone,
    tx_data bytea,
    tx_status bit varying,
    tx_addresses bytea,
    CONSTRAINT zm_blocks_import_pkey PRIMARY KEY (block_number)
);

CREATE TABLE public.zm_latest_block_update
(
    block_number numeric NOT NULL,
    updated_at timestamp without time zone DEFAULT now(),
    CONSTRAINT zm_latest_block_update_pkey PRIMARY KEY (block_number)
);

CREATE TABLE public.zm_mev_summary
(
    block_number numeric NOT NULL,
    mev_type integer NOT NULL,
    mev_amount_usd numeric,
    mev_count integer,
    CONSTRAINT zm_mev_summary_pkey PRIMARY KEY (block_number, mev_type)
);

CREATE TABLE public.zm_tokens
(
    address character varying(256) COLLATE pg_catalog."default" NOT NULL,
    name character varying(256) COLLATE pg_catalog."default",
    decimals integer,
    symbol character varying(256) COLLATE pg_catalog."default",
    owner character varying(256) COLLATE pg_catalog."default",
    image character varying(256) COLLATE pg_catalog."default",
    website character varying(256) COLLATE pg_catalog."default",
    facebook character varying(256) COLLATE pg_catalog."default",
    telegram character varying(256) COLLATE pg_catalog."default",
    twitter character varying(256) COLLATE pg_catalog."default",
    reddit character varying(256) COLLATE pg_catalog."default",
    coingecko character varying(256) COLLATE pg_catalog."default",
    CONSTRAINT zm_tokens_pkey PRIMARY KEY (address)
);

COPY public.zm_latest_block_update (block_number, updated_at) FROM stdin;
0	1990-01-01 00:00:00
\.