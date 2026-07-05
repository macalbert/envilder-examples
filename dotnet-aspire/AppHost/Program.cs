using Envilder;

var builder = DistributedApplication.CreateBuilder(args);

builder.Configuration["LocalStack:UseLocalStack"] = "true";

var localstack = builder.AddLocalStack("localstack", configureContainer: container => container.ContainerImageTag = "stable")
	?? throw new InvalidOperationException(
		"LocalStack is disabled (LocalStack:UseLocalStack).");

foreach (var (key, value) in await Env.ResolveFileAsync("envilder.json"))
{
	localstack.WithEnvironment(key, value);
}

// Add your AWS resources / projects as usual and wire them to LocalStack
// var api = builder.AddProject<Projects.MyApi>("api");
// builder.UseLocalStack(localstack);

builder.Build().Run();
