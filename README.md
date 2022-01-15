# zeromev
extract, transform and explore MEV data

### Set environment variables

#### Windows

```
setx ZM_DB "Host=149.28.227.190;Username=[username];Password=[password];Database=[database];Timeout=3;Command Timeout=3"
setx ZM_WSS "wss://mainnet.infura.io/ws/v3/[key]"
setx ZM_HTTPS "https://mainnet.infura.io/v3/[key]"
```

open another cmd window and test

```
echo %ZM_DB%
echo %ZM_WSS%
echo %ZM_HTTPS%
```
