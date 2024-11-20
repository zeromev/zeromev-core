\c mev_inspect

insert into zm_tokens select distinct token_in_address from swaps on conflict(address) do nothing;
insert into zm_tokens select distinct token_out_address from swaps on conflict(address) do nothing;
insert into zm_tokens select distinct received_token_address from liquidations on conflict(address) do nothing;
insert into zm_tokens select distinct debt_token_address from liquidations on conflict(address) do nothing;
insert into zm_tokens select distinct payment_token_address from nft_trades on conflict(address) do nothing;