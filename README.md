# Tiny DNS

[![Build](https://github.com/SmartHomeOS/TinyDNS/actions/workflows/dotnet.yml/badge.svg)](https://github.com/SmartHomeOS/TinyDNS/actions/workflows/dotnet.yml)
[![Version](https://img.shields.io/nuget/v/TinyDNS.svg)](https://www.nuget.org/packages/TinyDNS)

A small, fast, modern DNS / MDNS client

### Features:
* Recursive resolution from root hints with no DNS servers configured
* Resolution from OS or DHCP configured DNS servers
* Resolution using common public recursive resolvers (Google, CloudFlare, etc.)
* Support for DoH (DNS over HTTPS) with options for secure or insecure lookup
* Leak protection to ensure sensitive queries are not shared with public DNS servers
* Support for async, zerocopy, spans and all the modern .Net performance features