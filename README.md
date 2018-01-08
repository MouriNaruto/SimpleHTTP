# SimpleHTTP

## Introduction

Http and Https client based on sockets, allowing you customize every detail of the HTTP(S) package, including resolve hostname by yourself without changing system hosts or DNS servers.

Under early development, any help will be appreciated.

## Scenes

The hostname "www.pixiv.net" and Pixiv's other hostnames are now being polluted on every single DNS server in China. Without changing system hosts file or proxy it is impossible to establish an Http(s) connection with Pixiv in Chinese mainland.

Since I'm developing/maintaining Pixiv UWP, I need to find a way, trying to access Pixiv's APIs without proxy or changing hosts file (because most of my users have no proxy, and they don't want to or don't know how to change hosts file).

With SimpleHTTP, just construct an Http (or Https) client with both of the hostname and the known IP address:

```csharp
HttpsClient httpsClient = new HttpsClient("oauth.secure.pixiv.net", "210.129.120.41");
```

In this way SimpleHTTP won't try to resolve "oauth.secure.pixiv.net" through DNS or DNS cache, but use IP address "210.129.120.41" to establish Http(s) connections directly.

Of course, you can use it in normal ways:

```csharp
HttpsClient httpsClient = new HttpsClient("www.baidu.com");
```

Then SimpleHTTP will act just like a normal Http(s) client.