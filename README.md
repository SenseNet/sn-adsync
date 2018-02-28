# Active Directory snchronization for sensenet ECM
Two-way synchronization between the Content Repository and Active Directory for the [sensenet ECM](https://github.com/SenseNet/sensenet) platform. It handles users, groups, organizational units and memberships too.

[![Join the chat at https://gitter.im/SenseNet/sn-adsync](https://badges.gitter.im/SenseNet/sn-adsync.svg)](https://gitter.im/SenseNet/sn-adsync?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

[![NuGet](https://img.shields.io/nuget/v/SenseNet.SyncPortal2AD.Install.svg)](https://www.nuget.org/packages/SenseNet.SyncPortal2AD.Install)

[![NuGet](https://img.shields.io/nuget/v/SenseNet.SyncAD2Portal.Install.svg)](https://www.nuget.org/packages/SenseNet.SyncAD2Portal.Install)

You may install this component even if you only have the **sensenet ECM Services** main component installed. That way you'll get the backend part of the ad sync feature.

If you also have the [sensenet ECM WebPages](https://github.com/SenseNet/sn-webpages) component installed (which gives you a UI framework built on *ASP.NET WebForms* for sensenet ECM), you'll also get UI elements for managing the sync process.

> To find out which packages you need to install, take a look at the available [sensenet ECM components](http://community.sensenet.com/docs/sensenet-components).

## Installation
To get started, install the AdSync component from NuGet:
- [Install sensenet ECM AdSync from NuGet](/docs/install-adsync-from-nuget.md)