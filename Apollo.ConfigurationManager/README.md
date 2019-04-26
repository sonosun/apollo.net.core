﻿# 一、准备工作

## 1.1 环境要求
    
* NETFramework 4.5+

## 1.2 必选设置
Apollo客户端依赖于`AppId`，`Environment`等环境信息来工作，所以请确保阅读下面的说明并且做正确的配置：

### 1.2.1 AppId

AppId是应用的身份信息，是从服务端获取配置的一个重要信息。

请确保在app.config或web.config有AppID的配置，其中内容形如：

```xml
<?xml version="1.0"?>
<configuration>
    <appSettings>
        <!-- Change to the actual app id -->
        <add key="Apollo.AppId" value="SampleApp"/>
    </appSettings>
</configuration>
```

> 注：Apollo.AppId是用来标识应用身份的唯一id，格式为string。

### 1.2.2 Environment

Apollo支持应用在不同的环境有不同的配置，所以Environment是另一个从服务器获取配置的重要信息。

请确保在app.config或web.config有AppID的配置，其中内容形如：

```xml
<?xml version="1.0"?>
<configuration>
    <appSettings>
        <add key="Apollo.Env" value="Dev" />
    </appSettings>
</configuration>
```

目前，`env`支持以下几个值（大小写不敏感）：
* DEV
  * Development environment
* FAT
  * Feature Acceptance Test environment
* UAT
  * User Acceptance Test environment
* PRO
  * Production environment

### 1.2.3 服务地址
Apollo客户端针对不同的环境会从不同的服务器获取配置，所以请确保在app.config或web.config正确配置了服务器地址(Apollo.{ENV}.Meta)，其中内容形如：

```xml
<?xml version="1.0"?>
<configuration>
    <appSettings>
        <!-- Should change the apollo config service url for each environment -->
        <add key="Apollo.DEV.Meta" value="http://localhost:8080"/>
        <add key="Apollo.FAT.Meta" value="http://localhost:8080"/>
        <add key="Apollo.UAT.Meta" value="http://localhost:8080"/>
        <add key="Apollo.PRO.Meta" value="http://localhost:8080"/>
    </appSettings>
</configuration>
```

或者直接Apollo.MetaServer(优先级高于上面)

```xml
<?xml version="1.0"?>
<configuration>
    <appSettings>
        <!-- Should change the apollo config service url for each environment -->
        <add key="Apollo.MetaServer" value="http://localhost:8080" />
    </appSettings>
</configuration>
```

### 1.2.4 本地缓存路径
Apollo客户端会把从服务端获取到的配置在本地文件系统缓存一份，用于在遇到服务不可用，或网络不通的时候，依然能从本地恢复配置，不影响应用正常运行。

本地缓存路径位于`C:\opt\data\{appId}\config-cache`，所以请确保`C:\opt\data\`目录存在，且应用有读写权限。

### 1.2.5 可选设置

**Cluster**（集群）

Apollo支持配置按照集群划分，也就是说对于一个appId和一个环境，对不同的集群可以有不同的配置。

* 我们可以在App.config或Web.Config文件中设置Apollo.Cluster来指定运行时集群（注意大小写）
* 例如，下面的截图配置指定了运行时的集群为SomeCluster
* ![apollo-net-apollo-cluster](https://raw.githubusercontent.com/ctripcorp/apollo/master/doc/images/apollo-net-apollo-cluster.png)

**Cluster Precedence**（集群顺序，idc暂时不支持）

1. 如果`Apollo.Cluster`和`idc`同时指定：
    * 我们会首先尝试从`Apollo.Cluster`指定的集群加载配置
    * 如果没找到任何配置，会尝试从`idc`指定的集群加载配置
    * 如果还是没找到，会从默认的集群（`default`）加载

2. 如果只指定了`Apollo.Cluster`：
    * 我们会首先尝试从`Apollo.Cluster`指定的集群加载配置
    * 如果没找到，会从默认的集群（`default`）加载

3. 如果只指定了`idc`：
    * 我们会首先尝试从`idc`指定的集群加载配置
    * 如果没找到，会从默认的集群（`default`）加载

4. 如果`Apollo.Cluster`和`idc`都没有指定：
    * 我们会从默认的集群（`default`）加载配置

# 二、引入方式

安装包Com.Ctrip.Framework.Apollo.ConfigurationManager

# 三、客户端用法

## 3.1 获取默认namespace的配置（application）

```c#
IConfig config = await ApolloConfigurationManager.GetAppConfig(); //config instance is singleton for each namespace and is never null
string someKey = "someKeyFromDefaultNamespace";
string someDefaultValue = "someDefaultValueForTheKey";
string value = config.GetProperty(someKey, someDefaultValue);
```

通过上述的**config.GetProperty**可以获取到someKey对应的实时最新的配置值。

另外，配置值从内存中获取，所以不需要应用自己做缓存。

## 3.2 监听配置变化事件

监听配置变化事件只在应用真的关心配置变化，需要在配置变化时得到通知时使用，比如：数据库连接串变化后需要重建连接等。

如果只是希望每次都取到最新的配置的话，只需要按照上面的例子，调用**config.GetProperty**即可。

```c#
IConfig config = await ApolloConfigurationManager.GetAppConfig(); //config instance is singleton for each namespace and is never null
config.ConfigChanged += new ConfigChangeEvent(OnChanged);
private void OnChanged(object sender, ConfigChangeEventArgs changeEvent)
{
	Console.WriteLine("Changes for namespace {0}", changeEvent.Namespace);
	foreach (string key in changeEvent.ChangedKeys)
	{
		ConfigChange change = changeEvent.GetChange(key);
		Console.WriteLine("Change - key: {0}, oldValue: {1}, newValue: {2}, changeType: {3}", change.PropertyName, change.OldValue, change.NewValue, change.ChangeType);
	}
}
```

## 3.3 获取公共Namespace的配置

```c#
string somePublicNamespace = "CAT";
IConfig config = await ApolloConfigurationManager.GetConfig(somePublicNamespace); //config instance is singleton for each namespace and is never null
string someKey = "someKeyFromPublicNamespace";
string someDefaultValue = "someDefaultValueForTheKey";
string value = config.GetProperty(someKey, someDefaultValue);
```

## 3.4 获取多个Namespace的合并结果

```c#
string somePublicNamespace = "CAT";
IConfig config = await ApolloConfigurationManager.GetConfig(new [] { somePublicNamespace， ConfigConsts.NamespaceApplication }); //config instance is singleton for each namespace and is never null
string someKey = "someKeyFromPublicNamespace";
string someDefaultValue = "someDefaultValueForTheKey";
string value = config.GetProperty(someKey, someDefaultValue);
```

## 3.4 Demo

apollo.net项目中有多个样例客户端的项目：
* [Apollo.AspNet.Demo](https://github.com/ctripcorp/apollo.net/tree/dotnet-core/Apollo.AspNet.Demo)
* [Apollo.ConfigurationBuilder.Demo](https://github.com/ctripcorp/apollo.net/tree/dotnet-core/Apollo.ConfigurationBuilder.Demo)
* [Apollo.ConfigurationManager.Demo](https://github.com/ctripcorp/apollo.net/tree/dotnet-core/Apollo.ConfigurationManager.Demo)

# 四、NETFramework 4.7.1+ ConfigurationBuilder支持

## 4.1 ApolloConfigurationBuilder说明
``` xml
<configuration>
    <configBuilders>
        <builders>
            <add name="ApolloConfigBuilder1" type="Com.Ctrip.Framework.Apollo.AppSettingsSectionBuilder, Com.Ctrip.Framework.Apollo.ConfigurationManager" namespace="TEST1.test;application" />
        </builders>
    </configBuilders>
</configuration>

```
* namespace为可选值，该值对应apollo中的namespace。支持多个值，以`,`或`;`分割，优先级从低到高

## 4.2 ConnectionStringsSectionBuilder使用说明
``` xml
<configuration>
    <configBuilders>
        <builders>
            <add name="ConnectionStringsSectionBuilder1" type="Com.Ctrip.Framework.Apollo.ConnectionStringsSectionBuilder, Com.Ctrip.Framework.Apollo.ConfigurationManager" namespace="TEST1.test" defaultProviderName="MySql.Data.MySqlClient" />
        </builders>
    </configBuilders>
</configuration>

```
* namespace为可选值，该值对应apollo中的namespace。支持多个值，以`,`或`;`分割，优先级从低到高
* defaultProviderName为可选值，默认值为System.Data.SqlClient,，对应ConnectionString的ProviderName。
* key必须以ConnectionStrings:开始
* 通过ConnectionStrings:ConnectionName:ConnectionString或者ConnectionStrings:ConnectionName来设置连接字符串（同时指定时ConnectionStrings:ConnectionName:ConnectionString优先级高）
* 通过ConnectionStrings:ConnectionName:ProviderName来指定使用其他数据库，比如MySql.Data.MySqlClient来指定是MySql