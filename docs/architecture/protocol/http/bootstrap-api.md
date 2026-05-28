# /bootstrap/api — Launcher Configuration

## Overview

This endpoint is the first HTTP request the Darkspore launcher makes on startup. It tells the client where all other servers (Blaze, SporeNet, Liferay) live, whether TLS is required, and optionally delivers client settings and patch metadata.

The C++ router registers this at `Game/API.cpp:354`. The C# controller is `Adapters/Rest/BootstrapRestController.cs`.

**Dispatch method:** `method=` query parameter (default: `api.config.getConfigs` when omitted).

## Endpoint Table

| Method / Route | Query `method=` | C++ handler | C# handler | Status | Notes |
|---|---|---|---|---|---|
| GET/POST `/bootstrap/api` | `api.config.getConfigs` | `API.cpp:369` → `bootstrap_config_getConfig():932` | `BootstrapRestController.cs:24` | ✅ | Primary launcher endpoint |
| GET/POST `/bootstrap/launcher/` | *(no method param)* | `API.cpp:374` | *(static fallback)* | ⚠️ | C++ serves `skipLauncherScript` HTML or a templated `wrapper.html`; C# serves via `StaticStorageAdapter` |
| GET/POST `/bootstrap/launcher/{path}` | *(no method param)* | `API.cpp:393` | *(static fallback)* | ⚠️ | C++ serves files from `CONFIG_WWW_STATIC_PATH`; C# same via static fallback |

No other `method=` values are dispatched in C++: any unrecognised method within `api.config.*` silently falls through without a response. Only `api.config.getConfigs` is handled.

## api.config.getConfigs — Response Shape

Content-Type: `text/xml`

```xml
<?xml version="1.0" encoding="UTF-8"?>
<response>
  <stat>ok</stat>
  <version>5.3.0.127</version>
  <timestamp>1</timestamp>
  <exectime>1</exectime>
  <configs>
    <config>
      <blaze_service_name>darkspore</blaze_service_name>
      <blaze_secure>N</blaze_secure>
      <blaze_env>prod</blaze_env>          <!-- prod | beta | cert | test | dev -->
      <sporenet_cdn_host>127.0.0.1</sporenet_cdn_host>
      <sporenet_db_host>127.0.0.1</sporenet_db_host>
      <sporenet_db_name>darkspore</sporenet_db_name>
      <sporenet_host>127.0.0.1</sporenet_host>
      <http_secure>N</http_secure>
      <liferay_host>127.0.0.1</liferay_host>
      <launcher_action>2</launcher_action>
      <launcher_url>http://127.0.0.1:8033/bootstrap/launcher/?version=5.3.0.127</launcher_url>
    </config>
  </configs>
  <to_image/>
  <from_image/>

  <!-- only if include_settings=true -->
  <settings>
    <open test="true">true</open>
    <telemetry-rate>256</telemetry-rate>
    <telemetry-setting>0</telemetry-setting>
  </settings>

  <!-- only if include_patches=true (stub in both C++ and C#) -->
  <patches target="test" date="test2" from_version="test3" to_version="test4"
           id="test5" description="test6" application_instructions="test6"
           locale="en-US" shipping="true" file_url="test.zip"
           archive_size="1000" uncompressed_size="2000"
           hashes="0123456789abcdef"/>
</response>
```

### Query Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `method` | string | `api.config.getConfigs` | Must equal `api.config.getConfigs` |
| `version` | string | `"1"` | Client protocol version |
| `build` | string | `5.3.0.127` | Darkspore build string; echoed into `launcher_url` |
| `include_settings` | bool | false | Append `<settings>` block |
| `include_patches` | bool | false | Append `<patches>` block (stub data in both implementations) |

### C# / C++ Alignment

Both implementations are functionally equivalent. The C# `ConfigService.getGameConfig()` builds the same `ConfigContract` fields. Patches block is commented out/stubbed in both; neither delivers real patch data.
