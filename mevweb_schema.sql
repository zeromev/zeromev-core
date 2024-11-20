CREATE DATABASE mevweb;

\c mevweb

CREATE TABLE public.latest_mev_block (
    block_number bigint NOT NULL,
    updated_at timestamp without time zone DEFAULT now()
);

CREATE TABLE public.mev_block (
    block_number bigint NOT NULL,
    mev_data bytea
);

ALTER TABLE ONLY public.latest_mev_block
    ADD CONSTRAINT latest_mev_block_pkey PRIMARY KEY (block_number);

ALTER TABLE ONLY public.mev_block
    ADD CONSTRAINT mev_block_new_pkey PRIMARY KEY (block_number);