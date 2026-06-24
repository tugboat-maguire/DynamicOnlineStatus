using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Plugins.Interfaces;
using JetBrains.Annotations;
using SteamKit2;

namespace DynamicOnlineStatus;

#pragma warning disable CA1812 // ASF uses this class during runtime
[UsedImplicitly]
internal sealed class DynamicOnlineStatus : IGitHubPluginUpdates, IBotCardsFarmerInfo, IBotModules {
	public string Name => nameof(DynamicOnlineStatus);
	public string RepositoryName => "tugboat-maguire/DynamicOnlineStatus";
	public Version Version => typeof(DynamicOnlineStatus).Assembly.GetName().Version ?? throw new InvalidOperationException(nameof(Version));

	// Modern C# record for thread-safe multi-variable state management
	private sealed record PluginBotConfig(bool IsEnabled, int IdleDelaySeconds);
	private static readonly ConcurrentDictionary<string, PluginBotConfig> BotConfigs = new();

	public Task OnLoaded() {
		ASF.ArchiLogger.LogGenericInfo($"Hello {Name}!");
		return Task.CompletedTask;
	}

	public Task OnBotInitModules(Bot bot, IReadOnlyDictionary<string, JsonElement>? additionalConfigProperties = null) {
		if (additionalConfigProperties == null) {
			BotConfigs[bot.BotName] = new PluginBotConfig(false, 10);
			return Task.CompletedTask;
		}

		bool isEnabled = additionalConfigProperties.TryGetValue("EnableDynamicStatus", out JsonElement enabledElement) && (enabledElement.ValueKind == JsonValueKind.True);

		// Safely parse the optional delay, defaulting to 10 if missing or malformed
		int delaySeconds = 10;
		if (additionalConfigProperties.TryGetValue("IdleDelaySeconds", out JsonElement delayElement) && (delayElement.ValueKind == JsonValueKind.Number)) {
			if (delayElement.TryGetInt32(out int parsedDelay) && parsedDelay >= 0) {
				delaySeconds = parsedDelay;
			}
		}

		BotConfigs[bot.BotName] = new PluginBotConfig(isEnabled, delaySeconds);

		if (isEnabled) {
			bot.ArchiLogger.LogGenericInfo($"Dynamic Online Status ENABLED. Idle delay: {delaySeconds}s.");
		}

		return Task.CompletedTask;
	}

	public Task OnBotFarmingStarted(Bot bot) {
		if (!BotConfigs.TryGetValue(bot.BotName, out PluginBotConfig? config) || !config.IsEnabled) {
			return Task.CompletedTask;
		}

		bot.ArchiLogger.LogGenericInfo("Started farming. Setting status to Online.");

		if (bot.IsConnectedAndLoggedOn) {
			SetPersonaState(bot, EPersonaState.Online);
		}

		return Task.CompletedTask;
	}

	public Task OnBotFarmingFinished(Bot bot, bool farmedSomething) {
		// Silenced. Finished fires immediately before Stopped. We rely purely on Stopped to prevent duplicate network calls.
		return Task.CompletedTask;
	}

	public Task OnBotFarmingStopped(Bot bot) {
		return HandleIdleState(bot, "Stopped");
	}

	private static Task HandleIdleState(Bot bot, string reason) {
		if (!BotConfigs.TryGetValue(bot.BotName, out PluginBotConfig? config) || !config.IsEnabled) {
			return Task.CompletedTask;
		}

		bot.ArchiLogger.LogGenericInfo($"{reason} farming. Waiting {config.IdleDelaySeconds} seconds before going Invisible...");

		_ = Task.Run(async () => {
			try {
				await Task.Delay(TimeSpan.FromSeconds(config.IdleDelaySeconds)).ConfigureAwait(false);

				// Safely checks NowFarming. If CardsFarmer is null (bot disconnected during delay), defaults to false.
				bool isFarming = bot.CardsFarmer?.NowFarming ?? false;

				if (bot.IsConnectedAndLoggedOn && !isFarming) {
					bot.ArchiLogger.LogGenericInfo("Remained idle. Setting status to Invisible.");
					SetPersonaState(bot, EPersonaState.Invisible);
				} else {
					bot.ArchiLogger.LogGenericInfo("Resumed farming or disconnected during the delay. Aborting state change.");
				}
			} catch (Exception ex) {
				bot.ArchiLogger.LogGenericError($"DynamicOnlineStatus background task faulted: {ex.Message}");
			}
		});

		return Task.CompletedTask;
	}

	private static void SetPersonaState(Bot bot, EPersonaState state) {
		try {
			// [PublicAPI] - direct access, no reflection needed
			bot.SteamFriends.SetPersonaState(state);
			bot.ArchiLogger.LogGenericInfo($"Persona state successfully set to: {state}");
		} catch (Exception ex) {
			bot.ArchiLogger.LogGenericError($"DynamicOnlineStatus Exception: {ex.Message}");
		}
	}
}
#pragma warning restore CA1812 // ASF uses this class during runtime
