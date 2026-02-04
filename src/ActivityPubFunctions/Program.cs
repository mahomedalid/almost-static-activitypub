using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using ActivityPubDotNet.Core.Storage;
using ActivityPubDotNet;
using ActivityPubDotNet.Core;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((hostContext, services) =>
    {
        // Add your Azure Storage Table connection here
        services.AddSingleton(provider =>
        {
            var connectionString = hostContext.Configuration.GetSection("CoreStorageConnection");

            return new TableServiceClient(connectionString.Value);
        }).AddSingleton(provider =>
        {
            var connectionString = hostContext.Configuration.GetSection("CoreStorageConnection");

            return new BlobServiceClient(connectionString.Value);
        }).AddSingleton(provider =>
        {
            var domain = hostContext.Configuration.GetSection("BaseDomain").Value!;

            return new RepliesGenerator(
                provider.GetRequiredService<TableServiceClient>(),
                provider.GetRequiredService<BlobServiceClient>())
            {
                Domain = domain
            };
        }).AddSingleton(provider =>
        {
            var domain = hostContext.Configuration.GetSection("BaseDomain").Value!;

            return new RepliesService(
                provider.GetRequiredService<TableServiceClient>(),
                provider.GetRequiredService<RepliesGenerator>(),
                domain);
        }).AddSingleton(provider =>
        {
            return new FollowersGenerator(
                provider.GetRequiredService<TableServiceClient>(),
                provider.GetRequiredService<BlobServiceClient>())
            {
                Domain = hostContext.Configuration.GetSection("BaseDomain").Value!
            };
        }).AddSingleton(provider =>
        {
            var privatePem = hostContext.Configuration.GetSection("ActorPrivatePEMKey").Value;
            var keyId = hostContext.Configuration.GetSection("ActorKeyId").Value;

            return new ActorHelper(privatePem, keyId);
        }).AddSingleton(provider =>
        {
            return new FollowService(
                provider.GetRequiredService<TableServiceClient>(),
                provider.GetRequiredService<ActorHelper>(),
                provider.GetRequiredService<FollowersGenerator>());
        }).AddSingleton(provider =>
        {
            return new ServerConfig()
            {
                BaseDomain = hostContext.Configuration.GetSection("BaseDomain").Value!,
                ActorName = hostContext.Configuration.GetSection("ActorName").Value ?? "blog"
            };
        }).AddSingleton(provider =>
        {
            return new StampsGenerator(
                provider.GetRequiredService<BlobServiceClient>());
        }).AddSingleton(provider =>
        {
            return new QuoteRequestService(
                provider.GetRequiredService<ActorHelper>(),
                provider.GetRequiredService<ServerConfig>(),
                provider.GetRequiredService<StampsGenerator>());
        });
    })
    .Build();

host.Run();
