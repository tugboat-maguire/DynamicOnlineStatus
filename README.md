# DynamicOnlineStatus

A custom plugin for ArchiSteamFarm (ASF) that automatically manages bot persona states based on active farming status. It sets bots to **Online** when card farming begins, and **Invisible** when farming stops. 

## Installation

1. Download the latest `.zip` release from the [Releases](../../releases) page.
2. Inside your main ASF directory, extract the contents into a new folder located at `plugins/DynamicOnlineStatus/`.
3. Restart ASF.

## Configuration

This plugin operates on a per-bot basis. To enable it, you must add the following configuration block to your specific `bot.json` file. 

```json
{
  "EnableDynamicStatus": true,
  "IdleDelaySeconds": 10
}
