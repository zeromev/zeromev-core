CREATE DATABASE extractor;

\c extractor

CREATE TABLE public.extractor (
    extractor_index smallint NOT NULL,
    code character varying(5),
    description character varying(255)
);

CREATE TABLE public.extractor_block (
    block_number bigint NOT NULL,
    extractor_index smallint NOT NULL,
    block_time timestamp with time zone NOT NULL,
    extractor_start_time timestamp with time zone NOT NULL,
    arrival_count bigint NOT NULL,
    pending_count integer,
    tx_data bytea
);

CREATE TABLE public.fb_block (
    block_number bigint NOT NULL,
    bundle_data bit varying
);

CREATE TABLE public.latest_mev_block (
    block_number bigint NOT NULL,
    updated_at timestamp without time zone DEFAULT now()
);

CREATE TABLE public.mev_block (
    block_number bigint NOT NULL,
    mev_data bytea
);
ALTER TABLE ONLY public.extractor_block
    ADD CONSTRAINT extractor_block_pkey PRIMARY KEY (block_number, extractor_index, block_time);

ALTER TABLE ONLY public.extractor
    ADD CONSTRAINT extractor_pkey PRIMARY KEY (extractor_index);

ALTER TABLE ONLY public.fb_block
    ADD CONSTRAINT fb_block_new_pkey PRIMARY KEY (block_number);

ALTER TABLE ONLY public.latest_mev_block
    ADD CONSTRAINT latest_mev_block_pkey PRIMARY KEY (block_number);

ALTER TABLE ONLY public.mev_block
    ADD CONSTRAINT mev_block_new_pkey PRIMARY KEY (block_number);

COPY extractor (extractor_index, code, description) FROM stdin;
0	Inf	Infura
1	Qn	QuickNodes
2	US	US (Central)
3	EU	EU (Germany)
4	AS	Asia (Singapore)
\.

COPY public.latest_mev_block (block_number, updated_at) FROM stdin;
0	1990-01-01 00:00:00
\.
