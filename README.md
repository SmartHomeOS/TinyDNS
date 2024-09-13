# Tiny DNS Client

[![Build](https://github.com/SmartHomeOS/TinyDNS/actions/workflows/dotnet.yml/badge.svg)](https://github.com/SmartHomeOS/TinyDNS/actions/workflows/dotnet.yml)
[![Version](https://img.shields.io/nuget/v/TinyDNS.svg)](https://www.nuget.org/packages/TinyDNS)

A small, fast, modern DNS / MDNS / DNS-SD client

### Features:
* Recursive resolution from root hints with no DNS servers configured
* Resolution from OS or DHCP configured DNS servers
* Resolution using common public recursive resolvers (Google, CloudFlare, etc.)
* Support for DoH (DNS over HTTPS) with options for secure or insecure lookup
* Leak protection to ensure sensitive queries are not shared with public DNS servers
* A DNS-SD and MDNS client with known answer suppression, passive caching and other mandatory and optional flood control features from the spec.
* Support for async, zerocopy, spans and all the modern .Net performance features
* See the TinyDNSDemo project for examples