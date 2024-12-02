# zeromev

This guide describes how to install and run all components of the [zeromev](https://info.zeromev.org) frontrunning explorer and system, including the extractor, classifier, web server, web client and API.

# overview

The core zeromev codebase is written in [.NET 8](https://learn.microsoft.com/en-us/dotnet/) (see ZeroMev.sln) with additional node.js scripts for the Zeromev API, [postgres](https://www.postgresql.org/) for storage and [postgrest](https://docs.postgrest.org/en/v12/) for the API.

To be able to compile and run these projects, follow the [install](#install) section below from beginning to end.

## components

| name       | description                                                                                                                                        | dependencies                                              |
| ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------- |
| extractor  | timestamps blocks and transactions as they arrive at an Ethereum full node. Records them in the extractor db. It is designed to run as a service on a node, to ensure low latency tracking of that node.   | full node, extractor db. |
| classifier | creates and exports MEV summary data for use by both the website and the API. Runs as a service.                                                   | archive node, mev_inspect db, extractor db, mevweb db, zmapi db. |
| client     | zeromev.org website written in .NET 8 Blazor.                                                                                                      | internal REST API (see Server project).                   |
| server     | data source for the website (see Client). REST API for internal use only (api.zeromev.org/zmhealth), not the public Zeromev API (data.zeromev.org) | mevweb db, extractor db                        |

## databases

| db name | description |
| -------- | -------- |
| mev_inspect| the flashbots [mev-inspect-py](https://github.com/flashbots/mev-inspect-py) db, with additional zeromev tables maintained by the classifier. |
| extractor | eth block and transaction timing data populated by one or more nodes/extractor services.|
| mevweb | MEV data source for the website, served by the internal REST API (see Server project).
| zmapi | MEV transaction summary data for the zeromev API.|

# install

For this guide, we will install and run all zeromev components on a single fresh Ubuntu 22.04+ server. 

A [production](#production) setup using multiple servers is described later.

## requirements

Your server will need access to:
- a full archive node with traces (eg: [Erigon](https://erigon.gitbook.io/erigon)) 
- at least one full node (eg: [Geth](https://geth.ethereum.org/), [Infura](https://www.infura.io/)).

You will also need:
- an [Etherscan API](https://docs.etherscan.io/) key
- an [Ethplorer API](https://ethplorer.io/) key

## prepare

Ensure the server is up to date:

```
sudo apt-get update -y && sudo apt dist-upgrade -y
```

Create a new zeromev user and set the password:

```
sudo useradd -m -s /bin/bash zeromev
sudo passwd zeromev
```

Add zeromev to the sudo group:
```
sudo usermod -aG sudo zeromev
```

Exit the current session, and login again as the zeromev user:
```
exit
ssh zeromev@your.server.ip.address
```

Make sure [git is installed](https://github.com/git-guides/install-git), then clone the zeromev respository:

```
git clone https://github.com/zeromev/zeromev-core
```

Install postgres locally:
```
sudo apt install postgresql postgresql-contrib
```

Set a postgres password:

```
sudo -u postgres psql
\password
```

Exit postgres:

```
\q
```

Install .NET:
```
sudo apt-get update
sudo apt-get install -y wget apt-transport-https
```

Add the Microsoft package signing key:
```
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
```

Update the package list and Install .NET SDK 8.0:
```
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

## create databases

Enter the main zeromev git folder:

```
cd ~/zeromev-core/
```

Create the extractor database to hold transaction and block timing data:
```
sudo -u postgres psql -f extractor_schema.sql
```

Create a mevweb database to hold cached MEV data for the website:
```
sudo -u postgres psql -f mevweb_schema.sql
```

Create the zmapi database to hold MEV transaction summary data for the zeromev API. This also creates a web_anon role to allow the public website to connect to it:
```
sudo -u postgres psql -f zmapi_schema.sql
```

Then create the mev-inspect db and add zeromev specific tables:
```
sudo -u postgres psql -f mevinspect_schema.sql
```

## configure

Configuration files are needed by each component in order to run. 

Let's configure a template settings file, which we can then copy to each component as we go (as an appsettings.json file).

```
nano appsettings.template.json
```

Update these settings with your ethereum full node RPC and websocket addresses (you can use Infura endpoints for the purposes of this guide):

```
    "EthereumRPC": "http://your_ethereum_full_node_rpc_url",
    "EthereumWSS": "ws://your_ethereum_full_node_web_socket_url",
```

Set your [Etherscan API key](https://docs.etherscan.io/) here:

```
    "EtherscanAPIKey": "your_etherscan_api_key",
```

[Ethplorer](https://ethplorer.io/) is used by the classifier to update swap token details. Set your [Ethplorer API key](https://ethplorer.io/) here:

```
    "EthplorerAPIKey": "your_ethplorer_api_key",
```

Configure the postgres password for each of the databases you just created, so the zeromev components can connect to them:

```
    "DB": "Host=localhost;Database=extractor;Username=postgres;Password=your_postgres_password;Timeout=5;Command Timeout=7",
    "MevWebDB": "Host=localhost;Database=mevweb;Username=postgres;Password=your_postgres_password;Timeout=5;Command Timeout=5",
    "MevApiDB": "Host=localhost;Database=zmapi;Username=postgres;Password=your_postgres_password;Timeout=600;Command Timeout=600",
    "MevDB": "Host=localhost;Database=mev_inspect;Username=postgres;Password=your_postgres_password;Timeout=600;Command Timeout=600",    
```

Exit and save.

The web client is a bit different and has hardcoded settings. This speeds things up, because it doesn't require configuration libraries to be downloaded to the user's browser.

So to set web client defaults, enter the code directory for the shared library.

```
cd ~/zeromev-core/ZeroMev/Shared
```

Then create a new config code file from the template, and edit:

```
cp ../../Config.template.cs Config.cs
nano Config.cs
```

Update these default settings required by the web client:

```
        public string EthereumRPC { get; set; } = "your_ethereum_full_node_rpc_url";
        public string EthplorerAPIKey { get; set; } = "your_ethereum_full_node_web_socket_url";
        public string EtherscanAPIKey { get; set; } = "your_etherscan_api_key";
```

Exit and save.

## install extractor

Enter the code directory for the extractor service.

```
cd ~/zeromev-core/ZeroMev/ExtractorService
```

Create a new settings file from our template, and edit:

```
cp ../../appsettings.template.json appsettings.json
nano appsettings.json
```

Set the ExtractorIndex to the location that best describes your node:

| ExtractorIndex | Location |
| -------- | -------- |
|0|	Infura|
|1|	QuickNodes|
|2|	US (Central)|
|3|	EU (Germany)|
|4|	Asia (Singapore)|

For example, if your node is located in the USA:

```
    "ExtractorIndex": 2,
```

Exit and save. Then build the project (required after any appsettings change made here):

```
dotnet build ZeroMev.ExtractorService.csproj -c Release
```

Create a service file:

```
sudo nano /etc/systemd/system/extractor.service
```

Paste the following into the file to create the extractor service:

```
[Unit]
Description=zeromev extractor service
After=network.target postgresql.service
Requires=postgresql.service

[Service]
WorkingDirectory=/home/zeromev/zeromev/ZeroMev/ExtractorService/bin/Release/net8.0
ExecStart=/usr/bin/dotnet /home/zeromev/zeromev/ZeroMev/ExtractorService/bin/Release/net8.0/ZeroMev.ExtractorService.dll
Restart=on-failure
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=extractor
User=zeromev

[Install]
WantedBy=multi-user.target
```

Save on exit.

Run the following to enable auto-start at boot time:

```
sudo systemctl daemon-reload
sudo systemctl enable extractor
```

Finally, start your extractor service and check it's status:

```
sudo systemctl start extractor
sudo systemctl status extractor
```

If everything is working, you will see new logs for every block as it arrives at the node:

```
Nov 13 14:36:13 zeromev-guide extractor[831718]: extractor_block update 249 rows in 274 ms 0 remaining
Nov 13 14:36:18 zeromev-guide extractor[831718]: flashbots_block update in 18 ms
Nov 13 14:36:25 zeromev-guide extractor[831718]: ZeroMev.ExtractorService.Worker[0] new block 21179408 size 176 arrivals 1120 pending 695
Nov 13 14:36:25 zeromev-guide extractor[831718]: extractor_block update 176 rows in 5 ms 0 remaining
Nov 13 14:36:30 zeromev-guide extractor[831718]: flashbots_block update in 11 ms
Nov 13 14:36:36 zeromev-guide extractor[831718]: ZeroMev.ExtractorService.Worker[0] new block 21179409 size 122 arrivals 1516 pending 969
Nov 13 14:36:36 zeromev-guide extractor[831718]: extractor_block update 122 rows in 3 ms 0 remaining
Nov 13 14:36:41 zeromev-guide extractor[831718]: flashbots_block update in 13 ms
Nov 13 14:36:49 zeromev-guide extractor[831718]: ZeroMev.ExtractorService.Worker[0] new block 21179410 size 196 arrivals 1940 pending 1197
Nov 13 14:36:49 zeromev-guide extractor[831718]: extractor_block update 196 rows in 12 ms 0 remaining
```

To check the extractor database is being updated:
```
sudo -u postgres psql -d extractor -c "select count(*) extracted_blocks from extractor_block;"
```

You should see something like this:

```
 extracted_blocks
------------------
               59
(1 row)
```

Leave the extractor service collecting data while we move on to setting up the MEV extraction components.

### explanation

Each node you setup requires access to the same extractor database instance. For example, [zeromev.org](zeromev.org) has three Geth nodes in different geographical locations all writing to one extractor database, but with different `ExtractorIndex` settings to distinguish them. 

These nodes run their own extractor service locally to ensure the accurate timestamping of blocks and transactions with minimal latency.

Additionally, [zeromev.org](zeromev.org) maintains an [Infura](https://www.infura.io/) node.

### other settings

Set the frequency with which pending transactions that haven't made it into a block are released from memory (to avoid a memory leak). 14 days is a sensible default.

```
    "PurgeAfterDays": 14,
```

Configure whether to collect data from the [Flashbots Block API](https://blocks.flashbots.net/). This API has now been decomissioned by Flashbots, so is generally set false.

```
    "DoExtractFlashbots": false,
```
    
## install mev-inspect-py

Flashbots [mev-inspect-py](https://github.com/flashbots/mev-inspect-py) is used to extract swap and MEV data.

We have released a [fork](https://github.com/pmcgoohan/mev-inspect-py) which allows it to access an external postgres database, as well as fixing some versioning problems, so let's use this.

### allow access to db

First we need to allow connectivity to postgres from mev-inspect-py:

Find the postgres configuration directory:

```
sudo -u postgres psql -c 'show hba_file'
```

Enter this directory, and edit `pg_hba.conf` adding this to the end and updating `your_server_ip_address`:

```
host    all             all             your_server_ip_address/32            md5
host    all             all             172.18.0.0/24                md5
```

Save, then edit `postgresql.conf` and modify the listen_addresses line:

```
listen_addresses = '*' # find listen_address in file and replace localhost with *
```

Save, then restart postgres:

```
sudo systemctl restart postgresql
```

### install

Exit the zeromev user session and install mev-inspect-py and it's components as **root user** to avoid permissioning problems.

Follow the mev-inspect-py [install section](https://github.com/pmcgoohan/mev-inspect-py/blob/main/README.md#install) in the fork to install and run it in a local instance of kubernetes (https://github.com/pmcgoohan/mev-inspect-py).

Ensure your postgres password is set correctly to the postgres instance in your environment variables:

```
export POSTGRES_USER=postgres
export POSTGRES_PASSWORD=your_postgres_password
export POSTGRES_HOST=your_server_ip_address
```

Once you have run `tilt up`, ensure you populate the mev_inspect db, as described at the end of this section:

```
./mev exec alembic upgrade head
```

### run

Then start mev-inspect extracting data at the latest block:

```
./mev listener start
```

Check it's running:

```
./mev listener tail
```

You'll see something like this if it is:
```
INFO:mev_inspect.inspect_block:Block: 21288484 -- Total traces: 867
INFO:mev_inspect.inspect_block:Block: 21288484 -- Total transactions: 172
INFO:mev_inspect.inspect_block:Block: 21288484 -- Returned 866 classified traces
INFO:mev_inspect.inspect_block:Block: 21288484 -- Found 304 transfers
INFO:mev_inspect.inspect_block:Block: 21288484 -- Found 35 swaps
```

Now we have the extractor service and mev-inspect-py running, we can move onto the classifier which uses data from both.

### backfill

The backfill command allows you to extract historical MEV data:

```
./mev backfill from_block_number to_block_number
```

Although not vital for this guide, it's a good idea to backfill at least 2 weeks of data in a production system. This allows the classifier to build up a view of token exchange rates and symbol data.

## install classifier

The classifier takes data from a variety of sources, and creates and exports MEV summary data for use by both the website and the [zeromev API](https://data.zeromev.org/docs/).

### configure the classifier

Enter the zeromev user session.

Enter the code directory for the classifier:

```
cd ~/zeromev-core/ZeroMev/ClassifierService
```

Create a new settings file from our settings template, and edit it:

```
cp ../../appsettings.template.json appsettings.json
nano appsettings.json
```

Modify the classifier settings to connect to the Ethereum archive node, rather than the full node:

```
    "EthereumRPC": "your_archive_node_url",
```

### import swap tokens

If your mev-inspect database is large, it's best to bulk import the swap token details that will be displayed in the website before running the service.

To do this, first run a script to create empty token rows in the database, ready to be populated:

```
sudo psql -U postgres -h localhost -f before_import_all_tokens.sql
```

Then build the classifier:

```
dotnet build ZeroMev.ClassifierService.csproj -c Release
```

Make sure your Ethplorer API key is configured and valid, and run the classifier from the command line in import mode:

```
dotnet ~/zeromev-core/ZeroMev/ClassifierService/bin/Release/net8.0/ZeroMev.ClassifierService.dll import_tokens
```

This will take a while, especially if the database is large.

Note that this import will only impact MEV data processed by the classifier from now on. This is why we do it before getting the classifier to process MEV data for the first time, especially if you have backfilled a lot of historical data in mev-inspect-py.

Once the classifier is running, new tokens will be added automatically.

### preparation

Before running the classifier for the first time, we must tell it which block to start from.

The earliest this can be is the earliest block that mev_inspect has extracted.

So first let's find this earliest MEV block in the mev_inspect database:

```
sudo psql -U postgres -h localhost -d mev_inspect -c "select min(block_number) first_block from blocks;"
```

If mev-inspect-py has been running successfully, you should get an output like this giving you the first mev-inspect-py block:

```
 first_block
-------------
    21226858
(1 row)
```

Set the first zeromev classifier block to this by changing `first_block` to the result you got above:

```
sudo psql -U postgres -h localhost -d mev_inspect -c "UPDATE public.zm_latest_block_update SET block_number = first_block, updated_at = now();"
```


### create service

Build the project (required after any appsettings change in this directory):

```
dotnet build ZeroMev.ClassifierService.csproj -c Release
```

Create a service file:

```
sudo nano /etc/systemd/system/classifier.service
```

Paste the following into the file:

```
[Unit]
Description=zeromev classifier service
After=network.target postgresql.service
Requires=postgresql.service

[Service]
WorkingDirectory=/home/zeromev/zeromev/ZeroMev/ClassifierService/bin/Release/net8.0
ExecStart=/usr/bin/dotnet /home/zeromev/zeromev/ZeroMev/ClassifierService/bin/Release/net8.0/ZeroMev.ClassifierService.dll
Restart=on-failure
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=classifier
User=zeromev

[Install]
WantedBy=multi-user.target
```

Save on exit.

### start the service

Run the following to enable auto-start at boot time:

```
sudo systemctl daemon-reload
sudo systemctl enable classifier
```

Finally, start your classifier service:

```
sudo systemctl start classifier
```

And follow it's logs:

```
journalctl -u classifier -f
```

The classifier warms up by building a cache of swap rates (if sufficient mev-inspect data is available). How far back it caches before the current block is set by `BlockBufferSize` in appsettings.config.

This example shows the system that last classified block 21268860 warming up it's cache to that point, before backfilling data to the latest mev-inspect block 21269169.

```
classifier[1019381]: info: ZeroMev.ClassifierService.Classifier[0]
classifier[1019381]:       21261660 to 21263660 (warmup to 21268860 0.000%, backfill to 21269169 0.000%)
```

Once it's caught up, it starts processing new blocks as they arrive:

```
classifier[874644]: info: ZeroMev.ClassifierService.Classifier[0]
classifier[874644]:       waiting 5 secs for mev-inspect 21268850
classifier[874644]: mev blocks update in 9 ms
classifier[874644]: mev blocks update in 55 ms
classifier[874644]: api mev txs update in 70 ms
classifier[874644]: info: ZeroMev.ClassifierService.Classifier[0]
classifier[874644]:       processed 21268850
classifier[874644]: info: ZeroMev.ClassifierService.Classifier[0]
classifier[874644]:       try get new tokens
classifier[874644]: info: ZeroMev.ClassifierService.Classifier[0]
classifier[874644]:       got new tokens
```

Exit the log with CTRL+C.

## install web site

To install the [zeromev.org](https://zeromev.org) [frontrunning explorer](https://youtu.be/zkrrZZj_Oic?si=_CF3hX_Kjk8i5ZIg) locally, enter the web server code directory:

```
cd ~/zeromev-core/ZeroMev/Server
```

Create a new settings file from the settings template:

```
cp ../../appsettings.template.json appsettings.json
```

Build and launch the server locally on port 5000:

```
dotnet run --project ZeroMev.Server.csproj --configuration Release --urls "http://localhost:5000"
```

Once started, you'll see something like this:

```
Building...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Development
info: Microsoft.Hosting.Lifetime[0]
      Content root path: /home/zeromev/zeromev/ZeroMev/Server
```

To test the site:

If you are in an Desktop Environment, open a web browser and navigate to: 
```
http://localhost:5000
```

Alternatively, you can test from a remote browser:

Self-sign a development HTTPS certificate:

```
dotnet dev-certs https --trust
```

To run the site on all available IP addresses on port 5001 (or change 0.0.0.0 to a specific ip address):

```
dotnet run --project ZeroMev.Server.csproj --configuration Release --urls "https://0.0.0.0:5001"
```

## install API

Install the Zeromev API with the **root** user.

Clone this repository, and follow the instructions in the readme:

```
https://github.com/zeromev/zeromev-api
```

## production
In a production system, components will be split logically between servers. An example production setup is:

| Servers | Installed |
| -------- | -------- |
| extractor servers 1...4     | 4 servers, each running a full Ethereum node & extractor. One server for each of: North America, Europe, Asia, Infura|
|mev server |full archive node & classifier & mev-inspect-py|
|db server (+replica)|replicated postgres instance hosting: mev_inspect db, extractor db, api db, web db|
|web server|client, server, zeromev api|

The EthereumRPC/WSS you specify in the website Config.cs here must be accessible to all client browsers, as they will connect to them directly. Infura is a good option for this.

### backfill

Use the backfill command to extract as much data as possible in mev-inspect-py. To classify this and update the databases, stop the classifier and run it after the following appsetting.json changes:

```
    "ImportZmBlocksFrom": from_block_number,
    "ImportZmBlocksTo": to_block_number,
```

If you are confident that no existing data will be overwritten, setting FastImport will speed database inserts by bypassing conflict checking:

```
    "FastImport": true
```

Once completed, reset these lines and restart the classifier.