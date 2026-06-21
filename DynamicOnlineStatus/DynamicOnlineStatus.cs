using System;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Plugins.Interfaces;
using JetBrains.Annotations;

namespace DynamicOnlineStatus;

#pragma warning disable CA1812 // ASF uses this class during runtime
[UsedImplicitly]
internal sealed class DynamicOnlineStatus : IGitHubPluginUpdates {
	public string Name => nameof(DynamicOnlineStatus);
	public string RepositoryName => "tugboat-maguire/DynamicOnlineStatus";
	public Version Version => typeof(DynamicOnlineStatus).Assembly.GetName().Version ?? throw new InvalidOperationException(nameof(Version));

	public Task OnLoaded() {
		ASF.ArchiLogger.LogGenericInfo($"Hello {Name}!");

		return Task.CompletedTask;
	}
}
#pragma warning restore CA1812 // ASF uses this class during runtime
