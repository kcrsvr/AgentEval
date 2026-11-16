var builder = WebApplication.CreateBuilder(args);

if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
{
    builder.Configuration["OpenAI:ApiKey"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
}
if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_MODEL")))
{
    builder.Configuration["OpenAI:Model"] = Environment.GetEnvironmentVariable("OPENAI_MODEL");
}
if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_CHAT_COMPLETIONS_URL")))
{
    builder.Configuration["OpenAI:ChatCompletionsUrl"] = Environment.GetEnvironmentVariable("OPENAI_CHAT_COMPLETIONS_URL");
}

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSingleton<AgentEval.Services.ConversationRunStore>();
builder.Services.Configure<AgentEval.Services.OpenAiOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.AddHttpClient<AgentEval.Services.ICustomerTurnGenerator, AgentEval.Services.OpenAiCustomerTurnGenerator>(client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
});
builder.Services.AddHttpClient<AgentEval.Services.IAnswerEvaluator, AgentEval.Services.OpenAiAnswerEvaluator>(client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
});
builder.Services.AddSingleton<AgentEval.Services.IRoamingAgentSimulator, AgentEval.Services.LocalRoamingAgentSimulator>();
builder.Services.AddSingleton<AgentEval.Services.ConversationRunQueue>();
builder.Services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<AgentEval.Services.ConversationRunQueue>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
