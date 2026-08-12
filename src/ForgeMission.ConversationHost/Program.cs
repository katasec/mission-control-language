// ConversationHost — the future durable-conversation Silo/API (Phase 43.16). This is deliberately
// only a build/run shell: Task 4 adds the Orleans Silo and persistence, Task 6 maps routes. No
// endpoint, grain, queue, or hosted service belongs here yet.
var builder = WebApplication.CreateSlimBuilder(args);
var app = builder.Build();
app.Run();
